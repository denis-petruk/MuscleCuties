#!/usr/bin/env bash
# Build, install and launch the MAUI client on a local iOS Simulator (Debug, loopback API only).
#
# Physical-device runs are deprecated for QA (owner, 2026-11-09): acceptance runs on the simulator, launched from
# Rider through the shared run configuration in .run/ or from a terminal. This script never targets a real iPhone.
#
# Usage: bash scripts/run-ios-simulator.sh [simulator name or UDID]   (default: "iPhone 17 Pro")
# Env:   MC_SIM_APPEARANCE=light|dark   MC_SIM_LANGUAGE=en|ru   MC_LOCAL_API=http://127.0.0.1:5080
#        FDC_API_KEY (or repo-root .env) is forwarded to the app by the project build; see docs/FDC_API_SETUP.md.
set -euo pipefail

PROJECT="src/MuscleCuties.UI/MuscleCuties.UI.csproj"
TFM="net10.0-ios"
RID="iossimulator-arm64"
TARGET="${1:-iPhone 17 Pro}"
API="${MC_LOCAL_API:-http://127.0.0.1:5080}"

cd "$(dirname "$0")/.."

if [[ -f .env && -z "${FDC_API_KEY:-}" ]]; then
  FDC_KEY_FROM_ENV="$(grep -E '^[ \t]*FDC_API_KEY[ \t]*=' .env | head -n 1 | sed -E 's/^[ \t]*FDC_API_KEY[ \t]*=[ \t]*["'\'']?([^"'\'']*)["'\'']?/\1/' || true)"
  if [[ -n "$FDC_KEY_FROM_ENV" ]]; then
    export FDC_API_KEY="$FDC_KEY_FROM_ENV"
  fi
fi

case "$API" in
  http://127.0.0.1:*|http://localhost:*|http://\[::1\]:*) ;;
  *) echo "Refusing non-loopback API '$API': simulator QA must never reach Beta or production." >&2; exit 2 ;;
esac

# Resolve the target among simulators only (simctl never lists physical devices).
UDID="$(xcrun simctl list devices available -j | python3 -c '
import json, sys
target = sys.argv[1]
devices = [d for runtime in json.load(sys.stdin)["devices"].values() for d in runtime]
match = [d for d in devices if d["udid"] == target] or [d for d in devices if d["name"] == target]
print(match[0]["udid"] if match else "")
' "$TARGET")"
if [[ -z "$UDID" ]]; then
  echo "No available simulator named or identified as '$TARGET'. Run: xcrun simctl list devices available" >&2
  exit 2
fi

echo "Simulator: $TARGET ($UDID)"
xcrun simctl boot "$UDID" 2>/dev/null || true
open -a Simulator --args -CurrentDeviceUDID "$UDID" || true
xcrun simctl bootstatus "$UDID" -b >/dev/null

if [[ -n "${MC_SIM_APPEARANCE:-}" ]]; then
  xcrun simctl ui "$UDID" appearance "$MC_SIM_APPEARANCE"
fi

if ! curl -s -m 3 -o /dev/null "$API/api/v1/accounts/registration/quiz"; then
  echo "Warning: local API $API is not answering; account journeys will show the offline/error state." >&2
fi

if [[ -n "${MC_SIM_LANGUAGE:-}" ]]; then
  # Simulator-wide language/locale; the app reads them at launch.
  xcrun simctl spawn "$UDID" defaults write -g AppleLanguages -array "$MC_SIM_LANGUAGE"
  xcrun simctl spawn "$UDID" defaults write -g AppleLocale -string "$MC_SIM_LANGUAGE"
fi

# -t:Run builds, installs and launches on the chosen simulator. For breakpoints, run the Rider project
# configuration with this simulator selected instead (see docs/review_owner/list_of_changes.md).
dotnet build "$PROJECT" -c Debug -f "$TFM" -p:RuntimeIdentifier="$RID" \
  -p:_DeviceName=":v2:udid=$UDID" -t:Run
