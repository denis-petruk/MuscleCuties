# Simulator QA — 2026-11-09

| Field | Value |
| --- | --- |
| Binary | Debug `net10.0-ios`, `iossimulator-arm64`, MAUI 10.0.90, built from `integration/beta-rc-20261108` at `7aa53789` (before `bbc27ada`/`438de156`) |
| Device | iPhone 17 Pro simulator `678048F6-C4C7-406C-A970-7104AED3E00A`, iOS 26.3, dark appearance, EN |
| Backend | Local API `http://127.0.0.1:5080` (Development), disposable PostgreSQL 16 `mc_local` on `127.0.0.1:55461`, `schema_version=5`, 746 seed rows, `dbcheck` compatible |
| Account | Synthetic `qa.beta1@example.test`; OTP from the development log delivery; no Beta data used |
| Driver | Automated built-in iOS simulator control (tap/type/screenshot); Maestro not used |

## Journeys

| # | Journey | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Fresh registration → OTP → profile setup → 12-question quiz → first plan → dashboard | PASS with defects E-01, E-03, E-04, E-05 | Lands on Today; Train shows auto-applied plan |
| 2 | Incomplete onboarding: wellness notice unchecked; single weekday | PASS (blocked with inline messages) | "Choose 2 to 7 days…", "Please acknowledge the wellness notice…" (latter rendered off-screen: E-04) |
| 3 | Generated plan inspection | PASS | General strength, Mon + Thu preserved, 45 min, 6 exercises (chest press, chest-supported row, dead bug, leg curl, hack squat, hip thrust), Follicular phase |
| 5 | Start workout, log per-set rows, advance | PASS | DB `app.exercise_set_logs`: 2 observed rows, 20 kg × 8, RIR 3 and 2, `is_observed=true` |
| — | D1 equipment options | PASS | Only Full gym / Dumbbells and bands offered, with Beta disclosure |
| 4, 6–15 | Regeneration, readiness, injury before/during workout, cycle changes, HIIT expiry, promotion, restart, network failures, calendar boundaries, Dynamic Type/VoiceOver | NOT RUN | Remaining device time; covered only by unit/integration tests listed in RELEASE_READINESS |

## Defects found on the simulator

| ID | Sev | Defect | Status |
| --- | --- | --- | --- |
| E-01 | P1 | Untouched birthday picker saved `date_of_birth = today − 18y` and passed the age gate | FIXED `bbc27ada`, needs device re-check |
| E-02 | P1 | Raw key `Workout_Recommendation_StartingWeight` on the session screen | FIXED `438de156`, needs device re-check |
| E-03 | P3 | Username field auto-capitalizes ("Qabeta1") | FIXED `c97254b7`, needs device re-check |
| E-04 | P2 | Onboarding Continue shows its validation message below the fold | OPEN |
| E-05 | P2 | Today shows "Set your cycle phase" while Train shows the answered phase | OPEN |
| E-06 | P2 | Beginner plan card says "Intensity: Heavy" | OPEN (copy/product) |
| E-07 | P3 | Empty-state text flashes during the session page push | OPEN |

No crash occurred in any transition exercised (CR01 path: plan → session → back not stress-tested 20×).

## 2026-11-09 branch refresh — not run in the cloud workspace

Branch `integration/beta-polish-20261109-v2` (base `a99fd80c`) was **not** built or driven here: the workspace is Linux
and has no Xcode/CoreSimulator. CI's `verify_ios_compile` compiled the branch at the published tip `47f045f` (run 42686088233), so the E-04…E-07 fixes build; runtime and visual evidence still needs the owner. The reviewer
summary is [REVIEW_PACKET.md](REVIEW_PACKET.md).
Stage 3 of `scripts/beta-release-wizard.sh` runs the build, walks E-04…E-07 and journeys 4 and 6–15, and writes the
dated record this section should be replaced with. The current dated checklist is
[SIMULATOR_ACCEPTANCE_2026-11-09.md](../release-audit/2026-11-09/SIMULATOR_ACCEPTANCE_2026-11-09.md); every row there is
`NOT RUN` because this workspace has no macOS/Xcode, and the file records the exact `NETSDK1147` blocker.

| ID | Change | Commit | Status |
| --- | --- | --- | --- |
| E-04 | Validation + acknowledgment messages moved to the fixed action footer | `c3f050a` | Build + device check pending |
| E-05 | Today phase copy localized (header, status, title, advice) with the localized phase name | `ced0478` | Data half needs a device run |
| E-06 | Strength days report Moderate, not Heavy; intensity strings localized | `ced0478` | Build + device check pending |
| E-07 | Empty/choice states gated on a completed load attempt | `7718179` | Build + device check pending |

Journeys 4 and 6–15 remain **NOT RUN**; journeys 1, 2, 3 and 5 retain their 2026-11-09 result above at the older head.
CR01 and QA01 remain unrun.
