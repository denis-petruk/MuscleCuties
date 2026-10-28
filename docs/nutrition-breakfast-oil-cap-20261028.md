# Breakfast preference and generated oil — 2026-10-28

## Scope and Git preservation

- Started on `gitlab-working` at `aad6c10d796918bac52c9f3af59dd841904fa31c`. Authenticated GitLab fetch succeeded; the checkpoint branch and HEAD had zero divergence. No merge was necessary.
- Implementation branch: `codex/nutrition-breakfast-oil-cap-20261028`. The checked-in GitLab workflow excludes this branch. No deployment, live database action or scheduling activation is included.
- Continued existing partial nutrition edits. Preserved unrelated migration consolidation/deletions, reviewed-exercise importer changes and `temp-db-reset/`; these are excluded from the nutrition commit.
- All agents had bounded file ownership; the orchestrator alone handled integration and Git. Independent focused and persistence reviews were completed.

## Implemented behavior

- Breakfast suggestions and day planning expose localized Savoury/Sweet buttons using existing native button styles. The selected option updates immediately and reloads targets and suggestions. Version checks discard older responses. A target-load failure can be retried or recovered by reopening the modal.
- Day-plan reloads recalculate all slots because breakfast preference changes the shared meal distribution. The preference retains its existing view-model lifetime; durable preference storage is outside this change.
- Core oil classification uses the requested fat density threshold of 80 g per 100 g. Sauce portions remain at or below 14 g after serving snapping. A bounded whole-food search can recover feasible meals after the cap; impossible combinations remain rejected. Existing calorie/protein acceptance tolerances remain in force; exact fat/carbohydrate attainment is not promised by the general solver.
- The local multi-ingredient planner reapplies a shared meal oil budget after distribution and serving rounding, then reports actual resulting macros. Zero portions do not become positive through rounding.
- Server reference suggestions load personal targets and use the shared breakfast-preference meal distribution. Oil is identified by the separate English word `Oil`; all current oil concepts use Olive Oil or Sesame Oil. A meal-wide 14 g budget is further limited by available fat and calories. Remaining targets are allocated to non-oil components.
- Component nutrients follow the existing **per 100 g** contract. At 14 g, pure oil therefore contributes 14 g fat and 126 kcal using the requested 9 kcal/g calculation; the DTO stores 100 g fat and 900 kcal per 100 g. Zero oil is omitted so the meal remains loggable. Existing fixed reference concepts remain estimates rather than measured food composition.
- Unlinked reference ingredients use ID zero instead of fabricated catalog IDs. Logging stores owner-scoped `GeneratedMealSnapshot` rows, reuses only identical internal densities, and preserves older logged totals when a later portion differs. Snapshots are excluded from custom food search and barcode lookup and cannot inherit a same-name custom food's micronutrients. No schema change is introduced.

## Verification evidence

| Check | Result |
| --- | --- |
| Core/application suite | 990 passed |
| Server/API suite | 82 passed |
| Mobile contracts suite | 9 passed |
| Server nutrition mapping and PostgreSQL round trips | 18 passed |
| Explicit disposable schema/catalog preparation | 1 passed |
| iOS `net10.0-ios`, Debug, `iossimulator-arm64` | Passed; 0 errors, 14 existing analyzer warnings |
| Diff whitespace and focused architecture/async review | Passed |
| Impeccable detector for changed modal | No findings |

Coverage includes feasible 300/600/1200 kcal meals, non-vacuous solver results, oversized serving rounding, zero/low fat budgets, multiple oils, all current oil concepts, target/component reconciliation, preference catalogs, owner target forwarding, exclusions, generated meal logging/readback, later portions retaining historical totals, and internal snapshot exclusion from food search.

Database verification used a newly created loopback-only PostgreSQL 17 container, database `mc_test_nutrition_oil`, synthetic accounts and a dedicated synthetic role. Preparation used the preserved checkout's migration state, including unrelated local consolidation; those migrations are not part of this feature. The fixture cleaned its accounts and the disposable container was removed after verification. No live database was used.

### Existing integration-harness issues and temporary overrides

The default integration-project restore reports `NU1202` for its unused direct `Microsoft.EntityFrameworkCore.Design` reference and dependencies. The initial stale package assets also referenced a missing path under a different local cache. An official-feed temporary NuGet configuration and cache were used without reading personal NuGet credentials.

After restore, the unrelated `InjuryCanonicalReconciliationTests.cs` fails compilation because its `PostgresCheckInAndInjuryStore` constructor call lacks `TimeProvider`. The scoped nutrition run used this temporary MSBuild target file; repository project files and the unrelated test were unchanged:

```xml
<Project>
  <ItemGroup Condition="'$(MSBuildProjectName)' == 'MuscleCuties.Registration.Integration.Tests'">
    <PackageReference Remove="Microsoft.EntityFrameworkCore.Design" />
    <Compile Remove="InjuryCanonicalReconciliationTests.cs" />
  </ItemGroup>
</Project>
```

The scoped verification command was:

```sh
dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj \
  --no-restore -m:1 /nodeReuse:false \
  -p:CustomAfterMicrosoftCommonTargets=/private/tmp/mc-nutrition-verification.targets \
  --filter 'FullyQualifiedName~GeneratedMealPostgresTests|FullyQualifiedName~GroupedMealsAndCustomFoodsRoundTripAgainstPostgreSql|FullyQualifiedName~NutritionSuggestionPortionTests'
```

`MC_REGISTRATION_WRITE_TEST_POSTGRES` must point to an explicitly disposable loopback database; the new persistence test additionally requires the `mc_test_nutrition_` prefix. The normal unmodified integration suite is not claimed to pass.

## Remaining acceptance

- Signed-in EN/RU inspection on a narrow iPhone in light/dark themes: button selection, breakfast/day-plan refresh, rapid toggles and connection-failure retry. Compilation and source review do not establish device acceptance.
- Preference durability remains unchanged. Reference ingredient allocations are estimates; the general portion solver retains existing acceptance tolerances.
- Any backend/client release, live migration or scheduler action belongs to a separately authorized release task.
