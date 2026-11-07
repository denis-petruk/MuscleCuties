#!/usr/bin/env python3
"""Offline tests for the GitHub Lambda deployer using a fake AWS CLI."""
import base64
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / 'tools/deployment/deploy_release.py'
FAKE_AWS = r'''#!/usr/bin/env python3
import json, os, sys
args = sys.argv[1:]
state_path = os.environ['FAKE_STATE']
state = json.loads(open(state_path).read()) if os.path.exists(state_path) else {}
service, operation = args[0], args[1]
function = args[args.index('--function-name') + 1] if '--function-name' in args else 'unknown'
target = os.environ['TARGET_WORKER_B64'] if 'worker' in function else os.environ['TARGET_SERVER_B64']
old = os.environ['OLD_B64']
bucket_state = state.setdefault(function, {'updated': False, 'alias': '6'})
def save():
    open(state_path, 'w').write(json.dumps(state))
if service == 'sts':
    print(json.dumps({'Account': os.environ['ACCOUNT']}))
elif operation == 'get-function-configuration':
    print(json.dumps({'State': 'Active', 'LastUpdateStatus': 'Successful',
                      'CodeSha256': target if bucket_state['updated'] else old,
                      'RevisionId': 'rev-2' if bucket_state['updated'] else 'rev-1',
                      'Version': '$LATEST'}))
elif operation == 'update-function-code':
    if os.environ.get('FAKE_SCENARIO') == 'mismatch':
        print(json.dumps({'CodeSha256': 'wrong'}))
    else:
        bucket_state['updated'] = True
        save()
        print(json.dumps({'CodeSha256': target}))
elif operation == 'wait':
    sys.exit(0)
elif operation == 'publish-version':
    print(json.dumps({'Version': '7'}))
elif operation == 'get-alias':
    print(json.dumps({'FunctionVersion': bucket_state['alias'], 'RevisionId': 'alias-rev'}))
elif operation == 'update-alias':
    version = args[args.index('--function-version') + 1]
    bucket_state['alias'] = version
    save()
    print(json.dumps({'FunctionVersion': version}))
elif operation == 'get-function':
    qualifier = args[args.index('--qualifier') + 1]
    print(json.dumps({'Configuration': {'CodeSha256': old if qualifier == '6' else target,
                                        'Version': qualifier}}))
else:
    print('unsupported fake operation', file=sys.stderr)
    sys.exit(3)
'''


class DeployTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.fake = self.root / 'aws'
        self.fake.write_text(FAKE_AWS)
        self.fake.chmod(0o755)
        self.state = self.root / 'state.json'
        self.source = 'a' * 40
        self.tree = 'b' * 40
        self.env = dict(os.environ,
                        TARGET_ENV='beta', AWS_REGION='us-east-2', ACCOUNT='028417007003',
                        AWS_ACCOUNT_ID='028417007003',
                        SOURCE_SHA=self.source, SOURCE_TREE_SHA=self.tree,
                        SERVER_FUNCTION='fn-server', SERVER_ALIAS='live',
                        WORKER_FUNCTION='fn-worker', WORKER_ALIAS='live',
                        REPORT_PATH=str(self.root / 'report.json'),
                        MC_TEST_AWS_EXECUTABLE=str(self.fake),
                        FAKE_STATE=str(self.state), FAKE_SCENARIO='happy',
                        OLD_B64=base64.b64encode(hashlib.sha256(b'old').digest()).decode())
        self.server = self.make_component('Server', b'server')
        self.worker = self.make_component('WorkoutReconciliation', b'worker')
        self.env.update(self.server['env'])
        self.env.update(self.worker['env'])
        self.env['TARGET_SERVER_B64'] = self.server['b64']
        self.env['TARGET_WORKER_B64'] = self.worker['b64']

    def make_component(self, name, payload):
        zip_path = self.root / (name + '.zip')
        zip_path.write_bytes(payload)
        digest = hashlib.sha256(payload).digest()
        b64 = base64.b64encode(digest).decode()
        manifest = self.root / (name + '-manifest.json')
        manifest.write_text(json.dumps({
            'source_sha': self.source, 'tree_sha': self.tree,
            'zip_sha256': digest.hex(), 'zip_sha256_base64': b64,
            'zip_bytes': len(payload)}))
        obj = self.root / (name + '-object.json')
        obj.write_text(json.dumps({
            'bucket': 'release-bucket',
            'key': f'lambda/{name}/{self.source}/{digest.hex()}/{name}.zip',
            'version_id': 'v1', 'zip_sha256': digest.hex(),
            'zip_sha256_base64': b64, 'zip_bytes': len(payload)}))
        prefix = 'SERVER' if name == 'Server' else 'WORKER'
        return {
            'b64': b64,
            'env': {
                f'{prefix}_FUNCTION': 'fn-server' if name == 'Server' else 'fn-worker',
                f'{prefix}_ALIAS': 'live',
                f'{prefix}_ZIP': str(zip_path),
                f'{prefix}_MANIFEST': str(manifest),
                f'{prefix}_OBJECT': str(obj),
            },
        }

    def run_deploy(self):
        return subprocess.run([ 'python3', str(SCRIPT), 'deploy' ], env=self.env,
                              capture_output=True, text=True)

    def run_rollback(self):
        return subprocess.run([ 'python3', str(SCRIPT), 'rollback' ], env=self.env,
                              capture_output=True, text=True)

    def report(self):
        return json.loads((self.root / 'report.json').read_text())

    def test_both_functions_are_verified(self):
        result = self.run_deploy()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        report = self.report()
        self.assertEqual(report['status'], 'verified')
        self.assertEqual(report['components']['server']['post_code_sha256'], self.server['b64'])
        self.assertEqual(report['components']['worker']['post_code_sha256'], self.worker['b64'])
        self.assertEqual(report['components']['server']['previous_alias_version'], '6')
        self.assertEqual(report['components']['server']['post_version'], '7')
        self.assertEqual(report['components']['worker']['post_version'], '7')

    def test_digest_mismatch_never_reports_verified(self):
        self.env['FAKE_SCENARIO'] = 'mismatch'
        result = self.run_deploy()
        self.assertNotEqual(result.returncode, 0)
        report = self.report()
        self.assertNotEqual(report['status'], 'verified')
        self.assertEqual(report['failure_code'], 'lambda_update_hash_mismatch')
        self.assertEqual(report['recovery'], 'manual_rollback_required')
        self.assertFalse((report['components']['server']['post_code_sha256']))

    def test_alias_rollback_restores_previous_version(self):
        self.assertEqual(self.run_deploy().returncode, 0)
        # Both aliases now point at 7; roll back to the recorded 6 / old digest.
        self.env['FAKE_SCENARIO'] = 'rollback'
        result = self.run_rollback()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        report = self.report()
        self.assertEqual(report['status'], 'rolled_back')
        self.assertEqual(report['rollback']['server']['restored_version'], '6')
        self.assertEqual(report['rollback']['worker']['restored_version'], '6')

    def test_missing_alias_fails_before_mutation(self):
        self.env.pop('SERVER_ALIAS')
        result = self.run_deploy()
        self.assertNotEqual(result.returncode, 0)
        report = self.report()
        self.assertEqual(report['status'], 'blocked')
        self.assertEqual(report['failure_code'], 'missing_server_alias')
        self.assertFalse(report['mutation_attempted'])


if __name__ == '__main__':
    unittest.main()
