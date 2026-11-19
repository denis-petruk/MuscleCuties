# Building the .ipa for Transporter

MuscleCuties ships to TestFlight by hand: build a signed `.ipa` on a Mac with Xcode, then hand
that single file to Transporter. CI does not build it — without an Apple Distribution identity on
the runner MAUI can only produce an unsigned `.ipa`, which Transporter rejects. The
`mobile-release.yml` workflow therefore verifies compilation only.

## 1. One-time Xcode setup

Needs the Apple Developer team that owns `com.musclecuties.app`.

1. **Apple Distribution certificate.** Xcode → Settings → Accounts → add the Apple ID → select the
   team → Manage Certificates… → **+** → *Apple Distribution*. It must show up in the login
   keychain.
2. **App Store provisioning profile.** In the developer portal create an *App Store* distribution
   profile for App ID `com.musclecuties.app`. The App ID must have every capability that
   `Platforms/iOS/Entitlements.plist` asks for:
   - HealthKit
   - Sign in with Apple
   - Associated Domains
   - Keychain Sharing

   Download the profile and double-click it to install. Remember the profile's **name** — it is
   passed as `CodesignProvision` below. Without it the build only warns (`MT7139`) and the result
   cannot be distributed.

## 2. The command

Run from the repository root:

```bash
dotnet publish src/MuscleCuties.UI/MuscleCuties.UI.csproj \
  -f net10.0-ios \
  -c Release \
  -p:RuntimeIdentifier=ios-arm64 \
  -p:AppTargetEnvironment=beta \
  -p:BuildIpa=true \
  -p:CodesignProvision="<your App Store profile name>"
```

`-p:BuildIpa=true` is required: the project file sets `BuildIpa` to `false` for **Debug** only,
and Release leaves it unset. `CodesignProvision` selects the profile; the signing identity is taken
from the keychain.

## 3. Output — exact path and file name

```
src/MuscleCuties.UI/bin/Release/net10.0-ios/ios-arm64/publish/MuscleCuties.UI.ipa
```

Note the `publish/` segment: `dotnet publish` writes there, while `dotnet build` writes the `.ipa`
one level up, at `.../ios-arm64/MuscleCuties.UI.ipa`. The file name comes from
`<AssemblyName>MuscleCuties.UI</AssemblyName>` in the project file.

## 4. Check it before uploading

```bash
ipa=src/MuscleCuties.UI/bin/Release/net10.0-ios/ios-arm64/publish/MuscleCuties.UI.ipa
unzip -l "$ipa" | head                      # Payload/MuscleCuties.UI.app/…
rm -rf /tmp/ipa && unzip -q "$ipa" -d /tmp/ipa
codesign -dv --entitlements :- /tmp/ipa/Payload/MuscleCuties.UI.app
```

The signature must name your Apple Distribution identity, and the entitlements must list HealthKit,
Sign in with Apple, Associated Domains and keychain-access-groups.

## 5. Upload with Transporter

Bump `<ApplicationVersion>` (currently `6`) in `MuscleCuties.UI.csproj` for every upload — App Store
Connect rejects a build number it already holds for version `1.0`. Then open Transporter, sign in,
drag the `.ipa` in and press **Deliver**.

## Verified vs unverified

Verified on 2026-11-19 (macOS, Xcode 26.3, .NET SDK 10.0.203):

- `dotnet publish … -p:BuildIpa=true` wrote
  `bin/Debug/net10.0-ios/ios-arm64/publish/MuscleCuties.UI.ipa` (56,011,776 bytes) containing
  `Payload/MuscleCuties.UI.app/`. Release uses the same layout with `Release` in place of `Debug`.
- MSBuild resolves `BuildIpa=true` for Release when it is passed, and no Release path sets it false
  (`dotnet msbuild … -getProperty:BuildIpa` → `true` for Release, `false` for Debug).
- No `Directory.Build.props`/`Directory.Build.targets` in this repository touches `BuildIpa`,
  `CodesignKey`, `CodesignProvision` or `EnableCodeSigning`.

Not verified, because it needs the Apple identity: the Release/AOT publish itself, distribution
signing, and the Transporter upload. The run above used `-p:CodesignKey=-` (ad-hoc) so it could
complete without a distribution certificate.
