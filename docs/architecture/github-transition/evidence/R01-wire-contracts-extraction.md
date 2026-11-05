# R01 — Wire Contracts Extraction & Payload Equivalence Report

**Executed:** 2026-11-04  
**Feature Family:** Account Registration & Onboarding Preferences  
**Slice:** Plan II Package R01  
**Project Created:** `src/MuscleCuties.Contracts/MuscleCuties.Contracts.csproj`  

---

## 1. Architectural Boundary & Dependency Inversion

`MuscleCuties.Contracts` has been introduced as a leaf wire-only .NET 10 project with **zero external dependencies**:
- No reference to `MuscleCuties.Core`
- No reference to `MuscleCuties.Infrastructure` or `Npgsql`/`EntityFrameworkCore`
- No reference to `Microsoft.AspNetCore`
- No reference to `Microsoft.Maui`

Verified via new architectural boundary test in `CleanArchitectureBoundaryTests.cs`:
- `Contracts_has_zero_dependencies_on_Core_Infrastructure_or_Frameworks` (PASSED).

---

## 2. Extracted Types & Payload Equivalence

All DTOs maintain identical property names, types, order, and JSON serialization semantics:

| Contract Type | Namespace | Target Consumers | Wire Format Preservation |
| --- | --- | --- | --- |
| `StartAccountRegistrationRequest` | `MuscleCuties.Contracts.Account` | Server, UseCases, UI, Infrastructure | Exact match (Email, Username, Password, ConfirmPassword, PrivacyConsent) |
| `RegistrationPrivacyConsent` | `MuscleCuties.Contracts.Account` | Server, UseCases, DB Stores, UI | Exact match (NoticeVersion, Accepted, AcceptedAtUtc) |
| `PrivacyNotice` | `MuscleCuties.Contracts.Account` | Server, DB Stores, UI | Constant `"privacy-notice-2026-10-17"` preserved |
| `AccountRegistrationStatus` | `MuscleCuties.Contracts.Account` | Server, UseCases, UI | Enum values and names preserved |
| `StartAccountRegistrationResult` | `MuscleCuties.Contracts.Account` | Server, UseCases, UI | Exact match (Status, ErrorMessage, ExistingEmail, AttemptId) |
| `WorkoutType` | `MuscleCuties.Contracts.Account` | Server, UseCases, UI | Enum values (Strength, Cardio, Recovery, Rest) preserved |
| `InitialWorkoutPlanExerciseInput` | `MuscleCuties.Contracts.Account` | Server, UseCases, DB Stores | Exact match (ExerciseId, Sets, Reps) |
| `InitialWorkoutPlanDayInput` | `MuscleCuties.Contracts.Account` | Server, UseCases, DB Stores | Exact match (WeekIndex, DayOfWeek, WorkoutType, Name, Exercises) |
| `CompleteAccountRegistrationRequest` | `MuscleCuties.Contracts.Account` | Server, UseCases, DB Stores | Exact match |
| `CompleteAccountRegistrationStatus` | `MuscleCuties.Contracts.Account` | Server, UseCases, UI | Enum values preserved |
| `CompleteAccountRegistrationResult` | `MuscleCuties.Contracts.Account` | Server, UseCases, UI | Exact match |
| `ProfileSetupDraft` | `MuscleCuties.Contracts.Onboarding` | Server, UseCases, UI | Exact match |
| `QuizSelectionDto` | `MuscleCuties.Contracts.Onboarding` | Server, UseCases, UI | Exact match (QuestionId, AnswerId) |

---

## 3. Verification Suite Evidence

- `MuscleCuties.Core.Tests`: **1056/1056 tests passed**.
- `MuscleCuties.Mobile.Contracts.Tests`: **25/25 tests passed**.
- `MuscleCuties.Registration.Tests`: **39/39 tests passed**.
- `MuscleCuties.Server.Tests`: **112/112 route & contract tests passed**.
- MAUI iOS simulator build (`dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -c Debug -p:RuntimeIdentifier=iossimulator-arm64`): **Build succeeded with 0 errors, 0 warnings**.
