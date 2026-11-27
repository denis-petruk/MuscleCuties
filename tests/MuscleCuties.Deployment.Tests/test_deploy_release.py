#!/usr/bin/env python3
"""Offline tests for the GitHub Lambda deployer using a fake AWS CLI."""
import base64
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import re
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
target = os.environ['TARGET_SERVER_B64']
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
                        REPORT_PATH=str(self.root / 'report.json'),
                        MC_TEST_AWS_EXECUTABLE=str(self.fake),
                        FAKE_STATE=str(self.state), FAKE_SCENARIO='happy',
                        OLD_B64=base64.b64encode(hashlib.sha256(b'old').digest()).decode())
        self.server = self.make_component('Server', b'server')
        self.env.update(self.server['env'])
        self.env['TARGET_SERVER_B64'] = self.server['b64']
        # These tests describe an offline deploy driven by explicit inputs. A real GitHub run exports
        # GITHUB_EVENT_NAME/GITHUB_REPOSITORY, and the deployer correctly refuses a non-dispatch event,
        # so the inherited values would replace the case under test with manual_release_required.
        for name in [key for key in self.env if key.startswith('GITHUB_')]:
            del self.env[name]

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
        return {
            'b64': b64,
            'env': {
                'SERVER_FUNCTION': 'fn-server',
                'SERVER_ALIAS': 'live',
                'SERVER_ZIP': str(zip_path),
                'SERVER_MANIFEST': str(manifest),
                'SERVER_OBJECT': str(obj),
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

    def test_server_is_verified(self):
        result = self.run_deploy()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        report = self.report()
        self.assertEqual(report['status'], 'verified')
        self.assertEqual(report['components']['server']['post_code_sha256'], self.server['b64'])
        self.assertEqual(report['components']['server']['previous_alias_version'], '6')
        self.assertEqual(report['components']['server']['post_version'], '7')

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

    def test_missing_alias_fails_before_mutation(self):
        self.env.pop('SERVER_ALIAS')
        result = self.run_deploy()
        self.assertNotEqual(result.returncode, 0)
        report = self.report()
        self.assertEqual(report['status'], 'blocked')
        self.assertEqual(report['failure_code'], 'missing_server_alias')
        self.assertFalse(report['mutation_attempted'])


    def test_push_to_main_releases_automatically(self):
        # ci.yml calls the deployer after its required gate on a push to main, so that event must
        # be accepted; the deployer is the only thing that can see the event itself.
        self.env['GITHUB_EVENT_NAME'] = 'push'
        self.env['GITHUB_REF'] = 'refs/heads/main'
        result = self.run_deploy()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(self.report()['status'], 'verified')

    def test_other_events_are_refused_before_mutation(self):
        for event, ref in (('push', 'refs/heads/feature'), ('pull_request', 'refs/heads/main')):
            with self.subTest(event=event, ref=ref):
                self.env['GITHUB_EVENT_NAME'] = event
                self.env['GITHUB_REF'] = ref
                result = self.run_deploy()
                self.assertNotEqual(result.returncode, 0)
                report = self.report()
                self.assertEqual(report['failure_code'], 'manual_release_required')
                self.assertFalse(report['mutation_attempted'])


class WorkflowContractTests(unittest.TestCase):
    """The workflow YAML and the deployer must agree; they are one system."""

    WORKFLOW = ROOT / '.github' / 'workflows' / 'backend-release.yml'
    SCRIPT = ROOT / 'tools' / 'deployment' / 'deploy_release.py'

    SECRETS = ('IOS_DISTRIBUTION_CERTIFICATE_P12', 'IOS_DISTRIBUTION_CERTIFICATE_PASSWORD',
               'IOS_PROVISIONING_PROFILE', 'ASC_API_KEY_ID', 'ASC_API_ISSUER_ID',
               'ASC_API_PRIVATE_KEY')

    def setUp(self):
        self.workflow = self.WORKFLOW.read_text()
        self.script = self.SCRIPT.read_text()
        self.ci = (ROOT / '.github' / 'workflows' / 'ci.yml').read_text()
        self.mobile = (ROOT / '.github' / 'workflows' / 'mobile-release.yml').read_text()

    def job_block(self, workflow_text, job):
        match = re.search(r'\n  ' + job + r':\n(.*?)(?=\n  [a-z_]+:|\Z)', workflow_text, re.S)
        self.assertIsNotNone(match, job + ' is not wired into the workflow')
        return match.group(1)

    def step_script(self, workflow_text, step):
        # The checks are shell inside the workflows, so the tests extract and run that exact shell
        # rather than a copy of its logic that could drift from what Actions executes.
        start = workflow_text.index('      - name: ' + step)
        run_at = workflow_text.index('        run: |\n', start) + len('        run: |\n')
        script = []
        for line in workflow_text[run_at:].splitlines():
            if line.strip() and not line.startswith('          '):
                break
            script.append(line[10:])
        return '\n'.join(script)

    def gate_script(self):
        return self.step_script(self.ci, 'Check the TestFlight secrets')

    def run_gate(self, present):
        return self.run_script(self.gate_script(), present)

    def run_script(self, script, present):
        with tempfile.TemporaryDirectory() as tmp:
            output = Path(tmp) / 'output'
            summary = Path(tmp) / 'summary'
            output.touch()
            summary.touch()
            env = {name: value for name, value in os.environ.items()
                   if not name.startswith('GITHUB_')}
            for name in self.SECRETS:
                env.pop(name, None)
            for name in present:
                env[name] = 'set'
            env['GITHUB_OUTPUT'] = str(output)
            env['GITHUB_STEP_SUMMARY'] = str(summary)
            result = subprocess.run(['bash', '-c', script], env=env,
                                    capture_output=True, text=True)
            return result, output.read_text(), summary.read_text()

    def test_artifact_component_matches_the_key_prefix_the_deployer_requires(self):
        # validate_component expects lambda/<spec name>/<sha>/<digest>/, so the upload step has to
        # file the object under exactly that name. It once filed under MuscleCuties.Server, which
        # failed the release after the upload had already succeeded.
        self.assertIn('component("Server"', self.script)
        self.assertIn('ARTIFACT_COMPONENT: Server\n', self.workflow)
        self.assertNotIn('ARTIFACT_COMPONENT: MuscleCuties.Server', self.workflow)

    def test_deploy_step_supplies_every_required_variable(self):
        required = ['TARGET_ENV', 'AWS_REGION', 'SERVER_FUNCTION', 'SERVER_ALIAS',
                    'SERVER_ZIP', 'SERVER_MANIFEST', 'SERVER_OBJECT', 'REPORT_PATH', 'HEALTH_URL']
        for name in required:
            self.assertIn('\n          ' + name + ':', self.workflow,
                          name + ' is read by the deployer but never supplied by the workflow')

    def test_artifact_paths_named_by_the_deployer_are_produced_upstream(self):
        # Every file the deploy step names must actually be written by the packaging job or by the
        # upload step; a rename in either place would otherwise only surface during a real release.
        build = (ROOT / '.github' / 'workflows' / 'backend-build.yml').read_text()
        produced = build + self.workflow
        for name in ('SERVER_ZIP', 'SERVER_MANIFEST', 'SERVER_OBJECT'):
            match = re.search(r'\n          ' + name + r': (\S+)', self.workflow)
            self.assertIsNotNone(match, name + ' is never supplied')
            base = match.group(1).rsplit('/', 1)[-1]
            self.assertIn(base, produced, base + ' is named but never produced')

    def test_ci_releases_only_after_the_required_gate_on_main(self):
        # Deploying off a failed CI run is the failure this guards: each release job must wait for
        # the aggregate `required` job and must never run for a branch push or a pull request.
        backend = self.job_block(self.ci, 'release_backend_beta')
        self.assertIn('needs: required', backend)
        self.assertIn("github.event_name == 'push'", backend)
        self.assertIn("github.ref == 'refs/heads/main'", backend)
        self.assertIn('uses: ./.github/workflows/backend-release.yml', backend)

        gate = self.job_block(self.ci, 'release_ios_precondition')
        self.assertIn('needs: required', gate)
        self.assertIn("github.event_name == 'push'", gate)
        self.assertIn("github.ref == 'refs/heads/main'", gate)
        self.assertIn('ready: ${{ steps.secrets.outputs.ready }}', gate)

        ios = self.job_block(self.ci, 'release_ios_testflight')
        self.assertIn('needs: [required, release_ios_precondition]', ios)
        self.assertIn("needs.release_ios_precondition.outputs.ready == 'true'", ios)
        self.assertIn('uses: ./.github/workflows/mobile-release.yml', ios)

    def test_missing_apple_secrets_skip_only_testflight_with_a_notice(self):
        # Absent secrets must skip the iOS release with an actionable notice, not fail the run and
        # not let the Lambda release wait on a gate that has nothing to do with it.
        for present in ((), self.SECRETS[:-1]):
            with self.subTest(present=len(present)):
                result, output, summary = self.run_gate(present)
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertEqual(output.strip(), 'ready=false')
                self.assertIn('skipped', summary)
                self.assertIn('docs/mobile/ipa-for-transporter.md', summary)
                self.assertIn('Settings → Secrets and variables → Actions', summary)
                self.assertIn('::warning::', result.stdout)
                missing = [name for name in self.SECRETS if name not in present]
                for name in missing:
                    self.assertIn(name, summary)
                if present:
                    self.assertNotIn(present[0], summary)
        backend = self.job_block(self.ci, 'release_backend_beta')
        for reference in ('release_ios_precondition', 'release_ios_testflight'):
            self.assertNotIn(reference, backend,
                             'the backend release must not wait on the iOS gate')

    def test_present_apple_secrets_make_the_testflight_job_run(self):
        result, output, summary = self.run_gate(self.SECRETS)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(output.strip(), 'ready=true')
        self.assertEqual(summary.strip(), '')
        self.assertNotIn('::warning::', result.stdout)
        # The gate adds nothing to the release itself: the same reusable workflow still builds and
        # uploads, so the signing logic exists in exactly one place.
        ios = self.job_block(self.ci, 'release_ios_testflight')
        self.assertIn('uses: ./.github/workflows/mobile-release.yml', ios)
        self.assertNotIn('dotnet publish', self.ci)
        self.assertNotIn('altool', self.ci)

    def test_manual_dispatch_reports_every_missing_apple_secret_at_once(self):
        # A dispatch has no gate job to skip it, so the workflow's own first step must name every
        # missing secret in one notice rather than fail at the first step that needs one.
        script = self.step_script(self.mobile, 'Check the TestFlight secrets')
        self.assertLess(self.mobile.index('- name: Check the TestFlight secrets'),
                        self.mobile.index('- name: Checkout repository'))
        result, _, summary = self.run_script(script, ())
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('::error::TestFlight upload blocked', result.stdout)
        self.assertIn('docs/mobile/ipa-for-transporter.md', summary)
        for name in self.SECRETS:
            self.assertIn(name, summary)
            self.assertIn(name, result.stdout)
        result, _, summary = self.run_script(script, self.SECRETS)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(summary.strip(), '')
        self.assertNotIn('::error::', result.stdout)

    def test_the_apple_secret_list_exists_once_and_matches_the_ci_gate(self):
        # The new step is the only presence check: the old per-step checks could only ever report
        # one secret at a time, and both lists must name the same six so they cannot drift apart.
        self.assertNotIn('is not set: add it as a repository secret', self.mobile)
        start = self.mobile.index('- name: Check the TestFlight secrets')
        end = self.mobile.index('- name: Checkout repository')
        listed = set(re.findall(r'\n          ([A-Z][A-Z0-9_]+): \$\{\{ secrets\.',
                                self.mobile[start:end]))
        gate = set(re.findall(r'\n          ([A-Z][A-Z0-9_]+): \$\{\{ secrets\.',
                              self.job_block(self.ci, 'release_ios_precondition')))
        self.assertEqual(listed, set(self.SECRETS))
        self.assertEqual(gate, listed)

    def test_oidc_jobs_request_id_token(self):
        for job in ('upload_artifacts', 'deploy_beta', 'rollback_beta'):
            start = self.workflow.index(job + ':')
            self.assertIn('id-token: write', self.workflow[start:start + 900], job)


    def test_ios_certificate_is_validated_before_import(self):
        # The empty-secret failure must name the secret before security import, and an expired
        # identity must fail here rather than at codesign with a less specific message.
        start = self.mobile.index('- name: Import the distribution identity')
        end = self.mobile.index('- name: Install the App Store provisioning profile')
        step = self.mobile[start:end]
        for check in ('[ ! -s "$p12" ]', 'openssl pkcs12 -in "$p12" -noout',
                      'x509 -noout -checkend 0'):
            self.assertIn(check, step)
        self.assertLess(step.index('[ ! -s "$p12" ]'), step.index('security import "$p12"'))


if __name__ == '__main__':
    unittest.main()
