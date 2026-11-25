# Building the .ipa for Transporter

MuscleCuties ships to TestFlight from GitHub Actions. A push to `main` runs `ci.yml`, and once
every required check passes it calls `mobile-release.yml`, which builds the signed `.ipa` and
uploads it with `altool`; the same run calls `backend-release.yml` to deploy the API to Lambda.
Sections 1–5 describe a manual build on a Mac, section 6 describes the automated path and the
one-time secrets it needs.

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

   A profile is bound to one distribution certificate. When that certificate is revoked the
   profile shows **Invalid** in the portal and every upload is rejected with
   `Certificate Revoked … used to sign …`, so recreate the profile against the current
   certificate before building. `MuscleCuties App Store Connect v2` is such a profile (created
   against the certificate that expires 2027-11-25).

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

`-p:BuildIpa=true` is required: the project file sets `BuildIpa` to `false` for **Debug** only, and
Release leaves it unset.

`-p:AppTargetEnvironment=beta` is required, not cosmetic: in Release
`AppEndpointConfiguration.ActiveEnvironment` throws unless `APP_ENV_BETA` is defined, and
`MuscleCuties.UI.csproj` defines `APP_ENV_BETA` only when `AppTargetEnvironment` is `beta`. Dropping
the property still compiles and still produces an `.ipa`, but that build throws as soon as it
resolves its backend environment, so it cannot be shipped.

`CodesignProvision` selects the profile; the signing identity is taken from the keychain.

If the keychain holds more than one Apple Distribution identity with the same name — for example an
older certificate plus a freshly created one — detection fails with
`No iOS signing identities match the specified provisioning profile` even though the profile's own
certificate is installed and signs fine. In that case name the identity explicitly with
`-p:CodesignKey=<SHA-1 of the certificate the profile embeds>`; `security find-identity -v -p codesigning`
lists the SHA-1s, and the newest certificate is not necessarily the one the profile carries.

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

### Automated upload from the command line

`altool` can do the upload without Transporter's UI, but only with a **team** API key and all
three values: key ID, Issuer ID and the `.p8`. The key file must sit in `private_keys`,
`.private_keys`, `.appstoreconnect/private_keys` (or the directory named by `API_PRIVATE_KEYS_DIR`):

```bash
API_PRIVATE_KEYS_DIR="$HOME/.appstoreconnect/private_keys" \
xcrun altool --upload-app -f "$ipa" -t ios \
  --apiKey "$ASC_KEY_ID" --apiIssuer "$ASC_ISSUER_ID"
```

The Issuer ID is the UUID shown on App Store Connect → Users and Access → Integrations; it is
per team and is not stored in the key file. Without it `altool` refuses to start
(`Either JWT (--api-issuer and --api-key) or username and app password … is required`), and
`iTMSTransporter` says `The -apiKey option also requires the -apiIssuer option`.

Individual API keys do not carry an Issuer ID at all — their JWT uses `sub: user` — and Apple's
upload tools do not accept them (`401 NOT_AUTHORIZED`); a build upload needs a team key.
The username path (`--username`, `--app-password`, `--provider-public-id`) works too, with an
app-specific password instead of a key.

An individually generated token can be checked against the API before uploading:

```bash
curl -H "Authorization: Bearer $TOKEN" "https://api.appstoreconnect.apple.com/v1/apps?limit=1"
```

Bump `<ApplicationVersion>` (currently `6`) in `src/MuscleCuties.UI/MuscleCuties.UI.csproj` for every
upload — App Store Connect rejects a build number it already holds for version `1.0`. Then open
Transporter, sign in, drag the `.ipa` from section 3 in and press **Deliver**.

## 6. The automated release

`ci.yml` adds two jobs that declare `needs: required` — the aggregate of every test, build and
database check — and run only for a push to `refs/heads/main`:

| Job | Workflow | Effect |
|---|---|---|
| `release_backend_beta` | `backend-release.yml` | packages both Lambdas, uploads them to the immutable bucket, cuts the `beta` alias, then proves the served digest and the `/health/live` and `/health/ready` endpoints |
| `release_ios_testflight` | `mobile-release.yml` | builds the signed `.ipa` for `ios-arm64` and uploads it with `altool` |

`tools/deployment/deploy_release.py` re-checks the trigger itself: `workflow_dispatch`, or a
`push` to `refs/heads/main`. A pull request or a side-branch push is refused before the first
mutation, so the automatic path cannot be reached from unreviewed code.

### Repository secrets the iOS job needs

| Secret | Value |
|---|---|
| `IOS_DISTRIBUTION_CERTIFICATE_P12` | base64 of the exported *Apple Distribution* identity (`.p12`) |
| `IOS_DISTRIBUTION_CERTIFICATE_PASSWORD` | the password chosen when exporting that `.p12` |
| `IOS_PROVISIONING_PROFILE` | base64 of the App Store profile for `com.musclecuties.app` |
| `ASC_API_KEY_ID` | the App Store Connect **team** key's Key ID |
| `ASC_API_ISSUER_ID` | the team's Issuer ID (Users and Access → Integrations) |
| `ASC_API_PRIVATE_KEY` | base64 of that key's `AuthKey_<KEY_ID>.p8` |

Setting them from this Mac (Keychain Access → *My Certificates* → right-click → *Export* does the
same job as `security export` and lets you pick the single Apple Distribution identity):

```bash
security export -t identities -f pkcs12 -k ~/Library/Keychains/login.keychain-db \
  -o /tmp/distribution.p12                     # choose a password when prompted
base64 -i /tmp/distribution.p12 | gh secret set IOS_DISTRIBUTION_CERTIFICATE_P12
gh secret set IOS_DISTRIBUTION_CERTIFICATE_PASSWORD
base64 -i "$HOME/Library/MobileDevice/Provisioning Profiles/<uuid>.mobileprovision" \
  | gh secret set IOS_PROVISIONING_PROFILE
base64 -i ~/.appstoreconnect/private_keys/AuthKey_<KEY_ID>.p8 | gh secret set ASC_API_PRIVATE_KEY
gh secret set ASC_API_KEY_ID
gh secret set ASC_API_ISSUER_ID
rm /tmp/distribution.p12
```

The job signs with the profile's UUID, so it does not depend on the profile name, and it imports the
identity into a throwaway keychain that is the only keychain in the search list. It sets the build
number to `YYYYMMDD.HHMMSS` UTC via `-p:ApplicationVersion`, so every upload is unique without
editing the project file; bump `<ApplicationVersion>` only for a manual upload. The identity,
profile, key and `.p8` are deleted in an `always()` step.

The backend half reuses the existing repository secrets and variables (`AWS_ARTIFACT_UPLOAD_ROLE_ARN`,
`AWS_BETA_DEPLOY_ROLE_ARN`, `BETA_SERVER_FUNCTION`, `BETA_SERVER_ALIAS`, `BETA_API_ENDPOINT`). The
`beta` environment has no required reviewers, so the run is unattended.

## Verified vs unverified

Verified on 2026-11-19 (macOS, Xcode 26.3, .NET SDK 10.0.203, Microsoft.iOS.Sdk 26.2):

- The command in section 2 was executed in **Release**, on a clean `bin/Release` tree, with
  `-p:CodesignKey=-` added in place of `-p:CodesignProvision` because no Apple identity was
  available (ad-hoc signing). It exited **0** and wrote
  `src/MuscleCuties.UI/bin/Release/net10.0-ios/ios-arm64/publish/MuscleCuties.UI.ipa`,
  **39,764,474 bytes**, containing `Payload/MuscleCuties.UI.app/` for bundle `com.musclecuties.app`.
- `codesign` reports `Signature=adhoc` and `TeamIdentifier=not set` for that artifact. That is the
  expected result without an Apple identity; it is not a distribution signature.
- `dotnet publish` writes the `.ipa` into a `publish/` subdirectory. `dotnet build` does not — it
  writes to `.../ios-arm64/MuscleCuties.UI.ipa`, one directory up.
- Release publishes the AOT build and is slow: it took about fifteen minutes here. That is expected,
  not a hang. `-p:RunAOTCompilation=false` evaluates to `false` but the iOS SDK still runs the AOT
  compiler, so there is no faster Release path.
- Dropping `-p:AppTargetEnvironment=beta` leaves `APP_ENV_BETA` undefined in Release
  (`dotnet msbuild … -getProperty:DefineConstants` shows it absent), which is exactly what the
  runtime guard in `AppEndpointConfiguration.ActiveEnvironment` rejects.
- MSBuild resolves `BuildIpa=true` in Release when it is passed, and no Release path sets it false
  (`dotnet msbuild … -getProperty:BuildIpa` → `true` for Release, `false` for Debug).
  `EnableCodeSigning` stays `true` in Release, so signing is never silently dropped.
- No `Directory.Build.props`/`Directory.Build.targets` in this repository touches `BuildIpa`,
  `CodesignKey`, `CodesignProvision` or `EnableCodeSigning`.

Not verified, because it needs the Apple identity: distribution signing, and the Transporter upload
itself. The automated path in section 6 has not run yet either — it needs the six repository
secrets above, so its first execution is the first push to `main` afterwards.
