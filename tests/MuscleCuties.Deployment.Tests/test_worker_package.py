"""The reconciliation worker reads protected environment variables, not appsettings.json."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[2]
REQUIRED = [
    'MuscleCuties.WorkoutReconciliation.dll',
    'MuscleCuties.WorkoutReconciliation.deps.json',
    'MuscleCuties.WorkoutReconciliation.runtimeconfig.json',
]

class WorkerPackageTests(unittest.TestCase):
    def validate(self, omitted=None):
        with tempfile.TemporaryDirectory() as folder:
            archive = Path(folder) / 'worker.zip'
            manifest = Path(folder) / 'manifest.json'
            with zipfile.ZipFile(archive, 'w') as output:
                for name in REQUIRED:
                    if name != omitted:
                        output.writestr(name, '{}')
            env = dict(os.environ, SOURCE_COMMIT_SHA='a' * 40, SOURCE_TREE_SHA='b' * 40)
            result = subprocess.run(['bash', str(ROOT / 'tools/deployment/validate-worker-package.sh'), str(archive), str(manifest)], env=env, capture_output=True, text=True)
            return result, json.loads(manifest.read_text()) if manifest.exists() else None

    def test_environment_configured_worker_needs_no_appsettings_file(self):
        result, manifest = self.validate()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual('WorkoutReconciliation', manifest['component'])

    def test_missing_runtime_files_are_still_rejected(self):
        for required in REQUIRED:
            with self.subTest(required=required):
                result, manifest = self.validate(omitted=required)
                self.assertNotEqual(0, result.returncode)
                self.assertIsNone(manifest)

if __name__ == '__main__':
    unittest.main()
