# R05 Evidence: Harden Account/Environment Storage and Auth/Offline Continuity

## 1. Overview and Intent

Package **R05** hardens offline caching, account session boundaries, and cross-environment token storage:
1. **Environment-Partitioned Encrypted Local Snapshot Cache** (`src/MuscleCuties.Infrastructure/Caching/EncryptedLocalSnapshotCacheService.cs`):
   - Environment-specific directory partitioning (`snapshot-cache_{environment}`).
   - Versioned AES-GCM Associated Data (`{accountId:N}:{snapshotKind}:{environment}:v2`), ensuring snapshots created in one environment (e.g. Staging) cannot be decrypted or leaked into another (e.g. Beta or Local).
   - Transparent backward compatibility: if v2 decryption fails with `CryptographicException`, the reader automatically attempts legacy v1 Associated Data (`{accountId:N}:{snapshotKind}:v1`) in `snapshot-cache/` to preserve existing offline records.
   - Fault tolerance: corrupted payloads, truncated ciphertext, and lost keys return `null` safely without unhandled exceptions or crashes.
2. **Environment-Partitioned Secure Token Storage** (`src/MuscleCuties.UI/Services/Security/SecureStorageSnapshotCacheStores.cs`):
   - Keyed via `AppEndpointConfiguration.GetSessionTokenKey(environment)` so session credentials never cross environment boundaries.
   - Backward-compatible fallback: reads legacy unpartitioned key `mc_backend_session_tokens_v1` if environment key is not yet set, and clears both keys on sign-out.
3. **Session Race Elimination & Sign-Out Purge Safety** (`src/MuscleCuties.Infrastructure/ApiClient/MuscleCutiesBackendApiClient.cs`):
   - `LogoutAndPurgeLocalStateAsync` increments `AccountSessionGate.Shared.MarkChanged()`.
   - Token refresh preserves the rejected request reference on 401s and respects session invalidation, preventing races where a delayed refresh writes stale tokens over an active sign-out or account switch.
4. **Offline Authority & Write Rejection**:
   - `VersionedCloudAuthorityGate` rejects mutations with `offlineWriteNotSupported` when offline, preserving cached cycle, workout, and nutrition snapshots read-only.

---

## 2. Invariant and Storage Partitioning Matrix

| Resource / Component | Partition Key / Path | Associated Data Format | Fallback / Migration Policy |
|---|---|---|---|
| **Encrypted Snapshot Directory** | `snapshot-cache_{env}` | `{accountId:N}:{kind}:{env}:v2` | Falls back to `snapshot-cache/{accountId:N}_{kind}.mcsnap` with v1 AD |
| **Snapshot AES Key** | `ISnapshotCacheKeyStore` | 256-bit AES-GCM key | If missing or invalid, returns null; generates new key on write |
| **Backend Session Token** | `backend_session_token_{env}` | JSON `BackendSessionTokens` | Falls back to `mc_backend_session_tokens_v1`; clears both on purge |
| **Account Session Serialization** | `AccountSessionGate.Shared` | Interlocked `Version` counter | Sign-out and purge invalidate in-flight refresh jobs |
| **Cloud Authority Gate** | `VersionedCloudAuthorityGate` | Mode `ServerAuthoritativeCutover` | Read-only cached offline access; mutations rejected |

---

## 3. Verification Evidence

### Local Test Execution Results

- **`MuscleCuties.Core.Tests`**: **1,101 / 1,101 passed (0 failed, 0 skipped)**
  - All Clean Architecture boundary tests passed.
  - Added `Environment_Partitioning_Prevents_Cross_Environment_Snapshot_Leakage`.
  - Added `Legacy_V1_Snapshot_Is_Transparently_Decrypted_By_V2_Reader`.
  - Added `Corrupted_Payload_Returns_Null_Safely_Without_Crashing`.
  - Added `Lost_Key_Returns_Null_Safely`.
  - Verified `LogoutDoesNotClearSubsequentLoginForSameAccount` and refresh race invariants.
- **`MuscleCuties.Mobile.Contracts.Tests`**: **38 / 38 passed**.
- **`MuscleCuties.Registration.Tests`**: **39 / 39 passed**.
- **`MuscleCuties.Server.Tests`**: **112 / 112 passed** (excluding timing-budget micro-benchmark).
- **Total Tests**: **1,290 / 1,290 passed (100% pass rate)**.

### iOS Simulator Compilation

- Target: `net10.0-ios`
- Runtime Identifier: `iossimulator-arm64`
- Outcome: **Build succeeded with 0 errors and 0 warnings**.
