#!/usr/bin/env bash
set -euo pipefail
[ "$#" -eq 3 ] || { echo 'Usage: upload-artifact.sh ZIP MANIFEST OUTPUT_JSON' >&2; exit 2; }
: "${AWS_RELEASE_BUCKET:?}" "${AWS_REGION:?}"
python3 - "$1" "$2" "$3" <<'PY'
import base64, hashlib, json, os, pathlib, re, subprocess, sys

def fail(message): raise SystemExit('Artifact upload failed: ' + message)
def aws(*args):
    return subprocess.run(['aws','s3api',*args,'--region',os.environ['AWS_REGION'],'--output','json'], capture_output=True, text=True)
def parsed(result):
    try: return json.loads(result.stdout)
    except ValueError: fail('invalid AWS response')
try:
    data = pathlib.Path(sys.argv[1]).read_bytes(); manifest = json.loads(pathlib.Path(sys.argv[2]).read_text())
    digest = hashlib.sha256(data).digest(); hexsha = digest.hex(); b64 = base64.b64encode(digest).decode()
    commit = manifest['source_sha']
    if not re.fullmatch(r'[0-9a-f]{40}', commit) or manifest['zip_sha256'] != hexsha or manifest['zip_sha256_base64'] != b64 or manifest['zip_bytes'] != len(data): fail('manifest does not match ZIP')
except (OSError, ValueError, KeyError): fail('invalid input')
bucket = os.environ['AWS_RELEASE_BUCKET']; key = f'lambda/MuscleCuties.Server/{commit}/{hexsha}/MuscleCuties.Server.zip'
headargs = ['head-object','--bucket',bucket,'--key',key,'--checksum-mode','ENABLED']
head = aws(*headargs)
if head.returncode:
    if not re.search(r'\b(?:404|NoSuchKey|Not Found)\b', head.stderr): fail('cannot inspect existing object')
    put = aws('put-object','--bucket',bucket,'--key',key,'--body',sys.argv[1], '--if-none-match','*','--checksum-algorithm','SHA256','--checksum-sha256',b64,'--metadata',f'source-sha={commit},zip-sha256={hexsha}')
    if put.returncode and not re.search(r'\b(?:412|PreconditionFailed|409|ConditionalRequestConflict)\b', put.stderr): fail('conditional object creation failed')
    head = aws(*headargs)
    if head.returncode: fail('cannot verify uploaded object')
obj = parsed(head); metadata = obj.get('Metadata',{}); version = obj.get('VersionId')
if obj.get('ContentLength') != len(data) or obj.get('ChecksumSHA256') != b64 or metadata.get('source-sha') != commit or metadata.get('zip-sha256') != hexsha or not version or version == 'null': fail('object integrity, metadata, or version mismatch')
# Verify the exact version that will be passed to Lambda, not merely the latest key.
pinned = aws(*headargs, '--version-id',version)
if pinned.returncode: fail('cannot verify object version')
p = parsed(pinned)
if p.get('VersionId') != version or p.get('ContentLength') != len(data) or p.get('ChecksumSHA256') != b64: fail('pinned object mismatch')
out = {'bucket':bucket, 'key':key, 'version_id':version, 'zip_sha256':hexsha, 'zip_sha256_base64':b64, 'zip_bytes':len(data)}
pathlib.Path(sys.argv[3]).write_text(json.dumps(out, indent=2, sort_keys=True)+'\n')
PY
