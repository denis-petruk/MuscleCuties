# TestFlight build — 2026-11-11

This document is the repository-owned, repeatable build procedure. It contains no signing
certificates, provisioning profiles, passwords, or App Store Connect keys.

## Candidate

- Bundle: `com.musclecuties.app`
- Marketing version: **1.0**
- Build number: **4** (`ApplicationVersion` in `src/MuscleCuties.UI/MuscleCuties.UI.csproj`)
- Target: `net10.0-ios`, `ios-arm64`, Release, `AppTargetEnvironment=Beta`
- Entitlements checked in at `src/MuscleCuties.UI/Platforms/iOS/Entitlements.plist`:
  Sign in with Apple, Associated Domains, keychain access, HealthKit, and health-data access.

The archive script accepts `MC_IOS_VERSION` and `MC_IOS_BUILD`. Use a new positive integer
build number for every upload; build **4** is the checked-in candidate value, not a claim that
App Store Connect has never received it.

## Prerequisites

- macOS with Xcode and the .NET 10 iOS workload (`dotnet workload list`).
- A valid **Apple Distribution** identity in the login keychain.
- An App Store distribution provisioning profile for `com.musclecuties.app` that contains the
  HealthKit entitlement. A profile without HealthKit cannot produce a valid TestFlight build
  for this app even if it signs the binary. The installed `MuscleCuties App Store Connect`
  profile was decoded from the fresh-clone IPA and does contain HealthKit and Sign in with
  Apple; see the reconciliation below.

Inspect the local state without changing it:

```bash
security find-identity -v -p codesigning
ls "$HOME/Library/MobileDevice/Provisioning Profiles"
```

## Build the archive

```bash
cd /path/to/MuscleCuties
MC_IOS_BUILD=3 ./scripts/archive-ios-testflight.sh
```

Override the signing inputs when the local names differ:

```bash
MC_CODESIGN_IDENTITY="Apple Distribution: Your Name (TEAMID)" \
MC_PROVISIONING_PROFILE="Your App Store Profile" \
MC_APPLE_TEAM_ID="TEAMID" \
MC_IOS_VERSION="1.0" MC_IOS_BUILD="3" \
./scripts/archive-ios-testflight.sh
```

The script runs a signed Release build with `ArchiveOnBuild=true` and `BuildIpa=true`, then
prints the signed `.ipa` path (and any `.xcarchive` the target emits) under
`src/MuscleCuties.UI/bin/Release/net10.0-ios/ios-arm64/`.

`scripts/ios/ExportOptions.plist` is the checked-in App Store Connect export configuration
(`method=app-store-connect`, automatic signing, symbols uploaded, build number not managed by
Apple). It contains the team ID only; no certificate or profile is committed.

## Verify before upload

```bash
APP="src/MuscleCuties.UI/bin/Release/net10.0-ios/ios-arm64/MuscleCuties.UI.app"
/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$APP/Info.plist"
/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$APP/Info.plist"
/usr/libexec/PlistBuddy -c 'Print :CFBundleVersion' "$APP/Info.plist"
codesign -d --entitlements :- "$APP"
```

The signed entitlements must include `com.apple.developer.healthkit`. Also confirm the archive
contains `PrivacyInfo.xcprivacy` and that `ITSAppUsesNonExemptEncryption` is `false`.

## Apple-account-only steps

1. In the Apple Developer portal, create or regenerate an **App Store** distribution profile
   for `com.musclecuties.app` with the **HealthKit** capability. Install it on the build Mac.
2. Sign in to Xcode with the Apple ID that owns Team `N23Z94V7G6`, or install an App Store
   Connect API key with the **App Manager** role. Never commit the `.p8` key.
3. Upload the IPA from Xcode Organizer or Transporter (or `xcrun altool`/`notarytool` with the
   API key). Wait for App Store Connect processing.
4. Complete the App Privacy answers using `docs/beta-readiness/APP_PRIVACY_ANSWERS_2026-11-11.md`,
   confirm export compliance (`ITSAppUsesNonExemptEncryption=false`), and assign the build to
   the internal TestFlight group.
5. Install from TestFlight and run the existing device/QA checklist. The simulator result does
   not prove device HealthKit behavior.

The archive step itself requires the first item. Uploading and assigning testers require the
App Store Connect credentials; neither is stored in this repository.


## Verified signed package — 2026-11-11

```bash
MC_IOS_BUILD=3 ./scripts/archive-ios-testflight.sh
```

Observed result:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
Archive outputs:
src/MuscleCuties.UI/bin/Release/net10.0-ios/ios-arm64/publish/MuscleCuties.UI.ipa
src/MuscleCuties.UI/bin/Release/net10.0-ios/ios-arm64/MuscleCuties.UI.ipa
```

The generated bundle reports `CFBundleIdentifier=com.musclecuties.app`,
`CFBundleShortVersionString=1.0`, `CFBundleVersion=3`. This MAUI target emitted the signed IPA
directly; no `.xcarchive` was produced by the `ArchiveOnBuild=true`/`BuildIpa=true` path. The
IPA is the TestFlight upload artifact.

### Fresh-clone signing reconciliation

The same archive command was run from a throwaway clone of
`origin/product/qa-fixes-20261109` with no build artifacts. The IPA's
`Payload/MuscleCuties.UI.app/embedded.mobileprovision` was decoded with `security cms -D` and
compared with `codesign -d --entitlements :-`:

```text
Profile:             MuscleCuties App Store Connect
Profile UUID:        0fdd4b45-bbbe-44c3-8cb4-2c515917ac8d
Profile HealthKit:   true
Profile Sign in:     ["Default"]
App HealthKit:       true
App Sign in:         ["Default"]
```

The profile and binary agree, so HealthKit and Sign in with Apple are backed by the profile
that signed the IPA. The earlier claim that the profile lacked HealthKit was a `plutil` key-path
error, not a signing result. The remaining Apple-account action is authentication for upload:
install an App Store Connect API key or sign in through Xcode, then upload and assign the build
to the internal TestFlight group.
