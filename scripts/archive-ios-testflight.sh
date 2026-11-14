#!/usr/bin/env bash
# Build a signed Release archive for TestFlight. This never uploads.
# The signed entitlements must include HealthKit; see docs/beta-readiness/TESTFLIGHT_BUILD_2026-11-11.md.
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION="${MC_IOS_VERSION:-1.0}"
BUILD="${MC_IOS_BUILD:-4}"
PROFILE="${MC_PROVISIONING_PROFILE:-MuscleCuties App Store Connect}"
IDENTITY="${MC_CODESIGN_IDENTITY:-$(security find-identity -v -p codesigning 2>/dev/null | sed -n 's/.*"\(Apple Distribution[^"]*\)".*/\1/p' | head -1)}"

[[ -n "$IDENTITY" ]] || { echo "No Apple Distribution identity found. Set MC_CODESIGN_IDENTITY." >&2; exit 2; }
[[ -n "$PROFILE" ]] || { echo "Set MC_PROVISIONING_PROFILE to the App Store distribution profile." >&2; exit 2; }
command -v xcodebuild >/dev/null || { echo "xcodebuild is required." >&2; exit 2; }
command -v dotnet >/dev/null || { echo "dotnet is required." >&2; exit 2; }

echo "Archiving MuscleCuties $VERSION ($BUILD)"
echo "  signing configuration detected"

dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj \
  -c Release -f net10.0-ios \
  -p:RuntimeIdentifier=ios-arm64 \
  -p:AppTargetEnvironment=Beta \
  -p:ArchiveOnBuild=true \
  -p:BuildIpa=true \
  -p:ApplicationDisplayVersion="$VERSION" \
  -p:ApplicationVersion="$BUILD" \
  -p:CodesignKey="$IDENTITY" \
  -p:CodesignProvision="$PROFILE" \
  -m:1 -nodeReuse:false

IPA="src/MuscleCuties.UI/bin/Release/net10.0-ios/ios-arm64/MuscleCuties.UI.ipa"
[[ -f "$IPA" ]] || { echo "No IPA produced: $IPA" >&2; exit 1; }
scripts/sanitize-testflight-ipa.sh "$IPA" "$IDENTITY"
scripts/verify-testflight-ipa.sh "$IPA"
echo "Archive outputs:"
find src/MuscleCuties.UI/bin/Release/net10.0-ios/ios-arm64 -maxdepth 2 \
  \( -name '*.xcarchive' -o -name '*.ipa' \) -print 2>/dev/null || true
echo "Export/upload remains an Apple-account step: see docs/beta-readiness/TESTFLIGHT_BUILD_2026-11-11.md"
