# P03 CI Scaffold Ledger & Parity Mapping

Date: 2026-11-04
Recorded by: Integration Authority (Main Gemini CLI Session)
Target Branch: `platform/p03-ci-scaffold`
Base SHA: `5b27d1b117cefc6e4b85da8e9e1cfa282d8d80f8` (P02 Candidate)

---

## 1. Workflows Implemented

- **`.github/workflows/ci.yml`**:
  - `verify_source`: SDK verification and Server Release restore/build.
  - `verify_plan_generation`: Offline Python deployment tests, Core tests, Mobile contracts tests, Registration tests, Server HTTP route tests, and Model catalog integration filters.
  - `postgres_verify`: Calls reusable database verification workflow with PostgreSQL 17 service.
  - `required`: Central aggregation job enforcing zero failures across all required jobs.
- **`.github/workflows/db-verify.yml`**:
  - Sets up disposable PostgreSQL 17 on `127.0.0.1:5432`.
  - Creates the 3 disposable databases (`mc_test_nutrition_ci`, `mc_retry_disposable`, `mc_pg09_disposable`) and runs `services/postgresql/roles-and-grants.sql`.
  - Executes `MuscleCuties.Registration.Integration.Tests`.

---

## 2. Parity Status & Joint P03/P06 Requirement

- **Status**: Scaffold complete in shadow mode.
- **Joint Gate Note**: In accordance with the implementation roadmap, P03 and P06 form a jointly verified candidate. The schema compatibility tests (P06) will execute against real PostgreSQL in CI before the candidate is promoted to `main`.
