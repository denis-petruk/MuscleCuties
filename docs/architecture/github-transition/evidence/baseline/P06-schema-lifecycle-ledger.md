# P06 Non-Destructive Schema Baseline & Upgrade Contract Ledger

Date: 2026-11-04
Recorded by: Integration Authority (Main Gemini CLI Session)
Target Branch: `platform/p06-schema-lifecycle`
Base SHA: `f8ed8f7db0ea85bf775ee70d10b77e8a93bebf16` (P05 Candidate)

---

## 1. Schema Lifecycle & Guard Implementation

- **`PostgresSchemaBootstrap.cs` Safeguard:**
  - Added `IsDisposableTarget()` check before executing `context.Database.EnsureCreatedAsync()`.
  - Strictly denies running `EnsureCreated` on non-disposable / non-loopback databases unless `MC_ALLOW_ENSURE_CREATED=true` is explicitly provided.
  - Throws an `InvalidOperationException`: `"EnsureCreated is strictly forbidden on non-disposable environments. Shared schema must evolve via versioned migrations and compatibility checks."`
- **`.github/workflows/db-release.yml` Workflow:**
  - Configures environment-gated database release under concurrency lock `mutation-<env>`.
  - Executes schema actions exclusively under PostgreSQL session advisory lock (`session-lock-runner`).
  - Enforces `dbcheck` preflight, guarded application, and post-check drift verification.

---

## 2. Beta Data Protection Contract

- The existing Beta PostgreSQL database is classified as non-disposable tester data.
- Under this contract, `dbinit` cannot blindly wipe or drop tables; schema updates are non-destructive and additive.
