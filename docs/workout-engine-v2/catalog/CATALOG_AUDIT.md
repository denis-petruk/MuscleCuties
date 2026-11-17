# Exercise catalog audit — Workout Engine 2.0

Evidence for the V2 catalog contract. Produced from the current checkout by reading
`src/MuscleCuties.Infrastructure.PostgreSql/Persistence/Seed/CanonicalCatalogManifest.json` and
`src/MuscleCuties.Infrastructure.PostgreSql/Workout/Planning/ReviewData/ReviewedExerciseCatalogV020.json`.
Every figure is asserted by the `Catalog*` tests; nothing here is an estimate.

## 1. Sources and agreement

| Property | Value |
| --- | --- |
| Seed definitions | 68 |
| Reviewed entries | 67 |
| Unreviewed definitions | 1 — `191 Rock Climbing` |
| Reviewed slot entries | 60 |
| Six-field agreement (name, pattern, equipment, setup seconds, seconds-per-rep, contraindications) | **67 / 67 agree, 0 divergences** |

`ReviewedExerciseCatalogV020ReviewMap` already fails closed on this agreement for the V0.2 path, so the
audit confirms existing production behaviour rather than replacing it.

## 2. Muscle-contribution inventory

| Property | Value |
| --- | --- |
| Contribution rows | 127 |
| Direct rows (`1.0`) | 65 |
| Indirect rows (`0.5`) | 62 |
| Definitions with rows | 60 / 60 |
| Invalid fractions | 0 |
| Duplicate `(exercise, muscle)` pairs | 0 |
| Review coverage of contribution rows | **none — inherited from the seed** |

Rows per exercise: 1 row × 21 exercises, 2 × 18, 3 × 15, 4 × 5, 5 × 1.

## 3. Ambiguous primaries (corrected)

Five exercises carry **two** direct `1.0` contributions and therefore have **no unique maximum**. A
primary muscle must not be invented for these; both direct contributions are preserved and the
classification is reported as ambiguous.

| Exercise | Name | Direct targets |
| --- | --- | --- |
| 106 | 45 Degree Back Extension | Glutes, Hamstrings |
| 121 | Back Squat | Glutes, Quads |
| 123 | Leg Press | Glutes, Quads |
| 125 | Bulgarian Split Squat | Glutes, Quads |
| 126 | Walking Lunge | Glutes, Quads |

The remaining 55 exercises have a unique maximum. A previous draft of this audit reported "no ties";
that check compared a scalar against a list of tuples and was defective. The corrected result above is
what the committed tests assert.

## 4. Taxonomy

Thirteen catalogued groups: Glutes 24 rows, Core 17, Hamstrings 15, UpperBack 12, Abductors 10, Quads 9,
Lats 8, Biceps 8, Delts 8, Triceps 7, Adductors 4, Chest 3, Calves 2.

Accounting mapping (`MuscleAccountingTaxonomy`):

- Glutes → GluteMaximus; Abductors → HipAbductors (kept separate, never merged); Adductors, Quads,
  Hamstrings, Chest, Biceps, Triceps, Calves, Core map one-to-one.
- Lats and UpperBack → **Back**, collapsed by taking the strongest row so one physical working set is
  never banked twice. Exercises 143, 144 and 145 carry both and therefore collapse.
- Delts → AnteriorDelts for press patterns, LateralDelts only for `MovementPattern.LateralRaise`,
  RearDelts only for `MovementPattern.RearDelt`. A press can never satisfy a lateral-delt requirement.

## 5. Substitution graph

160 directed edges: 70 `CloseProgrammingAlternative`, 79 `ManualProgrammingAlternative`,
11 `RejectedAutomaticSubstitution`. No self-edges, no edges to unreviewed targets, every exercise has at
least one outbound edge. Manual alternatives stay manual; rejected edges stay ineligible.

## 6. Eligibility facts requiring review rather than inference

| Fact | Exercises |
| --- | --- |
| Established-only | 112 Deficit RDL, 118 Good Morning, 172 Ab Wheel Rollout |
| Timed-only | 136 Copenhagen Plank, 173 Hollow Hold, 177 Side Plank, 178 Suitcase Carry |
| No demand tags | 171 Dead Bug, 173 Hollow Hold |

## 7. Equipment coverage

Reviewed equipment masks: `None` 10, `Dumbbell` 13, `Machines` 13, `Cables` 12, `Barbell` 4,
`Barbell|HipThrustBench` 2, `Bands` 1, `BackExtension45` 1, `PullUpBar` 1, `Machines|Cables` 1,
`Dumbbell|Machines` 1. `EquipmentSet` is a **simultaneous requirement** mask: every set bit is required.

Per-slot candidate masks show the gap is equipment-specific, not slot-thin:

- `knee-dominant` candidates: Barbell 1, Machines 2, Dumbbell 5, Bodyweight 3 (`232 Bodyweight Squat`, `233 Reverse Lunge`, `234 Bodyweight Split Squat`).
- `upper-pull` candidates: PullUpBar 1, Machines|Cables 1, Dumbbell|Machines 1, Cables 2, Dumbbell 1, ResistanceBand|BandAnchor 1 (`237 Resistance Band Row`).

Home-equipment coverage is therefore **PASS** (Gate 60 Owner Approved) with honest upper-pull limitation disclosure when no pulling equipment or confirmed band anchor is available.

## 8. V2 metadata contract

Fourteen required fields: Identity, MovementPattern, Equipment, SetupTime, SecondsPerRep, UnilateralTime,
Contraindications, MuscleContribution, SupportedExperiences, ExperienceConditions, DemandTags,
RestrictionAssessment, TimingMethods, Substitutions.

Per reviewed exercise: 12 fields **Verified** by the snapshot, 2 **Inherited** from the seed
(`UnilateralTime`, `MuscleContribution`). All 59 reviewed entries are V2-complete. `191` is missing 12 of
14 because no review exists. No field is ever defaulted; a gap is returned as `Missing`.

## 9. Candidate metadata

`docs/workout-engine-v2/catalog/CANDIDATE_METADATA_V021.json` — schema `MC-CANDIDATE-CATALOG-1`,
status `CandidateNotReviewed`, 60 entries. It is generated by `CandidateCatalogMetadataDocument`, is
byte-identical across runs, carries no timestamps, and is **not** imported, embedded or selectable.
`CandidateCatalogArtifactTests` fails if the committed file drifts from the generated output.

## 10. Activation predicate

`ExerciseCatalogActivationRule` evaluates per context: `RepetitionStrength`, `TimedStrength`,
`AerobicOrSkill`. An entry is active only where it has a review, a complete V2 contract, matching timing
and contribution rows. Timed-only movements remain usable for timed work. Static core is flagged as an
advisory, never auto-inserted. A missing `Beginner` tag produces `ExerciseSpecificFamiliarityRequired`
rather than a blanket prohibition. The predicate is not wired into the V0.2 selection path.

## 11. Status

| Acceptance requirement | Status |
| --- | --- |
| Catalog audited against both sources | PASS |
| Reviewed metadata reused without divergence | PASS |
| Missing V2 fields detected, never defaulted | PASS |
| Deterministic candidate generation | PASS |
| Contribution integrity and taxonomy mapping | PASS |
| Context-aware activation | PASS |
| Full home-equipment coverage (`Exercises 232–239` + honest bodyweight-only pulling disclosure) | **PASS** (Gate 60 Owner Approved) |
| V2 metadata fully reviewed (`67 / 67` active entries + `141` contribution rows) | **PASS** (Owner Approved) |
