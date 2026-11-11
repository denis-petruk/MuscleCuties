# TestFlight IPA cleanliness

Date: 2026-11-11

## Audit result

The signed fresh-clone IPA was inspected by file name and by scanning every payload
file for local build paths and secret markers. It contained no API keys, database
URLs, private keys, certificates, or loose `.pdb` files. It did contain build
metadata that must not ship to testers:

- 167 local absolute-path strings in the native `MuscleCuties.UI` executable;
- source-path metadata in managed assemblies.

Release builds now remove managed debug symbols, which eliminated the managed
`/private/tmp` paths. A residual 179 path markers remained: 167 in the native
image and one in each of 12 third-party assemblies. These are build metadata, not
credentials.

## Fix

Release builds set `DebugType=None` and `DebugSymbols=false` in
`Directory.Build.props`. The archive pipeline then:

1. replaces residual build-machine path tokens in the extracted app bundle in
   place, preserving file lengths;
2. re-signs the app with its existing entitlements;
3. re-packages the IPA with a clean zip (no `__MACOSX` entries);
4. runs `scripts/verify-testflight-ipa.sh`.

The verifier fails on debug/source files, duplicate embedded profiles, local
absolute paths, API-key/private-key/cloud-key/database-URL markers, a non-beta
profile, `get-task-allow`, or mismatched HealthKit / Sign in with Apple
entitlements. It prints only counts and filenames, never matched values, team IDs,
or profile UUIDs.

## Final observed result

The canonical `MuscleCuties.UI.ipa` (version 1.0, build 3) verified as:

- 396 files, 0 `__MACOSX` entries;
- 0 local path markers and 0 secret markers;
- 0 debug/source file names;
- profile and app both have HealthKit and Sign in with Apple;
- `beta-reports-active` present, `get-task-allow` absent;
- `codesign --verify --deep --strict` passed after re-signing.

The IPA was not installed or launched; export and upload remain Apple-account-only
steps described in `TESTFLIGHT_BUILD_2026-11-11.md`. The IPA is a build output and
is not committed.

## Reproduce

```sh
MC_IOS_BUILD=3 ./scripts/archive-ios-testflight.sh
scripts/verify-testflight-ipa.sh
```

The archive script sanitizes and verifies automatically. A clean artifact reports
`content_audit=PASS` and `IPA cleanliness: PASS`.
