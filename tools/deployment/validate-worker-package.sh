#!/usr/bin/env bash
set -euo pipefail
[ "$#" -eq 2 ] || { echo 'Usage: validate-worker-package.sh ZIP MANIFEST' >&2; exit 2; }

export SOURCE_COMMIT_SHA="${SOURCE_COMMIT_SHA:-${GITHUB_SHA:-${CI_COMMIT_SHA:-}}}"
export SOURCE_TREE_SHA="${SOURCE_TREE_SHA:-${CI_COMMIT_TREE_SHA:-}}"
[ -n "$SOURCE_COMMIT_SHA" ] || { echo 'validate-worker-package.sh: missing commit SHA' >&2; exit 1; }

python3 - "$1" "$2" <<'PY'
import base64, hashlib, json, os, pathlib, re, subprocess, sys, zipfile

def fail(message):
    raise SystemExit('Worker package validation failed: ' + message)

commit = os.environ['SOURCE_COMMIT_SHA']
tree = os.environ.get('SOURCE_TREE_SHA') or subprocess.check_output(
    ['git', 'rev-parse', commit + '^{tree}'], text=True).strip()
if not re.fullmatch(r'[0-9a-f]{40}', commit) or not re.fullmatch(r'[0-9a-f]{40}', tree):
    fail('invalid source identity')

archive = pathlib.Path(sys.argv[1])
data = archive.read_bytes()
digest = hashlib.sha256(data).digest()
names = []
try:
    with zipfile.ZipFile(archive) as z:
        entries = z.infolist()
        names = [entry.filename for entry in entries]
        if len(names) != len(set(names)):
            fail('duplicate archive entry')
        if sum(entry.file_size for entry in entries) > 250 * 1024 * 1024:
            fail('unpacked package exceeds Lambda limit')
        for entry in entries:
            name = entry.filename
            parts = name.rstrip('/').split('/')
            if not name or name.startswith(('/', '\\')) or '\\' in name or \
               any(part in ('', '.', '..') for part in parts) or re.match(r'^[A-Za-z]:', name):
                fail('unsafe archive path')
            if (entry.external_attr >> 16) & 0o170000 == 0o120000:
                fail('symbolic link in archive')
            if entry.is_dir():
                continue
            content = z.read(entry)
            if re.search(r'(?i)(\.env(?:\.|$)|(^|/)(?:id_rsa|id_ed25519|credentials)(?:\.|$)|\.(?:pfx|p12|key)$)', name):
                fail('sensitive filename in archive')
            if re.search(rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|AKIA[0-9A-Z]{16}', content):
                fail('possible secret in archive')
    required = [
        'MuscleCuties.WorkoutReconciliation.dll',
        'MuscleCuties.WorkoutReconciliation.deps.json',
        'MuscleCuties.WorkoutReconciliation.runtimeconfig.json',
        'appsettings.json',
    ]
    if any(item not in names for item in required):
        fail('required root file missing')
except (zipfile.BadZipFile, OSError, KeyError, ValueError):
    fail('invalid archive or metadata')

manifest = {
    'schema_version': 1,
    'component': 'WorkoutReconciliation',
    'source_sha': commit,
    'tree_sha': tree,
    'zip_bytes': len(data),
    'zip_sha256': digest.hex(),
    'zip_sha256_base64': base64.b64encode(digest).decode(),
}
pathlib.Path(sys.argv[2]).write_text(json.dumps(manifest, indent=2, sort_keys=True) + '\n')
PY
