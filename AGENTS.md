# MuscleCuties — Principal Systems Architect & Mobile Engineer Directive

## 1. Context Economy & Navigation (Read First)
- **Language & Brevity:** Think, search, and code in English. Respond concisely without introductory filler or repeating the prompt.
- **Index-First Navigation:** Identify the target project from the Directory Index below before exploring. Never scan the whole repository.
- **Search-First & Frugal Reading:** Always use `rg` or `grep` to locate symbols, DTOs, or XAML bindings first. Read only narrow line ranges around matches—never load entire large files or logs into context.
- **Deny Noise:** NEVER read `bin/`, `obj/`, `.vs/`, `*.g.cs`, `.env`, lockfiles, or binary assets.
- **State Continuity:** After completing a task, update `ROADMAP.md` / `STATUS.md` so context is preserved across `/clear` sessions.

## 2. Project Directory Index & Architecture Boundaries
Dependencies point strictly INWARD: `Server / UI` -> `Infrastructure` -> `UseCases` -> `Core`.

- `src/MuscleCuties.Core` (Layer 1: Rich Domain Entities)
  - Pure C# enterprise logic and state transitions (private setters, no anemic models, cohesive single-file invariants).
  - **ZERO dependencies:** No ORM/EF attributes, JSON/serialization tags, ASP.NET, or MAUI references.
- `src/MuscleCuties.UseCases` (Layer 2: Application / Interactors)
  - Single-responsibility Lego blocks (`ExecuteAsync(InputDTO) -> Task<OutputDTO>`).
  - Orchestrates repositories and entities only. NEVER perform business/cycle math or validation here—delegate to Core Entities.
- `src/MuscleCuties.Infrastructure.PostgreSql` (Layer 3: Adapters & Persistence)
  - `PostgresDbContext` (sole schema authority) and `RepoModels`. Maps pure Domain Entities to/from PostgreSQL.
  - Schema is model-first: there are NO EF migrations. `Persistence/Schema/security.sql` owns roles, grants, RLS policies and SECURITY DEFINER functions; `Persistence/Schema/schema_supplement.sql` owns the few objects EF cannot express. `DbMaintenance dbinit` creates/secures/seeds, `dbcheck` detects drift.
- `services/MuscleCuties.Server` (Layer 4: Backend Host)
  - ASP.NET Core (.NET 10) + AWS Lambda (`Program.cs` composition root: never inject scoped services into singletons).
- `src/MuscleCuties.UI` (Layer 4: Mobile Client)
  - .NET MAUI 10 iOS shell (`MauiProgram.cs`), CommunityToolkit.Mvvm 8.4 / Maui 13.
  - **Zero SQLite:** Offline gym caching uses `ILocalCacheService` (AES-encrypted JSON snapshots in `FileSystem.AppDataDirectory` with `SecureStorage` keys; never use `Preferences`).
  - ViewModels/Controllers consume/produce plain `InputDTO`/`OutputDTO` only—never leak Domain Entities or inject `DbContext`/`IRepository` directly into ViewModels.
  - `Models/UI/` are XAML runtime contracts; treat as read-only unless verified unused across all `.xaml` files.
- `tests/MuscleCuties.Core.Tests`
  - xUnit in-memory & architecture verification suite.

## 3. Privacy, Security & Code Invariants
- **100% C# / .NET 10:** Zero Python or SQLite in production.
- **Privacy & Third-Party Air-Gap:** Authoritative data lives on first-party PostgreSQL (AES-256 at rest, TLS 1.3 in transit) and private S3 (short-lived presigned URLs). NEVER send user/health data to third-party APIs or AI services (external APIs like Food Data Central receive generic query strings only, e.g., `"chicken breast"`).
- **Zero Sensitive Traces:** Use synthetic fixtures only. Never log real health records, tokens, passwords, or photo binaries.
- **Dormant Health Adapters:** NEVER wire `DisabledHealthSyncService` to live Apple Health / Google Health Connect without explicit user authorization (HealthKit is strictly read-only step count).
- **Deterministic Math (`ReadinessEngine`, `CyclePrediction`):** Catch all edge cases (`DivideByZero`, empty history, negative durations) and return clinically conservative default records. Never hallucinate data or omit null checks.
- **Zero Magic Numbers or Cryptic Keys:** Every numeric literal (thresholds, offsets, multipliers, UI dimensions—except trivial `0`/`1` loop counters) and localization/resource key MUST be a descriptive, human-readable constant (e.g., `DefaultLutealPhaseDays = 14`, `Nutrition_MedicalDisclaimer`).
- **Always-Applicable Strength Plan (owner rule, 2026-11-01):** Every generated plan MUST be immediately performable — non-empty, equipment-satisfiable, no unmet prerequisite — and a session with an active injury MUST still contain strength training. Injury work is additive and dose-adjusted, never a replacement: rehab or mobility alone is not an acceptable session. When a contraindication removes an exercise, substitute a safe strength alternative (same slot → same pattern other equipment → same muscle group other pattern → minimum viable strength block); if no strength movement is permissible, withhold the session with an injury-review reason instead of silently converting it to rehab. Never emit a zero-strength session.

## 4. Pre-Commit Verification Gate
- **Build Gate (owner rule, 2026-11-01):** Test **backend only** — `dotnet build`/`dotnet test` on Core, UseCases, Infrastructure, DbMaintenance and Server. NEVER build, run or test the UI project: no iOS/Android/Mac Catalyst/Windows builds, no simulator runs, no screenshots. UI code must still be implemented fully and correctly; it simply is not verified by the agent. If a UI change needs checking, hand it to the owner instead of building it.
- **No Android Target (owner rule, 2026-11-01):** The UI project targets **iOS only**. Never add, restore or re-enable `net10.0-android` in `TargetFrameworks`, never install Android workloads, and never introduce Android-only code paths (`#if ANDROID`, `Platforms/Android` additions). An Android target framework appearing in the build is a regression to be removed, not accommodated.
- **Mandatory 6-Point Check before finalizing changes or deletions:**
  1. *Boundaries:* Zero outer imports in Core/UseCases; only DTOs cross into UI/Controllers; no `DbContext`/`IRepository` in ViewModels.
  2. *XAML Protection:* Before deleting/renaming any C# property, `[ObservableProperty]`, or `[RelayCommand]` with 0 C# references, `rg` all `.xaml` files to confirm zero runtime bindings.
  3. *Async & Main Thread:* Zero synchronous I/O (`.Wait()`, `.Result`). All EF Core calls must use `*Async()`.
  4. *Zero-Crash & Isolation:* Use cases tested via mocks without DB/UI boot; refactor scenario-by-scenario (no "Big Bang" rewrites).
  5. *Zero Magic Literals:* Scan diff for bare numbers or cryptic strings.

## 5. Design System & UI Agent Workflow
- **Read Before UI Work:** Start with [DESIGN.md](DESIGN.md), then the relevant section of [the presentation specification](docs/UI_PRESENTATION_DESIGN_SPEC.md) and the nearest existing component. These govern app-wide design; [the dated audit](docs/design/AUDIT_2026-11-02.md) records implementation gaps rather than alternate rules.
- **Owner Direction (2026-11-02):** Warm and wellness-focused; retain the current soft palette. Use the supplied references for clear hierarchy, contextual logging, compact useful rows and choice flows. Reference copy, metrics and advertised features are not requirements or permission to add third-party health-data processing.
- **Hard Style Limit (owner rule, 2026-11-02):** One consistent shared page style across the app. Every component family has at most two visual styles; use one unless the second has a distinct reusable purpose. Buttons are `ButtonPrimary`/`ButtonSecondary`; cards are `CardPrimary`/`CardSecondary`. No feature-specific skins, third-style variants or renamed families to evade the limit. Selection/error/busy are functional states within these styles. Existing extra card styles and their four-card source assertion must be migrated together before claiming compliance.
- **Reuse First:** Colors stay in `Resources/Styles/Colors.xaml`; shared roles/styles in `Resources/Styles/Styles.xaml`; C# resolution through `AppThemeResources` and existing palette adapters; reusable behavior in `Controls/Shared`; visible and accessible copy in the existing EN/RU catalogs. Do not introduce another palette/token catalog, page-local styles or arbitrary dimension tokens.
- **Role-only colour references (owner rule, 2026-11-03):** Outside `Resources/Styles/Colors.xaml`, never resolve a value-shaped palette key (`Gray300`, `White`, `Black`, `DeepMauve`, …). Use or add an intent-named role (`Surface*`, `Text*`, `Border*`, `Track*`, `Accent*`, state) whose light/dark values exactly match the palette value; appearance must not change. `UiContractGuardTests.Value_shaped_colour_tokens_are_definition_only` rejects consumer references and new value-shaped definitions. `Transparent` stays a sentinel; brand/action, phase, readiness, recovery, water, sleep and macro roles are already intent-named.
- **Complete Patterns:** Cover light/dark, large text, localized semantics, loading/empty/error/offline/disabled states and the appropriate screen recipe. All modal sheets must use the shared `AnimatedModalView` lifecycle, with safe-area/keyboard handling, focus capture, entry, background accessibility isolation, title announcement, focus restoration and Reduce Motion. A non-dismissible inline page state is not a sheet and must be recorded as an exception with its reason. New conventions do not certify existing compliance.
- **Every Block Has a Contract:** Use the specification's block catalog, named anatomy/text/spacing roles and state matrix. Normal text (including helper/placeholder text) meets 4.5:1; qualifying large text and essential non-text indicators meet 3:1. Check composed colors in each appearance/state; support enlarged text without clipping or shrinking essential content.
- **Incremental Migration:** Preserve bindings, routes and DTO boundaries. Verify XAML and C# callers before retiring a contract or token. Record source evidence and remaining owner device checks; the backend-only verification rule above still applies.
