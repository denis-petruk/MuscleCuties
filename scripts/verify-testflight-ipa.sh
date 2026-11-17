#!/usr/bin/env bash
# Audit a signed TestFlight IPA without printing secret values or identifiers.
set -euo pipefail
cd "$(dirname "$0")/.."

IPA="${1:-}"
if [[ -z "$IPA" ]]; then
  IPA="$(find src/MuscleCuties.UI/bin/Release/net10.0-ios/ios-arm64 -maxdepth 1 -name '*.ipa' -print -quit 2>/dev/null || true)"
fi
[[ -n "$IPA" && -f "$IPA" ]] || { echo "IPA not found. Pass the path as the first argument." >&2; exit 2; }
command -v python3 >/dev/null || { echo "python3 is required." >&2; exit 2; }

echo "Auditing IPA: $IPA"
python3 - "$IPA" <<'PY'
import plistlib
import re
import sys
import zipfile

ipa = sys.argv[1]
z = zipfile.ZipFile(ipa)
names = z.namelist()
failures = []

info_names = [n for n in names if re.match(r'^Payload/[^/]+\.app/Info\.plist$', n)]
if len(info_names) != 1:
    failures.append('expected exactly one top-level app Info.plist')
    app = None
else:
    info_name = info_names[0]
    app = info_name[: -len('/Info.plist')]
    info = plistlib.loads(z.read(info_name))
    print(f"bundle_id={info.get('CFBundleIdentifier')} version={info.get('CFBundleShortVersionString')} build={info.get('CFBundleVersion')}")
    if info.get('get-task-allow') is True:
        failures.append('Info.plist enables get-task-allow')
    if not info.get('NSHealthShareUsageDescription'):
        failures.append('Info.plist missing NSHealthShareUsageDescription')
    if not info.get('NSHealthUpdateUsageDescription'):
        failures.append('Info.plist missing NSHealthUpdateUsageDescription')

for n in names:
    if n.endswith('.plist'):
        try:
            plistlib.loads(z.read(n))
        except Exception as e:
            failures.append(f'corrupted plist {n}: {e}')

expected_profile = f'{app}/embedded.mobileprovision' if app else None
profiles = [n for n in names if n.endswith('/embedded.mobileprovision')]
if expected_profile is None or profiles != [expected_profile]:
    failures.append('expected exactly one embedded.mobileprovision for the app')

forbidden_suffixes = (
    '.pdb', '.dsym', '.cs', '.csproj', '.env', '.p12', '.p8', '.key',
    '.cer', '.pem', '.sqlite', '.db', '.log', '.trx',
)
bad_names = []
for n in names:
    low = n.lower()
    if low.endswith(forbidden_suffixes) or re.search(r'(^|/)appsettings\.[^/]*\.json$', n, re.I):
        bad_names.append(n)
    if n.endswith('/embedded.mobileprovision') and n != expected_profile:
        bad_names.append(n)

path_patterns = [b'/Users/', b'/private/tmp/', b'/var/folders/', b'/home/', b'C:\\Users\\']
secret_patterns = [
    b'FDC_API_KEY', b'BEGIN PRIVATE KEY', b'BEGIN RSA PRIVATE KEY',
    b'BEGIN EC PRIVATE KEY', b'AKIA', b'ASIA', b'postgres://', b'postgresql://',
]
path_hits = 0
secret_hits = 0
path_files = []
secret_files = []
for n in names:
    if n.endswith('/'):
        continue
    try:
        data = z.read(n)
    except Exception:
        continue
    pc = sum(data.count(p) for p in path_patterns)
    sc = sum(data.count(p) for p in secret_patterns)
    if pc:
        path_hits += pc
        path_files.append(n)
    if sc:
        secret_hits += sc
        secret_files.append(n)

print(f"files={len(names)} debug_or_source_names={len(bad_names)} local_path_markers={path_hits} secret_markers={secret_hits}")
if bad_names:
    print('offending_names=' + ', '.join(sorted(set(bad_names))[:20]))
if path_files:
    print('path_marker_files=' + ', '.join(sorted(set(path_files))[:20]))
if secret_files:
    print('secret_marker_files=' + ', '.join(sorted(set(secret_files))[:20]))
if bad_names or path_hits or secret_hits:
    failures.append('IPA contains debug/source names, local paths, or secret markers')
if failures:
    for f in failures:
        print('FAIL: ' + f, file=sys.stderr)
    sys.exit(1)
print('content_audit=PASS')
PY

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
unzip -q "$IPA" -d "$TMP"
APP="$(find "$TMP/Payload" -maxdepth 1 -name '*.app' -type d -print -quit)"
[[ -n "$APP" ]] || { echo "No app bundle in IPA." >&2; exit 1; }
security cms -D -i "$APP/embedded.mobileprovision" > "$TMP/profile.plist" 2>/dev/null || {
  echo "Could not decode embedded.mobileprovision." >&2; exit 1;
}
codesign -d --entitlements :- "$APP" > "$TMP/app-entitlements.plist" 2>/dev/null || {
  echo "Could not read the app's signed entitlements." >&2; exit 1;
}

python3 - "$TMP/profile.plist" "$TMP/app-entitlements.plist" <<'PY'
import plistlib
import sys

profile = plistlib.load(open(sys.argv[1], 'rb'))
pe = profile.get('Entitlements', {})
raw = open(sys.argv[2], 'rb').read()
start = raw.find(b'<?xml')
if start > 0:
    raw = raw[start:]
ae = plistlib.loads(raw)

def present(d, key):
    return bool(d.get(key))

checks = {
    'healthkit_profile': present(pe, 'com.apple.developer.healthkit'),
    'healthkit_app': present(ae, 'com.apple.developer.healthkit'),
    'signin_profile': pe.get('com.apple.developer.applesignin') == ['Default'],
    'signin_app': ae.get('com.apple.developer.applesignin') == ['Default'],
    'beta_reports_profile': present(pe, 'beta-reports-active'),
    'get_task_allow_profile': present(pe, 'get-task-allow'),
    'get_task_allow_app': present(ae, 'get-task-allow'),
}
for key in ('healthkit_profile', 'healthkit_app', 'signin_profile', 'signin_app', 'beta_reports_profile'):
    print(f'{key}={checks[key]}')
print(f"get_task_allow_profile={checks['get_task_allow_profile']}")
print(f"get_task_allow_app={checks['get_task_allow_app']}")
if not (checks['healthkit_profile'] and checks['healthkit_app'] and checks['signin_profile'] and checks['signin_app']):
    sys.exit('Profile and app entitlements do not match for HealthKit and Sign in with Apple.')
if not checks['beta_reports_profile']:
    sys.exit('Profile is not an App Store beta-reporting profile.')
if checks['get_task_allow_profile'] or checks['get_task_allow_app']:
    sys.exit('Distribution profile or app has get-task-allow enabled.')
PY

echo "IPA cleanliness: PASS"
