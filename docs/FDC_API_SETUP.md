# USDA FoodData Central (FDC) setup

The nutrition catalog falls back to USDA FoodData Central only when the server has an API key.
The client never holds the key and never calls USDA directly: the app asks
`GET /api/v1/nutrition/catalog/search`, and the server adds the generic query to its own request.

- Key name: `FDC_API_KEY` (server configuration; `Fdc:ApiKey` is also accepted).
- Header: `X-Api-Key`, never a query parameter.
- Request: `https://api.nal.usda.gov/fdc/v1/foods/search`, generic data types only
  (`Foundation`, `SR Legacy`, `Survey (FNDDS)`), 15 results, 4-second timeout.
- Only recognized generic names leave the service (`GenericFoodQueryRule`); everything else is
  answered from the local catalog. A missing key, a non-success status, a timeout or an unreadable
  payload yields no external results and never fails the catalog search.

## Where the key is read

| Environment | Source |
| --- | --- |
| Local API | repository-root `.env`, loaded by `scripts/run-local-api.sh` |
| AWS Lambda (`musclecuties-backend-api`) | Lambda environment variable `FDC_API_KEY` |
| iOS app | none; the key must never be compiled into the app or sent to the client |

Do not commit the key. `.env` is ignored; deployment secrets live only in the Lambda configuration.

## Local check

```bash
cp .env.example .env          # then set FDC_API_KEY to a key from https://fdc.nal.usda.gov/api-key-signup.html
scripts/run-local-api.sh
```

Verify the server path (replace the token with a signed-in session's access token):

```bash
curl -s -o /dev/null -w "%{http_code}\n" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  "http://127.0.0.1:5080/api/v1/nutrition/catalog/search?query=tempeh&pageSize=10&pageNumber=1"
```

A search for a generic food that is not yet in the catalog should return items whose
`sourceSummary` is `Nutrition_Source_UsdaFoodDataCentral`; the imported foods are stored as
owner-scoped rows and are found locally on the next search.

Check the key itself without exposing it:

```bash
set -a; . ./.env; set +a
curl -s -o /dev/null -w "%{http_code}\n" \
  -H "X-Api-Key: $FDC_API_KEY" \
  "https://api.nal.usda.gov/fdc/v1/foods/search?query=chicken%20breast&dataType=Foundation,SR%20Legacy,Survey%20(FNDDS)&pageSize=3&pageNumber=1"
```

USDA answers `200` for a valid key. Very rapid repeated requests from one address can be answered
with an nginx `400`; space verification calls a couple of seconds apart.

## Deployment check

The deployed Lambda must list `FDC_API_KEY` among its environment variables. It can be compared with
the local key without printing either value:

```bash
aws lambda get-function-configuration --function-name musclecuties-backend-api \
  --query "Environment.Variables | keys(@)" --output json
```
