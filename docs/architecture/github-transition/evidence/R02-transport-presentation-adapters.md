# R02 Evidence: Mobile Configuration, Transport, and Presentation Adapters Isolation

## 1. Overview and Intent

Package **R02** decouples mobile service composition, transport error handling, and presentation state mapping:
1. **Isolated Composition Modules**: Split monolithic DI registrations from `MauiProgram.cs` into dedicated architectural modules under `src/MuscleCuties.UI/Composition/`:
   - `PlatformCompositionExtensions.cs`: Native handlers, platform logging, notification services, and FDC TLS 1.3 pinning policy.
   - `TransportCompositionExtensions.cs`: API clients (`IMuscleCutiesBackendApiClient`, FDC, Wger, ExerciseDb), encrypted local caches, session token store, and authority gate.
   - `ApplicationCompositionExtensions.cs`: Domain calculation engines, Use Cases, and persistence stores.
   - `WorkoutPlanningCompositionExtensions.cs`: Workout planning config, readiness and gating engines, proposal/offer/calendar clients, and account lifecycle schedulers.
2. **Explicit Transport Failure Taxonomy**:
   - `BackendApiStatus` extended with `RateLimited` and `Timeout`.
   - `ApiFailurePresenter` maps response statuses and HTTP status codes to explicit user actions: `SignInAgain` (401), `Offline` (offline network), `WaitAndRetry` (429 Rate Limited), `Retry` (Timeout, 500 Server, 503 Service Unavailable, 409 Conflict), and `ContactSupport` (schema/permission database errors).
   - `ApiFailureNotice` constructor enforces request reference sanitization, ensuring opaque trace identifiers are retained while credentials, auth tokens, email addresses, and excessive string payloads are strictly rejected.
3. **Screen Parity & Clean Architecture Compliance**:
   - ViewModels retain exact bindings, route keys, localization keys, and encrypted-cache semantics.
   - Clean Architecture boundary tests pass 100%, verifying zero prohibited outer references or synchronous blocking calls.

---

## 2. Affected Screen State Matrix

| Screen / Flow | Transport Outcome | Mapped User Action | Localized Notice Key | Reference Displayed |
|---|---|---|---|---|
| **Any Auth/Session** | 401 Unauthorized / Expired Refresh | `SignInAgain` | `Ui_Common_Error_SessionExpired` | Yes (opaque ID) |
| **Any Screen** | Offline rejected / Network offline | `Offline` | `Ui_Common_Error_Offline` | No (local state) |
| **Any API Route** | 429 Too Many Requests | `WaitAndRetry` | `Ui_Common_Error_RateLimited` | Yes (opaque ID) |
| **Any API Route** | 408 / Timeout / TaskCanceled | `Retry` | `Ui_Common_Error_Timeout` | Yes (opaque ID if present) |
| **Any Mutation** | 409 Concurrency Conflict | `Retry` | Feature Fallback Key | Yes (opaque ID) |
| **Any API Route** | 500 Internal Server Error | `Retry` | `Ui_Common_Error_Server` | Yes (opaque ID) |
| **Any API Route** | 503 / Database Unavailable | `Retry` | `Ui_Common_Error_ServiceUnavailable` | Yes (opaque ID) |
| **Any API Route** | Missing Table / Column / Perm Denied | `ContactSupport` | `Ui_Common_Error_SupportRequired` | Yes (opaque ID) |

---

## 3. Verification Evidence

### Local Test Execution Results

- **`MuscleCuties.Core.Tests`**: **1,056/1,056 tests passed** (including all Clean Architecture boundary tests and UI cleanliness/reachability tests).
- **`MuscleCuties.Mobile.Contracts.Tests`**: **38/38 tests passed** (including 13 new contract tests covering all `ApiFailurePresenter` paths and `RequestId` PII/credential sanitization).
- **`MuscleCuties.Registration.Tests`**: **39/39 tests passed**.
- **`MuscleCuties.Server.Tests`**: **112/112 route & contract tests passed**.
- **Total Tests**: **1,245 / 1,245 passed (0 failed, 0 skipped)**.

### iOS Simulator Compilation

- Target: `net10.0-ios`
- Runtime Identifier: `iossimulator-arm64`
- Outcome: **Build succeeded with 0 errors and 0 warnings**.
