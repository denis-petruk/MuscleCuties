# Simulator acceptance — 2026-11-09

**Status: NOT RUN — BLOCKED in this environment.** Nothing in this record is device, simulator, interaction or
screenshot evidence. It is the dated checklist the owner completes on a Mac, with the blocker this workspace hit.

## Why it could not be run here

- Host: `Linux 6.1.102 x86_64` (cloud workspace), not macOS.
- `xcrun`, `simctl` and `xcodebuild`: **not found**.
- .NET SDK `10.0.403`; `dotnet workload list` reports **no installed workloads**.
- The simulator build was attempted exactly as the release procedure specifies:

  ```
  dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug \
    -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
  ```

  Exit `1`, with:

  ```
  error NETSDK1147: To build this project, the following workloads must be installed: maui-tizen wasm-tools
  .../MuscleCuties.UI.csproj::TargetFramework=net10.0-ios
  error NETSDK1147: To install these workloads, run the following command: dotnet workload restore
  ```

  Installing workloads would not make this a simulator run: iOS targets need Apple's toolchain and CoreSimulator,
  which this Linux host does not have.

## Compile evidence that does exist (not acceptance)

- CI run [42676922976](https://github.com/denis-petruk/MuscleCuties/actions/runs/42676922976) on branch
  `integration/beta-polish-20261109-v2` at tip `91886e266796650a5152336602b16d44c9cefc0d` **completed successfully**;
  the commit page reports **10 successful, 0 failing, 0 pending**, including `verify_ios_compile` on `xcode-27`.
- Compilation proves the XAML and C# build. It does not prove install, launch, navigation, rendering, localization,
  contrast, Dynamic Type, or any journey below.

## Owner procedure (Mac)

1. `dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64`
2. Boot the smallest supported iPhone and an iPhone 17 Pro; `xcrun simctl list devices booted` must show both UDIDs.
3. `xcrun simctl install booted src/MuscleCuties.UI/bin/Debug/net10.0-ios/iossimulator-arm64/MuscleCuties.UI.app`
4. Launch the app (Simulator, Rider or Visual Studio) and walk the table below.
5. Capture evidence with `xcrun simctl io booted screenshot <path>`; flip appearance with
   `xcrun simctl ui booted appearance dark`.
6. Run the matrix twice: light and dark, English and Russian; at least one pass at the largest supported Dynamic Type.
7. Replace every `NOT RUN` with `PASS` or `FAIL` plus a one-line observation, fill the header below, and commit this
   record. Then replace the `G12 Simulator` row in
   [RELEASE_READINESS.md](../../beta-readiness/RELEASE_READINESS.md) with this record's result.
   `bash scripts/beta-release-wizard.sh` stage 3 performs steps 1–5 and writes an owner record in this directory.

## Run header (owner fills)

| Field | Value |
| --- | --- |
| Branch / tip SHA | `integration/beta-polish-20261109-v2` / `91886e2` at record creation |
| Device models | |
| iOS versions | |
| Appearances | Light / Dark |
| Languages | English / Russian |
| Largest Dynamic Type pass | |
| Tester / date | |
| Screenshot folder | |

## Checks

| ID | Check | Result |
| --- | --- | --- |
| E-04 | Invalid onboarding form shows the reason directly above Continue | NOT RUN |
| E-05 | Today's phase header matches the phase the quiz/Train tab shows | NOT RUN |
| E-06 | A beginner general-strength day no longer reports "Heavy" | NOT RUN |
| E-07 | Opening a session does not flash the no-activities state | NOT RUN |
| J01 | Existing journey 1 (see [SIMULATOR_QA.md](../../beta-readiness/SIMULATOR_QA.md)) | NOT RUN |
| J02 | Existing journey 2 (see SIMULATOR_QA.md) | NOT RUN |
| J03 | Existing journey 3 (see SIMULATOR_QA.md) | NOT RUN |
| J04 | Injury restrictions and substitutions | NOT RUN |
| J05 | Existing journey 5 (see SIMULATOR_QA.md) | NOT RUN |
| J06 | Plan preview and Apply | NOT RUN |
| J07 | Schedule and reschedule | NOT RUN |
| J08 | Session logging and completion | NOT RUN |
| J09 | History | NOT RUN |
| J10 | Cycle | NOT RUN |
| J11 | Nutrition | NOT RUN |
| J12 | Body measurements | NOT RUN |
| J13 | Offline and retry | NOT RUN |
| J14 | Russian at large text | NOT RUN |
| J15 | First-run install | NOT RUN |
| CR01 | Plan → session → back transition, 20 repetitions | NOT RUN |
| QA01 | Physical-device acceptance on an iPhone | NOT RUN |

## What this record changes

- It does **not** upgrade `G12 Simulator`: the gate stays NOT RUN/BLOCKED.
- It **does** make the blocker and the owner's exact path explicit, so the readiness verdict no longer rests on an
  implied simulator run. The release remains **NO-GO** until this record contains real PASS results.
