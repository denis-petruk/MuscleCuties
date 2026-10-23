# MuscleCuties — Principal Systems Architect & Mobile Engineer Directive

## 1. Context Economy & Navigation (Read First)
- **Language & Brevity:** Think, search, and code in English. Respond concisely without introductory filler or repeating the prompt.
- **Index-First Navigation:** Identify the target project from the Directory Index below before exploring. Never scan the whole repository.
- **Search-First & Frugal Reading:** Always use `rg` or `grep` to locate symbols, DTOs, or XAML bindings first. Read only narrow line ranges around matches—never load entire large files or logs into context.
- **Deny Noise:** NEVER read `bin/`, `obj/`, `.vs/`, `Migrations/*.Designer.cs`, `*.g.cs`, `.env`, lockfiles, or binary assets.
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
  - `PostgresDbContext` (sole schema/migration authority) and `RepoModels`. Maps pure Domain Entities to/from PostgreSQL.
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

## 4. Pre-Commit Verification Gate
- **Build Gate:** Run **iOS builds** and `dotnet test` only. Do NOT run Android, Mac Catalyst, or Windows builds unless requested.
- **Mandatory 6-Point Check before finalizing changes or deletions:**
  1. *Boundaries:* Zero outer imports in Core/UseCases; only DTOs cross into UI/Controllers; no `DbContext`/`IRepository` in ViewModels.
  2. *XAML Protection:* Before deleting/renaming any C# property, `[ObservableProperty]`, or `[RelayCommand]` with 0 C# references, `rg` all `.xaml` files to confirm zero runtime bindings.
  3. *Async & Main Thread:* Zero synchronous I/O (`.Wait()`, `.Result`). All EF Core calls must use `*Async()`.
  4. *Zero-Crash & Isolation:* Use cases tested via mocks without DB/UI boot; refactor scenario-by-scenario (no "Big Bang" rewrites).
  5. *Zero Magic Literals:* Scan diff for bare numbers or cryptic strings.
