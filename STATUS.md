# Status — Workout Engine 2.0: V2 exercise catalog audit and metadata contract

Branch `fix/qa-audit-followups`. Baseline HEAD before this task: `5cecd0eb`.

## Changed files

New Core rules under `src/MuscleCuties.Core/Domain/Workout/Planning/Catalog/`:

- `ExerciseCatalogMetadataContract.cs` — 14 required V2 fields with `Verified` / `Inherited` / `Missing`
  provenance. Nothing is defaulted; a gap is returned as `Missing`.
- `ExerciseCatalogAuditRule.cs` — six-field source agreement, contribution integrity (duplicate pairs,
  invalid fractions, ambiguous maximum), and the accounting taxonomy that collapses Lats+UpperBack to one
  Back credit and splits deltoids by movement pattern.
- `ExerciseCatalogActivationRule.cs` — context-aware activation (`RepetitionStrength`, `TimedStrength`,
  `AerobicOrSkill`); timed-only movements stay usable for timed work, static core is advisory only, and a
  missing experience tag raises a familiarity condition instead of a ban.
- `CandidateCatalogMetadataDocument.cs` — canonical JSON builder: fixed key order, invariant formatting,
  no timestamps.

Tests in `tests/MuscleCuties.Core.Tests/`: `CatalogAuditFixture.cs`, `CatalogMetadataContractTests.cs`,
`CatalogContributionIntegrityTests.cs`, `CatalogActivationAndDeterminismTests.cs`,
`CandidateCatalogArtifactTests.cs`.

Generated artifact: `docs/workout-engine-v2/catalog/CANDIDATE_METADATA_V021.json` (60 entries, status
`CandidateNotReviewed`, reproducible and drift-guarded by test).

Documents: `docs/workout-engine-v2/catalog/CATALOG_AUDIT.md`,
`docs/workout-engine-v2/catalog/REVIEW_PACKET.md`, and this file.

## Verification

```
$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release --filter "FullyQualifiedName~Catalog"
Passed!  - Failed:     0, Passed:    85, Skipped:     0, Total:    85, Duration: 143 ms

$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release
Passed!  - Failed:     0, Passed:  1500, Skipped:     0, Total:  1500, Duration: 624 ms
```

Corrected audit facts now asserted by tests: 60 definitions, 59 reviewed, 127 contribution rows
(65 direct / 62 indirect), no duplicate `(exercise, muscle)` pairs, no invalid fractions, and **five**
exercises with two direct contributions and therefore no unique maximum (106, 121, 123, 125, 126). An
earlier draft claimed "no ties"; that check compared a scalar against a list of tuples and was wrong.

## Not verified

- No PostgreSQL-backed suite ran, so the catalog importer and review-map path are untouched but not
  exercised against a database in this task.
- No simulator or device run: no UI surface changed.
- The V2 activation predicate is **not wired** into `ExercisePlanPreview`/`ExerciseEligibilityRule`, so
  V0.2 production selection behaviour is unchanged; only unit-level decisions exist.
- Assignment of the 127 contribution rows remains unreviewed by the snapshot — the audit treats them as
  `Inherited`, never `Verified`.
- Full home-equipment coverage is BLOCKED: no reviewed bodyweight knee-dominant or band pulling movement.

## Next step

Owner decision on the eight items in `docs/workout-engine-v2/catalog/REVIEW_PACKET.md`, then W2 muscle
accounting over the 59 valid entries with synthetic fixtures.
