#!/usr/bin/env bash
set -euo pipefail
[ "$#" -eq 2 ] || { echo 'Usage: validate-package.sh ZIP MANIFEST' >&2; exit 2; }
: "${CI_COMMIT_SHA:?}" "${CI_COMMIT_TREE_SHA:?}" "${CI_PIPELINE_ID:?}" "${CI_JOB_ID:?}"
python3 - "$1" "$2" <<'PY'
import base64, hashlib, json, os, pathlib, re, subprocess, sys, zipfile

def fail(message):
    raise SystemExit('Package validation failed: ' + message)
def command(*args):
    try: return subprocess.check_output(args, stderr=subprocess.DEVNULL, text=True).strip()
    except (subprocess.CalledProcessError, FileNotFoundError): fail('required command failed: ' + args[0])
def sha(data): return hashlib.sha256(data).hexdigest()
commit, tree = os.environ['CI_COMMIT_SHA'], os.environ['CI_COMMIT_TREE_SHA']
if not re.fullmatch(r'[0-9a-f]{40}', commit) or not re.fullmatch(r'[0-9a-f]{40}', tree): fail('invalid source identity')
if command('git','rev-parse','HEAD') != commit or command('git','rev-parse',commit+'^{tree}') != tree: fail('source identity mismatch')
if command('git','status','--porcelain','--untracked-files=no'): fail('tracked source changes present')
sdk = command('dotnet','--version')
if sdk != '10.0.403': fail('unexpected SDK version')
tools = command('./.tools/dotnet-lambda','--version')
if not re.search(r'(?<!\d)7\.0\.0(?!\d)', tools): fail('unexpected Lambda Tools version')
archive = pathlib.Path(sys.argv[1]); data = archive.read_bytes(); digest = hashlib.sha256(data).digest()
files = []
try:
  with zipfile.ZipFile(archive) as z:
    entries = z.infolist(); names = [i.filename for i in entries]
    if len(names) != len(set(names)): fail('duplicate archive entry')
    if sum(i.file_size for i in entries) > 250 * 1024 * 1024: fail('unpacked package exceeds Lambda limit')
    for i in entries:
      n = i.filename; parts = n.rstrip('/').split('/')
      if not n or n.startswith(('/', '\\')) or '\\' in n or any(p in ('', '.', '..') for p in parts) or re.match(r'^[A-Za-z]:', n): fail('unsafe archive path')
      if (i.external_attr >> 16) & 0o170000 == 0o120000: fail('symbolic link in archive')
      if i.is_dir(): continue
      b = z.read(i)
      if re.search(r'(?i)(\.env(?:\.|$)|(^|/)(?:id_rsa|id_ed25519|credentials)(?:\.|$)|\.(?:pfx|p12|key)$)', n): fail('sensitive filename in archive')
      if re.search(rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|AKIA[0-9A-Z]{16}|(?i:password|api[_-]?key|client[_-]?secret)\s*[=:]\s*["\']?[^\s"\'{}$,;]{12,}', b): fail('possible secret in archive')
      if b.startswith(b'\x7fELF') and (len(b) < 20 or b[4] != 2 or b[5] != 1 or int.from_bytes(b[18:20], 'little') != 183): fail('non ARM64 ELF dependency')
      files.append({'path': n, 'bytes': len(b), 'sha256': sha(b)})
    required = ['MuscleCuties.Server.dll','MuscleCuties.Server.deps.json','MuscleCuties.Server.runtimeconfig.json','MuscleCuties.Core.dll','MuscleCuties.UseCases.dll','MuscleCuties.Infrastructure.PostgreSql.dll','Npgsql.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.AspNetCore.Authentication.JwtBearer.dll','Amazon.Lambda.AspNetCoreServer.Hosting.dll','appsettings.json']
    if any(x not in names for x in required): fail('required root file missing')
    if not any(pathlib.PurePosixPath(n).name == 'libSkiaSharp.so' and z.read(n).startswith(b'\x7fELF') for n in names if not n.endswith('/')): fail('ARM64 Skia native asset missing')
    runtime = json.loads(z.read('MuscleCuties.Server.runtimeconfig.json'))['runtimeOptions']
    if runtime.get('tfm') != 'net10.0' or not any(f.get('name') == 'Microsoft.NETCore.App' and str(f.get('version','')).startswith('10.0.') for f in runtime.get('frameworks', [runtime.get('framework', {})])): fail('wrong runtime framework')
    deps = json.loads(z.read('MuscleCuties.Server.deps.json'))
    if not deps.get('targets') or not deps.get('libraries'): fail('invalid dependency manifest')
    if commit.encode() not in z.read('MuscleCuties.Server.dll'): fail('assembly does not contain source identity')
    if b'ReviewedExerciseCatalogV020.json' not in z.read('MuscleCuties.Infrastructure.PostgreSql.dll'): fail('embedded exercise catalog missing')
    cert = os.environ.get('MC_ROOT_CERTIFICATE_FILE')
    if cert and (cert.startswith('/') or cert not in names): fail('configured certificate is absent from package')
except (zipfile.BadZipFile, RuntimeError, KeyError, ValueError, OSError) as e: fail('invalid archive or metadata')
manifest = {'schema_version': 1, 'source_sha': commit, 'tree_sha': tree, 'pipeline_id': os.environ['CI_PIPELINE_ID'], 'job_id': os.environ['CI_JOB_ID'], 'sdk_version': sdk, 'lambda_tools_version': '7.0.0', 'runtime': 'net10.0', 'architecture': 'arm64', 'handler': 'MuscleCuties.Server', 'zip_bytes': len(data), 'zip_sha256': digest.hex(), 'zip_sha256_base64': base64.b64encode(digest).decode(), 'files': files}
requirements = pathlib.Path('src/MuscleCuties.DbMaintenance/Schema/SchemaRequirements.json')
if requirements.is_file(): manifest['requirements_sha256'] = sha(requirements.read_bytes())
pathlib.Path(sys.argv[2]).write_text(json.dumps(manifest, indent=2, sort_keys=True)+'\n')
PY
