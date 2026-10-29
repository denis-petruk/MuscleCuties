#!/usr/bin/env python3
"""Offline upload tests using a fake AWS CLI; no AWS account is contacted."""
import base64
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / 'tools/deployment/upload-artifact.sh'

class UploadTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.zip = self.root / 'package.zip'
        self.zip.write_bytes(b'synthetic package')
        self.digest = hashlib.sha256(self.zip.read_bytes()).digest()
        self.manifest = self.root / 'manifest.json'
        self.manifest.write_text(json.dumps({'source_sha': 'a' * 40, 'zip_sha256': self.digest.hex(), 'zip_sha256_base64': base64.b64encode(self.digest).decode(), 'zip_bytes': self.zip.stat().st_size}))
        fake = self.root / 'aws'
        fake.write_text('''#!/usr/bin/env python3
import json, os, sys
args = sys.argv
state = os.environ['FAKE_STATE']
if 'put-object' in args:
    open(state, 'w').write('created')
    print('{}')
elif not os.path.exists(state):
    print('An error occurred (404): Not Found', file=sys.stderr); sys.exit(1)
else:
    print(json.dumps({'ContentLength': int(os.environ['SIZE']), 'ChecksumSHA256': os.environ['CHECKSUM'], 'VersionId': os.environ.get('VERSION', 'v1'), 'Metadata': {'source-sha': 'a'*40, 'zip-sha256': os.environ['HEX']}}))
''')
        fake.chmod(0o755)
        self.env = dict(os.environ, PATH=str(self.root) + os.pathsep + os.environ['PATH'], AWS_RELEASE_BUCKET='fixture', AWS_REGION='us-east-2', FAKE_STATE=str(self.root/'state'), SIZE=str(self.zip.stat().st_size), CHECKSUM=base64.b64encode(self.digest).decode(), HEX=self.digest.hex())
    def run_upload(self):
        return subprocess.run([str(SCRIPT), str(self.zip), str(self.manifest), str(self.root/'output.json')], env=self.env, capture_output=True, text=True)
    def test_create_and_retry_reuse(self):
        self.assertEqual(self.run_upload().returncode, 0)
        self.assertEqual(self.run_upload().returncode, 0)
        output = json.loads((self.root/'output.json').read_text())
        self.assertEqual(output['version_id'], 'v1')
        self.assertIn(self.digest.hex(), output['key'])
    def test_mismatched_checksum_fails(self):
        self.env['CHECKSUM'] = 'incorrect'
        self.assertNotEqual(self.run_upload().returncode, 0)
    def test_unversioned_object_fails(self):
        self.env['VERSION'] = 'null'
        self.assertNotEqual(self.run_upload().returncode, 0)
    def test_stale_manifest_fails_before_aws(self):
        self.zip.write_bytes(b'changed')
        self.assertNotEqual(self.run_upload().returncode, 0)
        self.assertFalse((self.root/'state').exists())

class ValidationTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        (self.root / '.tools').mkdir()
        git = self.root / 'git'
        git.write_text("#!/bin/sh\ncase \"$*\" in *status*) exit 0;; *tree*) printf '%s\\n' \"$CI_COMMIT_TREE_SHA\";; *) printf '%s\\n' \"$CI_COMMIT_SHA\";; esac\n")
        git.chmod(0o755)
        dotnet = self.root / 'dotnet'
        dotnet.write_text("#!/bin/sh\necho 10.0.403\n")
        dotnet.chmod(0o755)
        tool = self.root / '.tools/dotnet-lambda'
        tool.write_text("#!/bin/sh\necho 7.0.0\n")
        tool.chmod(0o755)
        self.env = dict(os.environ, PATH=str(self.root)+os.pathsep+os.environ['PATH'], CI_COMMIT_SHA='a'*40, CI_COMMIT_TREE_SHA='b'*40, CI_PIPELINE_ID='1', CI_JOB_ID='2')
    def run_validate(self, package):
        return subprocess.run([str(ROOT/'tools/deployment/validate-package.sh'), str(package), str(self.root/'manifest.json')], cwd=self.root, env=self.env, capture_output=True, text=True)
    def test_traversal_fails(self):
        package = self.root / 'bad.zip'
        with zipfile.ZipFile(package, 'w') as archive: archive.writestr('../escape', 'fixture')
        self.assertIn('unsafe archive path', self.run_validate(package).stderr)
    def test_missing_dependencies_fail(self):
        package = self.root / 'missing.zip'
        with zipfile.ZipFile(package, 'w') as archive: archive.writestr('MuscleCuties.Server.dll', 'fixture')
        self.assertIn('required root file missing', self.run_validate(package).stderr)
    def test_corrupt_archive_fails(self):
        package = self.root / 'corrupt.zip'
        package.write_bytes(b'not a zip')
        self.assertNotEqual(self.run_validate(package).returncode, 0)

if __name__ == '__main__': unittest.main()
