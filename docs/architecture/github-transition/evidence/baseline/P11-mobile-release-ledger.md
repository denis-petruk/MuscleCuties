# P11 Environment-Specific iOS & TestFlight Release Workflow Ledger

Date: 2026-11-04
Recorded by: Integration Authority (Main Gemini CLI Session)
Target Branch: `product/p11-ios-release`
Base SHA: `60f2387baea1d5db2f458151ce50239b9772bf62` (P09 Candidate)

---

## 1. Scope & Implementation

- **`src/MuscleCuties.UI/MuscleCuties.UI.csproj`**:
  - Added conditional compilation constants for `APP_ENV_STAGING` and `APP_ENV_BETA` when `-p:AppTargetEnvironment=<env>` is specified.
- **`.github/workflows/mobile-release.yml`**:
  - Created GitHub Actions workflow for macOS runners (`macos-15`).
  - Restores .NET 10 SDK and `maui-ios` workload.
  - Compiles the iOS binary with explicit environment selection.
- **Simulator Build Verification**:
  - Under the authorized execution grant, `dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -c Debug -p:RuntimeIdentifier=iossimulator-arm64` completed with 0 errors and 0 warnings.
  - Successfully produced `src/MuscleCuties.UI/bin/Debug/net10.0-ios/iossimulator-arm64/MuscleCuties.UI.dll`.

---

## 2. TestFlight & Device Acceptance Gate

- Building the simulator binary proves toolchain and compilation integrity.
- Distribution to TestFlight / App Store Connect and physical device testing remains an owner-controlled gate requiring Apple certificates and signing credentials.
