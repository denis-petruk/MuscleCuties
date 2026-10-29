"""Provider-level release tests use local fake executables; no AWS or DB access."""

import base64
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
ENGINE = ROOT / "tools/deployment/release-engine.py"
AWS = r'''#!/usr/bin/env python3
import json, os, pathlib, sys
state_path = pathlib.Path(os.environ['MC_TEST_STATE'])
s = json.loads(state_path.read_text())
args = sys.argv[1:]
service, op = args[:2]
def arg(name): return args[args.index(name)+1] if name in args else None
def save(): state_path.write_text(json.dumps(s))
def out(value): print(json.dumps(value))
if service == 'sts' and op == 'get-caller-identity': out({'Account':s['account']})
elif service == 'rds' and op == 'describe-db-instances':
    out({'DBInstances':[{'DBInstanceIdentifier':'db-test','DBName':'mc_test','Endpoint':{'Address':'db.local'},'DBInstanceStatus':'available'}]})
elif service == 'rds' and op == 'describe-db-snapshots':
    out({'DBSnapshots':[{'DBInstanceIdentifier':'db-test','Status':'available'}]})
elif service == 'apigatewayv2' and op == 'get-integrations':
    out({'Items':[{'IntegrationType':'AWS_PROXY','IntegrationUri':s['integration_uri']}]})
elif service == 'lambda' and op == 'get-function-configuration':
    s['config_reads'] += 1
    revision = 'raced' if s.get('stale') and s['config_reads'] > 1 and not s['updates'] else s['revision']
    out({'FunctionArn':s['function_arn'],'Runtime':'dotnet10','Architectures':['arm64'],
         'PackageType':'Zip','Handler':'MuscleCuties.Server','State':'Active',
         'LastUpdateStatus':'Successful','RevisionId':revision,'CodeSha256':s['code_sha'],
         'Environment':{'Variables':{'ConnectionStrings__PostgreSql':
         'Host=db.local;Database=mc_test;SSL Mode=VerifyFull;Password=synthetic'}}})
    save()
elif service == 's3api' and op == 'head-object':
    if (arg('--key') or '').startswith('reports/'):
        out({'VersionId':'report-v1','ChecksumSHA256':s.get('report_checksum')})
    elif (arg('--key') or '').startswith('lambda/MuscleCuties.Server/known-good/'):
        out({'VersionId':'good-v1','ContentLength':1,'ChecksumSHA256':s['good_b64'],
             'Metadata':{'zip-sha256':s['good_hex'],'source-sha':s['good_source_sha']}})
    else:
        out({'VersionId':'v1','ContentLength':1,
             'ChecksumSHA256':'wrong' if s.get('bad_object') else s['candidate_b64'],
             'Metadata':{'zip-sha256':s['candidate_hex'],'source-sha':s['source_sha']}})
elif service == 's3api' and op == 'put-object':
    if s.get('report_fail'): sys.exit(1)
    s['report_checksum'] = arg('--checksum-sha256'); save(); out({'VersionId':'report-v1'})
elif service == 'lambda' and op == 'update-function-code':
    s['updates'] += 1
    s['code_sha'] = s['good_b64'] if (arg('--s3-key') or '').startswith('lambda/MuscleCuties.Server/known-good/') else s['candidate_b64']
    s['revision'] = 'after-update'; save()
    out({'CodeSha256':s['code_sha'],'RevisionId':'after-update'})
elif service == 'lambda' and op == 'wait':
    if s.get('wait_fail'): sys.exit(1)
    out({})
elif service == 'lambda' and op == 'publish-version': out({'Version':'2'})
elif service == 'lambda' and op == 'get-alias':
    out({'FunctionVersion':'stale' if s.get('alias_race') else '1','RevisionId':'alias-rev1'})
elif service == 'lambda' and op == 'update-alias':
    s['alias_updates'] = s.get('alias_updates',0)+1; save()
    out({'FunctionVersion':arg('--function-version'),'RevisionId':'alias-rev2'})
else: sys.exit(1)
'''
MAINTENANCE = r'''#!/usr/bin/env python3
import json, os, sys
if os.environ.get('MC_TEST_SCHEMA_FAIL') == '1': sys.exit(1)
print(json.dumps({'contract_version':'1.0.0','status':'compatible',
    'requirements_sha256':sys.argv[3],
    'db_fingerprint_sha256':'f'*64,'migration_history_mode':'reviewed',
    'failure_codes':[]}))
'''
RUNTIME = r'''#!/usr/bin/env python3
import os, sys
fail = os.environ.get('MC_TEST_RUNTIME_FAIL') == '1'
candidate_only = os.environ.get('MC_TEST_RUNTIME_FAIL_FOR_CANDIDATE') == '1' and sys.argv[1] == os.environ['MC_TEST_CANDIDATE_B64']
sys.exit(1 if fail or candidate_only else 0)
'''


class ReleaseEngineTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.dir = Path(self.tmp.name)
        self.source_sha = 'a' * 40
        self.tree_sha = 'b' * 40
        self.candidate_bytes = b'candidate'
        self.candidate_hex = hashlib.sha256(self.candidate_bytes).hexdigest()
        self.candidate_b64 = base64.b64encode(hashlib.sha256(self.candidate_bytes).digest()).decode()
        self.good_hex = hashlib.sha256(b'known-good').hexdigest()
        self.good_b64 = base64.b64encode(hashlib.sha256(b'known-good').digest()).decode()
        self.good_source_sha = 'd' * 40
        self.function_arn = 'arn:aws:lambda:us-east-2:123456789012:function:mc-test'
        self.integration_uri = 'arn:aws:apigateway:us-east-2:lambda:path/2015-03-31/functions/' + self.function_arn + '/invocations'
        self.req = self.dir / 'requirements.json'; self.req.write_text('{}')
        self.req_hash = hashlib.sha256(b'{}').hexdigest()
        self.policy = self.dir / 'policy.json'
        self.manifest = self.dir / 'manifest.json'
        self.object = self.dir / 'object.json'
        self.report = self.dir / 'report.json'
        self.state = self.dir / 'state.json'
        policy = {
            'contract_version':1,'target_id':'staging','region':'us-east-2','account_id':'123456789012',
            'function_arn':self.function_arn,'function_name':'mc-test','database_identifier':'db-test',
            'database_name':'mc_test','database_host':'db.local','backup_snapshot_id':'snapshot-test',
            'backup_approved':True,'restore_drill_evidence_sha256':'c'*64,
            'api_id':'api123','approved_integration_uri':self.integration_uri,'integration_mode':'unqualified',
            'maintenance_window_approved':True,'release_bucket':'release-test',
            'approved_source_sha':self.source_sha,'approved_tree_sha':self.tree_sha,
            'candidate_requirements_path':str(self.req),'candidate_requirements_sha256':self.req_hash,
            'serving_requirements_path':str(self.req),'serving_requirements_sha256':self.req_hash,
            'reconciliation':'none'
        }
        self.policy.write_text(json.dumps(policy))
        self.manifest.write_text(json.dumps({
            'source_sha':self.source_sha,'tree_sha':self.tree_sha,
            'zip_sha256':self.candidate_hex,'zip_sha256_base64':self.candidate_b64,
            'zip_bytes':1,'runtime':'net10.0','architecture':'arm64','handler':'MuscleCuties.Server',
            'requirements_sha256':self.req_hash}))
        self.object.write_text(json.dumps({
            'bucket':'release-test','key':f'lambda/MuscleCuties.Server/{self.source_sha}/{self.candidate_hex}/MuscleCuties.Server.zip',
            'version_id':'v1','zip_sha256':self.candidate_hex,'zip_sha256_base64':self.candidate_b64,'zip_bytes':1}))
        self.state.write_text(json.dumps({
            'account':'123456789012','function_arn':self.function_arn,'integration_uri':self.integration_uri,
            'candidate_hex':self.candidate_hex,'candidate_b64':self.candidate_b64,'source_sha':self.source_sha,
            'code_sha':'old-code','revision':'before-update','config_reads':0,'updates':0,
            'good_b64':self.good_b64,'good_hex':self.good_hex,'good_source_sha':self.good_source_sha}))
        for name, content in [('aws', AWS),('maintenance', MAINTENANCE),('runtime', RUNTIME)]:
            path = self.dir / name; path.write_text(content); path.chmod(0o700)
        self.env = dict(os.environ,
                        MC_RELEASE_OFFLINE_TEST='1', MC_TEST_AWS_EXECUTABLE=str(self.dir / 'aws'),
                        MC_TEST_MAINTENANCE_EXECUTABLE=str(self.dir / 'maintenance'),
                        MC_TEST_RUNTIME_EXECUTABLE=str(self.dir / 'runtime'), MC_TEST_STATE=str(self.state),
                        CI='false',CI_PROJECT_PATH='denis-petruk/musclecuties-testflight-beta',
                        CI_PIPELINE_SOURCE='web',CI_COMMIT_REF_PROTECTED='true',
                        CI_ENVIRONMENT_NAME='staging',CI_COMMIT_BRANCH='codex/plan-generation-checkpoint',
                        CI_COMMIT_SHA=self.source_sha,CI_COMMIT_TREE_SHA=self.tree_sha,
                        CI_PIPELINE_ID='1',CI_JOB_ID='2',MC_TEST_CANDIDATE_B64=self.candidate_b64)

    def run_engine(self, **extra):
        env = dict(self.env, **extra)
        return subprocess.run(['python3',str(ENGINE),str(self.policy),str(self.manifest),str(self.object),str(self.report)],
                              env=env, capture_output=True, text=True)

    def report_data(self): return json.loads(self.report.read_text())
    def state_data(self): return json.loads(self.state.read_text())

    def test_verified_update_and_duplicate_retry(self):
        self.assertEqual(self.run_engine().returncode, 0)
        self.assertEqual(self.report_data()['status'], 'verified')
        self.assertEqual(self.state_data()['updates'], 1)
        self.assertEqual(self.run_engine().returncode, 0)
        self.assertEqual(self.report_data()['failure_code'], 'already_serving_verified')
        self.assertEqual(self.state_data()['updates'], 1)

    def test_stale_revision_blocks_update(self):
        state = self.state_data(); state['stale'] = True; self.state.write_text(json.dumps(state))
        self.assertNotEqual(self.run_engine().returncode, 0)
        self.assertEqual(self.report_data()['failure_code'], 'stale_lambda_revision')
        self.assertEqual(self.state_data()['updates'], 0)

    def test_object_mismatch_blocks_update(self):
        state = self.state_data(); state['bad_object'] = True; self.state.write_text(json.dumps(state))
        self.assertNotEqual(self.run_engine().returncode, 0)
        self.assertEqual(self.report_data()['failure_code'], 'release_object_mismatch')
        self.assertEqual(self.state_data()['updates'], 0)

    def test_schema_failure_blocks_update(self):
        self.assertNotEqual(self.run_engine(MC_TEST_SCHEMA_FAIL='1').returncode, 0)
        self.assertEqual(self.report_data()['failure_code'], 'schema_schema_preflight_failed')
        self.assertEqual(self.state_data()['updates'], 0)

    def test_runtime_failure_needs_known_good(self):
        self.assertNotEqual(self.run_engine(MC_TEST_RUNTIME_FAIL='1').returncode, 0)
        self.assertEqual(self.report_data()['status'], 'partial')
        self.assertEqual(self.report_data()['recovery'], 'manual_required')
        self.assertEqual(self.state_data()['updates'], 1)

    def test_waiter_timeout_is_partial_and_does_not_repeat_update(self):
        state = self.state_data(); state['wait_fail'] = True; self.state.write_text(json.dumps(state))
        self.assertNotEqual(self.run_engine().returncode, 0)
        self.assertEqual(self.report_data()['failure_code'], 'lambda_wait_failed')
        self.assertEqual(self.state_data()['updates'], 1)

    def test_partial_restart_revalidates_then_finishes_duplicate(self):
        self.assertNotEqual(self.run_engine(MC_TEST_RUNTIME_FAIL='1').returncode, 0)
        self.assertEqual(self.state_data()['updates'], 1)
        self.assertEqual(self.run_engine().returncode, 0)
        self.assertEqual(self.report_data()['failure_code'], 'already_serving_verified')
        self.assertEqual(self.state_data()['updates'], 1)

    def test_alias_race_blocks_cutover(self):
        policy = json.loads(self.policy.read_text())
        policy.update(integration_mode='alias',alias='live',expected_serving_version='1',
                      approved_integration_uri=self.integration_uri.replace(self.function_arn,self.function_arn+':live'))
        self.policy.write_text(json.dumps(policy))
        state = self.state_data(); state['integration_uri'] = policy['approved_integration_uri']; state['alias_race'] = True
        self.state.write_text(json.dumps(state))
        self.assertNotEqual(self.run_engine().returncode, 0)
        self.assertEqual(self.report_data()['failure_code'], 'alias_stale')
        self.assertEqual(self.state_data().get('alias_updates',0), 0)

    def test_verified_known_good_can_recover_failed_smoke(self):
        policy = json.loads(self.policy.read_text())
        policy['known_good'] = {
            'runtime_verified':True,'db_fingerprint_sha256':'f'*64,'migration_history_mode':'reviewed',
            'requirements_path':str(self.req),'requirements_sha256':self.req_hash,
            'bucket':'release-test','key':'lambda/MuscleCuties.Server/known-good/'+self.good_hex+'/MuscleCuties.Server.zip',
            'version_id':'good-v1','code_sha256_base64':self.good_b64,
            'zip_sha256':self.good_hex,'source_sha':self.good_source_sha}
        self.policy.write_text(json.dumps(policy))
        self.assertNotEqual(self.run_engine(MC_TEST_RUNTIME_FAIL_FOR_CANDIDATE='1').returncode, 0)
        self.assertEqual(self.report_data()['recovery'], 'verified_known_good')
        self.assertEqual(self.state_data()['updates'], 2)
        self.assertEqual(self.state_data()['code_sha'], self.good_b64)

    def test_report_persistence_failure_is_not_success(self):
        state = self.state_data(); state['report_fail'] = True; self.state.write_text(json.dumps(state))
        self.assertNotEqual(self.run_engine().returncode, 0)
        self.assertEqual(self.report_data()['failure_code'], 'durable_report_failed')
        self.assertEqual(self.report_data()['status'], 'partial')


if __name__ == '__main__': unittest.main()
