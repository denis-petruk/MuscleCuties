# P09 Explicit Endpoint Selection & Compatibility Ledger

Date: 2026-11-04
Recorded by: Integration Authority (Main Gemini CLI Session)
Target Branch: `product/p09-client-endpoints`
Base SHA: `8f090cc25ec2f42bf1895a08aaeeaf0256d981b2` (P01 Candidate)

---

## 1. Endpoint Architecture & Safety Controls

- **`AppEndpointConfiguration.cs`**:
  - Implemented `AppTargetEnvironment` (`Local`, `Staging`, `Beta`).
  - **Removed implicit shared debug fallback:** In `DEBUG` mode, default endpoint is now safely `Local` (`http://127.0.0.1:5000`), preventing local builds from accidentally mutating or querying shared Beta data.
  - **Host Allowlist Enforced:** Only allowlisted hosts (`127.0.0.1`, `localhost`, `staging-api.musclecuties.app`, `y2ejsgrc6l.execute-api.us-east-2.amazonaws.com`) are permitted. Arbitrary unvalidated external hosts are rejected.
  - **Cache & Token Partitioning:** Cache directories and token keys now incorporate the active environment name (`snapshots_local`, `snapshots_staging`, `snapshots_beta`), preventing cross-environment token reuse or cache contamination.

---

## 2. Verification Evidence

- `tests/MuscleCuties.Mobile.Contracts.Tests/AppEndpointConfigurationTests.cs`:
  - Verified documented URLs.
  - Verified host allowlist positive and negative tests.
  - Verified cache directory and token key partitioning.
  - 25/25 tests passed.
