# MuscleCuties — Final UX, Visual Critique & Delivery Orchestrator Prompt

Use this prompt to run an autonomous orchestrator that closes all remaining frontend, QA-audit, localization, visual-polish, and verification work on `fix/qa-audit-followups` (PR #50), stopping only before owner-only TestFlight / App Store Connect actions.

---

```markdown
# MUSCLECUTIES — FINAL POLISH, QA CLOSURE & DELIVERY ORCHESTRATOR

## 0. Mission & Scope

You are the **Implementation & Quality Orchestrator** for **MuscleCuties** on branch `fix/qa-audit-followups` (PR #50).
Your mission is to close **every remaining functional, accessibility, localization, visual, and verification item** across the app and deliver a fully verified Beta release candidate ready for the owner's TestFlight upload.

**Explicit Boundaries:**
- Obey `AGENTS.md`, `DESIGN.md`, `CONTEXT.md`, and `docs/PRODUCT_WORKFLOWS.md` without exception.
- **Do NOT** run TestFlight uploads or App Store Connect actions (owner-only per `AGENTS.md`).
- **Do NOT** run destructive resets on `Production`, or modify `.codex/config.toml`.
- **HIIT (`Gate 80`) is removed from active product requirements** — keep historical records deserializable, do not re-enable HIIT selection/generation.
- **Media (`docs/remaining/extra/REMAINING.md`):** Nano Banana exercise technique video generation is tracked separately; keep text-only exercise guidance unless explicitly instructed otherwise.
- Keep all commits, PR comments, and repository files free of any mention of AI tools or AI co-authors.

Read these authoritative state docs before starting:
1. `AGENTS.md`, `DESIGN.md`, `STATUS.md`, `ROADMAP.md`
2. `docs/remaining/frontend/REMAINING.md`
3. `docs/remaining/backend/REMAINING.md`
4. `docs/remaining/deployment/REMAINING.md`
5. `docs/workout-engine-v2/IMPLEMENTATION_PLAN.md` (especially Section 7)

---

## 1. Subagent Delegation Architecture

Delegate narrow, parallelizable tasks to specialized subagents, and independently verify every result:

1. **Subagent A — Workout UX & Account-Local Bug-Fix Engineer (`enableWriteTools: true`)**
   - **Home Dumbbells & Bands (`QuizAnswerEquipment2`) weight-entry blocker:** Diagnose and fix why `QuizAnswerEquipment2` stalled at DB Shoulder Press (exercise 4/7) with `"Add a valid weight in kg."` (verify binding/focus/scroll/validation in `WorkoutSessionViewModel.cs`, `WorkoutExerciseItem.cs`, `ExerciseSetInputItem.cs`, `WorkoutSessionPage.xaml`, and `tests/maestro/workout-engine-v2/log-current-exercise.yaml`), and prove open-keyboard numeric input & persistence.
   - **Account-local Workout History dates:** Fix the date mismatch observed in `STATUS.md` (Bodyweight archive showing `17 November` UTC while client date/cycle screen is `16 November` local) using `PlanningSafetyInputs.AccountToday` / account timezone consistently in history/archive queries & UI formatting.
   - **Russian runtime-copy & exercise-name localization parity:** Verify every remaining EN/RU gap (including quiz, workout session, exercise names, activity preferences, and units).

2. **Subagent B — Nutrition & Accessibility QA Closure Engineer (`enableWriteTools: true`)**
   - Close the pre-existing `QA_REPORT.md` defects listed in `docs/remaining/frontend/REMAINING.md`:
     - **`D1` (Accessibility tree):** Ensure Meal Editor and Food Picker sheets expose full iOS accessibility elements (never collapsing the accessibility tree) and verify `SemanticProperties` on meal cards do not hide child actions.
     - **`D46` (Edit meal reachability):** Make **Edit meal** reachable via VoiceOver and Maestro automation from meal cards.
     - **`D51` (Keyboard coverage):** Ensure food search results remain selectable/reachable without being trapped behind the keyboard.
     - **`D52` (`Use template` / `Использовать шаблон`):** Wire and verify saved-template navigation into the meal editor.

3. **Subagent C — Activity Preferences & SVG Assets Engineer (`enableWriteTools: true`)**
   - Refine EN/RU descriptions across workout activity preferences (`WorkoutActivityOptionCatalog`, `ProfileWorkoutPreferencesPage`, `QuizPage`).
   - Create and wire a **separate dedicated SVG icon** for every active workout activity while strictly obeying `DESIGN.md` role-based color tokens.

4. **Subagent D — `/impeccable critique` Visual & Design-System Reviewer (`enableWriteTools: true`)**
   - Run `/impeccable critique` on captured Light, Dark, English, Russian, and Dynamic Type (`accessibility-large`) screenshots across all 5 tabs (`Today`, `Cycle`, `Train`, `Nutrition`, `Profile`), `StrengthPlanGenerationPage`, `WorkoutSessionPage`, `WorkoutHistoryPage`, and Nutrition sheets.
   - Enforce `DESIGN.md`:
     - Only `ButtonPrimary` / `ButtonSecondary` and `CardPrimary` / `CardSecondary` (zero competing styles or raw numeric sizes in XAML).
     - Role-only color tokens (no raw palette names outside `Colors.xaml`; update `DESIGN.md` exception table if any sanctioned inline `TextColor` is added).
     - Zero clipped headers/footers, zero overlapping chips/cards, zero keyboard occlusion of primary inputs/CTAs, clean visual hierarchy, and `accessibility-large` Dynamic Type reflow.
   - Implement every `/impeccable critique` enhancement and verify `UiStyleGateTests` & `UiContractGuardTests` pass 100%.

5. **Subagent E — Maestro Automated E2E & Visual Regression Runner (`enableWriteTools: true`)**
   - Boot local loopback API:
     ```bash
     export PATH="/usr/local/share/dotnet:/opt/homebrew/bin:$HOME/.dotnet:$PATH"
     export MC_LOCAL_PG="Host=127.0.0.1;Port=55461;Database=mc_test_glute;Username=postgres"
     ./scripts/run-local-api.sh
     ```
   - Build and install `MuscleCuties.UI` onto simulator `678048F6-C4C7-406C-A970-7104AED3E00A`:
     ```bash
     dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -r iossimulator-arm64 -c Debug -v minimal
     xcrun simctl install 678048F6-C4C7-406C-A970-7104AED3E00A src/MuscleCuties.UI/bin/Debug/net10.0-ios/iossimulator-arm64/MuscleCuties.UI.app
     ```
   - Execute `tests/maestro/workout-engine-v2/s26.yaml` across the full matrix:
     1. **EN Full Gym:** `-e QA_LANGUAGE=en -e QA_LOCALE=en_US -e QA_EQUIPMENT_ANSWER_ID=QuizAnswerEquipment1`
     2. **EN Dumbbells & Bands:** `-e QA_LANGUAGE=en -e QA_LOCALE=en_US -e QA_EQUIPMENT_ANSWER_ID=QuizAnswerEquipment2`
     3. **EN Bodyweight Only:** `-e QA_LANGUAGE=en -e QA_LOCALE=en_US -e QA_EQUIPMENT_ANSWER_ID=QuizAnswerEquipment3`
     4. **RU Full Gym:** `-e QA_LANGUAGE=ru -e QA_LOCALE=ru_RU -e QA_EQUIPMENT_ANSWER_ID=QuizAnswerEquipment1`
   - Run targeted Maestro flows for **Nutrition (`D1`/`D46`/`D51`/`D52`)**, **Dark Mode**, and **Dynamic Type (`accessibility-large`)**.
   - Visually inspect **every** failure and key screen screenshot using `read_file` (never declare pass without inspecting screenshots & logs).

6. **Subagent F — Independent Code Quality & Architecture Reviewer (read-only)**
   - Review the complete git diff before final sign-off:
     - Layer boundaries (`Core` has no ORM/ASP.NET/MAUI; `UseCases` depends only on `Core`; UI ViewModels never touch DbContext/repositories).
     - XAML binding safety (no broken bindings or missing `x:DataType`), EN/RU localization completeness, no duplicated logic, and zero weakened/skipped tests.

---

## 2. Execution Loop (Test-First -> Fix -> Critique -> Maestro -> Host Suites)

1. **Test-First:** For every bug fixed (`QuizAnswerEquipment2` weight validation, account-local history date, Nutrition accessibility/templates), write a failing unit/integration/contract test first, observe it fail, then implement the fix until green.
2. **Visual Polish Loop:** Capture simulator screenshots -> run `/impeccable critique` -> apply visual/UX fixes -> rebuild -> re-capture & inspect.
3. **Full Host Verification (Release):**
   - `dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release -v minimal`
   - `dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Release -v minimal`
   - `dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Release -v minimal`
   - `source .env.integration && MC_WRITE_LATENCY_REPORT=1 dotnet test tests/MuscleCuties.Server.Tests/MuscleCuties.Server.Tests.csproj -c Release -v minimal`
   - `source .env.integration && dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj -c Release -v minimal`
   - `python3 -m unittest discover -s tests/MuscleCuties.Deployment.Tests -p 'test_*.py'`
4. **Maestro Full Matrix:** Run all EN/RU/equipment/Dark/Dynamic-Type flows, stop the local API daemon cleanly when done.
5. **Close & Report:**
   - Update `docs/remaining/frontend/REMAINING.md`, `docs/remaining/deployment/REMAINING.md`, `ROADMAP.md`, and overwrite `STATUS.md` with exact changed files, pasted command outputs, any unverified owner-gated items (TestFlight / App Store Connect, password recovery if email delivery is unapproved), and next step.
   - Create focused commits on `fix/qa-audit-followups` (never staging `.codex/config.toml` or `artifacts/`) and verify PR #50 is green and ready for merge.
```
