# USDA FoodData Central (FDC) API — server-side configuration

FoodData Central generic food search is handled exclusively by the backend API (`MuscleCuties.Server`) via `FdcExternalFoodSource`.
The client (`MuscleCuties.UI`) never communicates with FoodData Central directly, never holds third-party API credentials, and never compiles or stores the FDC API key.

## Architecture & Boundary

- **Mobile client:** Sends authenticated catalog search queries to the backend endpoint `/api/v1/nutrition/catalog/search`. The client has zero direct FDC dependencies.
- **Backend server:** `MuscleCuties.Server` queries `https://api.nal.usda.gov/fdc/v1/` using the server-side `FDC_API_KEY` in a secure `X-Api-Key` request header (never in the URL).
- **Caching & fallback:** Results are parsed, ranked, and persisted by `PostgresExternalFoodImportStore` so subsequent searches are served directly from the canonical database.

## Local development setup

1. Get a free API key at <https://fdc.nal.usda.gov/api-key-signup>.
2. Place `FDC_API_KEY=<your key>` in your root `.env` (gitignored).
3. `scripts/run-local-api.sh` automatically exports `FDC_API_KEY` from `.env` to the server process.
4. If running `MuscleCuties.Server` directly from Rider/IDE, add `FDC_API_KEY=<your key>` in the Server Run Configuration environment variables.

## Deployed environments (AWS / Staging / Production)

The deployed backend receives `FDC_API_KEY` via AWS Secrets Manager or Lambda environment variables. The key is never committed, never packaged in the iOS bundle, and never shipped in the IPA.
