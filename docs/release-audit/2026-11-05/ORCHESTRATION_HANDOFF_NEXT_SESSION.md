# MuscleCuties — Orchestrator Handoff & Next Session Execution Brief

**Date:** November 5, 2026  
**Project:** MuscleCuties iOS / MAUI (.NET 10) & AWS Serverless Backend  
**Authoritative Context:** **100% MuscleCuties.** (No Notion or third-party life-os context applies).  
**Baseline Candidate:** `continue_musclecuties_beta_plan` at `6e38535756494ab3e40a75cc74109ad247727213`  
**Rider Worktree / Checkout:** `/Users/denispetruk/RiderProjects/MuscleCuties` on `main` at `d840d62745337a604f56f4ba32568fb66ae53158`  
**Candidate Worktree:** `/Users/denispetruk/.gemini/antigravity/worktrees/MuscleCuties/continue_musclecuties_beta_plan`

---

## 1. Orchestrator Prompt Audit & Fulfillment Status

Across `docs/`, multiple orchestrator prompts and checkpoints were audited:

| Document / Prompt | Scope | Fulfillment Status | Notes / Evidence |
| :--- | :--- | :---: | :--- |
| `docs/release-audit/2026-11-05/ORCHESTRATOR_PROMPT.md` | **P0:** Diagnose Rider Debug data load failure on iPhone simulator. | **FULFILLED** | **Root cause identified:** macOS Sonoma/Sequoia AirPlay receiver (`ControlCenter` / `AirTunes`, PID 637) occupies port 5000, returning `HTTP 403 Forbidden`.<br>**Fix applied:** `AppEndpointConfiguration.LocalBaseUrl` set to `http://127.0.0.1:5080` (free port). Unit tests pass (58/58).<br>**Remote AWS tested:** iPhone 16 simulator preference pointed to AWS API Gateway (`https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com`). App booted cleanly to login screen (`simulator_remote_aws.png`). Latency measured at 124–165 ms warm, RDS PostgreSQL healthy. |
| `docs/release-audit/2026-11-04/ORCHESTRATOR_PROMPT.md` | Full corrective Beta plan (BA00, WP00–WP05, UX01–UX03, RM00–RM08, PL01–PL05, QA00–QA01, TF00–TF02). | **PARTIALLY FULFILLED** | **Completed:** BA00, WP00, PD00, DS00, PL01, WP01, WP02a, WP03a, WP03b, WP04, WP05, UX01a, UX01b, UX02, UX03, RM00, RM01, RM02, RM07, PL02, QA00 (all 1,346 automated tests passing).<br>**Pending:** Candidate fast-forward into Rider `main`, QA01 in-app journey execution, PL03/PL04 isolated staging, TF00–TF02 TestFlight. |
| `docs/architecture/github-transition/08-execution-orchestrator-prompt.md` | Full roadmap execution (Plan I P00–P13, Plan II R01–R07). | **MERGED & INTEGRATED** | Historical roadmap transition completed and merged into `origin/main` (PRs #15–#24). Current focus is purely the corrective Beta stabilization. |
| `docs/design/orchestration/01-ui-ux-remediation-orchestrator.md` | UI/UX remediation sublane (A.UI). | **INTEGRATED INTO CANDIDATE** | Handled in candidate packages UX01–UX03 and RM00–RM07. |
| `docs/infra/checkpoints/postgres-schema-orchestrator.md` | PostgreSQL schema lifecycle and versioned migration. | **INTEGRATED (PL02)** | Sequential migrations `001_base_schema.sql` -> `002_schema_fingerprint.sql` -> `003_schedule_seam.sql` validated with advisory locks in `db-release.yml`. |

---

## 2. Active State & Artifact Inventory

1. **Booted Simulator:**
   - Device: `iPhone 16` (`60DD8563-A1D6-4FE5-9F4F-2BFB975B194B`) running iOS 18.2.
   - Bundle ID: `com.musclecuties.app`.
   - Current Effective Preference:
     ```bash
     xcrun simctl spawn 60DD8563-A1D6-4FE5-9F4F-2BFB975B194B defaults read com.musclecuties.app backend_api_base_url
     # Output: https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com
     ```
   - Current Screen: Login Screen ("Welcome back").
   - Screenshot Evidence: [`simulator_remote_aws.png`](file:///Users/denispetruk/.gemini/antigravity/brain/1d8cb6e5-10fe-4ee0-b9b6-e58e28611f62/simulator_remote_aws.png).
2. **AWS Backend Performance (us-east-2):**
   - API Gateway: `https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com`
   - `/health/live`: HTTP 200 (~124 ms warm)
   - `/health/ready`: HTTP 200 (~155 ms warm, RDS PostgreSQL healthy)
   - `/api/v1/accounts/registration/quiz`: HTTP 200 (5,923 bytes JSON, ~128 ms)
   - `/api/v1/session`: HTTP 401 (~118 ms)
3. **Local Port Change:**
   - `AppEndpointConfiguration.LocalBaseUrl` set to `http://127.0.0.1:5080`.
   - Unit tests updated and passing: `tests/MuscleCuties.Mobile.Contracts.Tests/AppEndpointConfigurationTests.cs` (58/58 green).
   - Synced to `/Users/denispetruk/RiderProjects/MuscleCuties`.
4. **Candidate Codebase:**
   - Worktree: `/Users/denispetruk/.gemini/antigravity/worktrees/MuscleCuties/continue_musclecuties_beta_plan`
   - Head: `6e385357` (38 commits ahead of Rider `main` at `d840d627`).

---

## 3. Pending Jobs Queue & Immediate Next Actions

### **Job 1: In-App Journey Execution on Simulator (QA01 Part A)**
* **Objective:** Test end-to-end data loading inside the app on the booted iPhone simulator against AWS.
* **Execution:**
  1. Use synthetic test account credentials or trigger sign-up / registration quiz.
  2. Complete login and navigate to `WorkoutPage`.
  3. Verify that the 7-day schedule strip renders session chips cleanly without overlap (UX02).
  4. Verify that completing a session transitions the workout overview status (UX03).
  5. Capture simulator screenshot for the evidence packet.

### **Job 2: Fast-Forward / Merge Candidate into Rider `main`**
* **Objective:** Bring the owner's primary Rider checkout up to date with candidate `6e385357` so Rider builds include all multi-session, status transition, double progression, and crash protection code.
* **Execution:**
  1. Inspect `/Users/denispetruk/RiderProjects/MuscleCuties`.
  2. Verify clean git status (uncommitted docs already synced).
  3. Merge or fast-forward `continue_musclecuties_beta_plan` (`6e385357`) into `main`.
  4. Run `/usr/local/share/dotnet/dotnet test` in Rider checkout to confirm 100% green.

### **Job 3: Complete Beta Promotion Evidence Packet (TF00–TF02)**
* **Objective:** Finalize the release candidate evidence packet in `docs/architecture/github-transition/evidence/TF00-TF02-beta-candidate-evidence.md`.
* **Required Evidence:**
  - Candidate Git SHA and commit tree.
  - PostgreSQL migration checksums (`001`, `002`, `003`).
  - Full automated test execution log (1,346 tests passing).
  - Simulator in-app workout overview screenshot.
  - AWS API latency and health probe log.
* **Owner Gate:** Present evidence packet to owner for explicit authorization before triggering any code signing, App Store Connect upload, or external TestFlight distribution.

---

## 4. Copy-Paste Prompt for the Next Orchestration Session

Copy the exact block below into the new chat to resume orchestration immediately:

```markdown
# MuscleCuties Orchestrator Execution Prompt

You are the accountable Integration Orchestrator for MuscleCuties.
Follow AGENTS.md, docs/release-audit/2026-11-05/HANDOFF.md, and docs/release-audit/2026-11-05/ORCHESTRATION_HANDOFF_NEXT_SESSION.md.
Context is 100% MuscleCuties only.

Current State:
1. Candidate worktree is at `/Users/denispetruk/.gemini/antigravity/worktrees/MuscleCuties/continue_musclecuties_beta_plan` (commit `6e385357`).
2. Package P0 is COMPLETED: Port 5000 AirPlay collision fixed; LocalBaseUrl moved to port 5080 (58/58 tests passing).
3. iPhone 16 simulator (60DD8563-A1D6-4FE5-9F4F-2BFB975B194B) is booted, pointed to remote AWS API Gateway (https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com), and loaded at the login screen (warm latency 124–165ms, PostgreSQL healthy).
4. Rider checkout at `/Users/denispetruk/RiderProjects/MuscleCuties` is on `main` at `d840d627`.

Your Immediate Objectives:
1. QA01 (In-App Journey): Execute user sign-in or test account creation on the booted simulator to verify the workout overview, schedule strip multi-session chips (UX02), and status transitions (UX03) against the live AWS backend. Capture screenshot evidence.
2. Candidate Sync: Reconcile / fast-forward Rider `main` with candidate `6e385357` so Rider compiles all latest features.
3. TF00–TF02 Evidence Packet: Finalize the release candidate evidence packet with test logs and simulator screenshots for owner review before codesigning and TestFlight promotion.

Maintain clean architecture boundaries, run all local checks (/usr/local/share/dotnet/dotnet test), record progress in CONTROL.md, and request owner approval before any external cloud or TestFlight actions.
```
