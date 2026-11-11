#!/usr/bin/env bash
# Replace build-machine paths in an IPA's app bundle, then re-sign and repackage it.
set -euo pipefail
cd "$(dirname "$0")/.."

IPA="${1:-}"
IDENTITY="${2:-${MC_CODESIGN_IDENTITY:-$(security find-identity -v -p codesigning 2>/dev/null | sed -n 's/.*"\(Apple Distribution[^"]*\)".*/\1/p' | head -1)}}"
[[ -n "$IPA" && -f "$IPA" ]] || { echo "IPA not found. Pass the path as the first argument." >&2; exit 2; }
[[ -n "$IDENTITY" ]] || { echo "No Apple Distribution identity found. Set MC_CODESIGN_IDENTITY." >&2; exit 2; }
command -v codesign >/dev/null || { echo "codesign is required." >&2; exit 2; }
command -v zip >/dev/null || { echo "zip is required." >&2; exit 2; }

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
unzip -q "$IPA" -d "$TMP"
APP="$(find "$TMP/Payload" -maxdepth 1 -name '*.app' -type d -print -quit)"
[[ -n "$APP" ]] || { echo "No app bundle in IPA." >&2; exit 1; }

ENT="$TMP/entitlements.plist"
codesign -d --entitlements :- "$APP" > "$ENT" 2>/dev/null
python3 - "$ENT" <<'PY'
import sys
p = sys.argv[1]
data = open(p, 'rb').read()
i = data.find(b'<?xml')
if i < 0:
    raise SystemExit('could not extract app entitlements')
open(p, 'wb').write(data[i:])
PY

python3 scripts/sanitize-ipa-paths.py "$APP"
for fw in "$APP"/Frameworks/*.framework; do
  if [[ -d "$fw" ]]; then
    codesign --force --timestamp --sign "$IDENTITY" "$fw"
  fi
done
codesign --force --timestamp --sign "$IDENTITY" --entitlements "$ENT" "$APP"
codesign --verify --deep --strict "$APP"

mkdir "$TMP/out"
for entry in "$TMP"/*; do
  name="$(basename "$entry")"
  case "$name" in
    out|entitlements.plist) continue ;;
  esac
  cp -R "$entry" "$TMP/out/"
done
OUT="$TMP/clean.ipa"
( cd "$TMP/out" && zip -qry -X "$OUT" . )
mv "$OUT" "$IPA"
echo "Re-signed and repackaged IPA."
