# USDA FoodData Central (FDC) API — local setup

The mobile client calls `https://api.nal.usda.gov/fdc/v1/foods/search` for generic food-name search only.
The key is supplied through the environment; it is never committed and never compiled into the app.

## Configure (once per machine)

1. Get a free key: <https://fdc.nal.usda.gov/api-key-signup>.
2. From the repository root:

   ```bash
   cp .env.example .env
   ```

3. Set `FDC_API_KEY=<your key>` in `.env` (`.env` is gitignored).
4. Run the app on a simulator:

   ```bash
   bash scripts/run-ios-simulator.sh "iPhone 17"
   ```

An exported `FDC_API_KEY` in the shell overrides `.env`. CI and Release builds read neither.

## Where it lives

| Piece | Location |
|-------|----------|
| Key template | `.env.example` (tracked) → `.env` (local, ignored) |
| Build wiring | `src/MuscleCuties.UI/MuscleCuties.UI.csproj` — Debug + iOS only; resolves `FDC_API_KEY` from the build environment, then `.env`, and passes it to the launched simulator process via `MlaunchEnvironmentVariables` |
| Runtime read | `src/MuscleCuties.UI/Infrastructure/Network/SecureStorageFdcApiKeyProvider.cs` — SecureStorage `fdc_api_key` first; in Debug, falls back to the `FDC_API_KEY` process variable and stores it in SecureStorage |
| Request policy | `FdcAirGapHandler.cs` (HTTPS GET to the search path only, headers stripped, generic query enforced, appends `api_key`) |
| Client / DI | `FdcApiClient.cs`; registered in `Composition/TransportCompositionExtensions.cs` |
| TLS pinning | `FdcCertificatePinningPolicy.cs` — extra pins via `FDC_CERT_PIN_SHA256` (`;`-separated); Debug-only bypass `FDC_DISABLE_PIN=1` |

## Launching from Rider

- Shared `.run/iOS Simulator - …` configurations call `scripts/run-ios-simulator.sh`, so `.env` applies automatically.
- The native `MuscleCuties.UI` project configuration launches through Rider's own runner. If the key is not picked
  up there, add `FDC_API_KEY=<key>` under *Run | Edit Configurations | MuscleCuties.UI | Environment variables*
  (stored in your local `.idea` workspace, not in the repository).

After one successful launch the key is cached in the simulator's SecureStorage. To rotate it, update `.env` and
erase the app from the simulator (or reset the simulator keychain).

## Verify

```bash
dotnet msbuild src/MuscleCuties.UI/MuscleCuties.UI.csproj -p:Configuration=Debug -p:TargetFramework=net10.0-ios -getProperty:_McFdcApiKey
```

Prints the resolved key (empty means `.env`/environment is not set). In the app, search a generic food
(for example "oats") in Nutrition → Add food; without a key FDC rejects the request and only catalog results appear.

## Not covered

Release/TestFlight builds do not receive a key from this mechanism: the provider reads SecureStorage only and
nothing provisions it. Shipping FDC search to testers needs a separate owner decision (for example proxying
search through the backend so no key ships in the binary).
