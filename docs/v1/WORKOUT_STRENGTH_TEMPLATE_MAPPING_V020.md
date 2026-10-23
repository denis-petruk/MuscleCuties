# MC-WORKOUT-POLICY 0.2.0 strength template mapping

Template definitions are versioned as `MC-STRENGTH-TEMPLATES-0.2.0` in
`StrengthTemplateCatalogV020`. Slot resolution uses catalog movement patterns, equipment, stable IDs,
user exclusions, reviewed v0.2 eligibility metadata, existing selections, performance history, and
user preferences in that order. Exercise IDs are never selected randomly.

## Templates and required coverage

| Template ID | Split | Required slots | Explicit optional slots |
| --- | --- | --- | --- |
| `MC-FB-1D-A` | One-day full body | Knee-dominant; hip-dominant; upper push; upper pull (A-pattern preferences) | Trunk; goal accessory |
| `MC-FB-2D-A` | Two-day full body A | Knee-dominant; hip-dominant; upper push; upper pull (A-pattern preferences) | Trunk; goal accessory |
| `MC-FB-2D-B` | Two-day full body B | Knee-dominant; hip-dominant; upper push; upper pull (B-pattern preferences) | Trunk; goal accessory |
| `MC-FB-3D-A` | Three-day full body A | Knee-dominant; hip-dominant; upper push; upper pull (A-pattern preferences) | Trunk; goal accessory |
| `MC-FB-3D-B` | Three-day full body B | Knee-dominant; hip-dominant; upper push; upper pull (B-pattern preferences) | Trunk; goal accessory |
| `MC-FB-3D-C` | Three-day full body C | Knee-dominant; hip-dominant; upper push; upper pull (C-pattern preferences) | Trunk; goal accessory |
| `MC-UL-4D-UPPER-A` | Four-day upper/lower A | Horizontal push; horizontal pull | Trunk; goal accessory |
| `MC-UL-4D-LOWER-A` | Four-day upper/lower A | Squat-pattern knee; hip hinge | Trunk; goal accessory |
| `MC-UL-4D-UPPER-B` | Four-day upper/lower B | Vertical push; vertical pull | Trunk; goal accessory |
| `MC-UL-4D-LOWER-B` | Four-day upper/lower B | Lunge knee; hip thrust | Trunk; goal accessory |

| Training style | Required slot designation | Optional slot designation |
| --- | --- | --- |
| General strength | Main | Accessory |
| High-volume strength | Main | Accessory |
| High-intensity full body | Knee and hip: heavy main; push and pull: accessory | Accessory |
| High-intensity upper | Push: heavy main; pull: accessory | Accessory |
| High-intensity lower | Knee and hip: heavy main | Accessory |

Variant letter patterns are preferences inside each movement category; all catalog patterns in that
category remain eligible when equipment, restrictions, history, or preferences make another match
more appropriate. High-intensity strength labels knee and hip slots as heavy mains in full-body and
lower sessions; upper push is a heavy main on upper sessions. Remaining required slots are accessories.
This keeps each session to at most two heavy main movements. Optional slots are removed before
required coverage. Four hard sessions remain infeasible under the three-hard-days-per-week cap and
must produce the explicit three-day alternative. The one-day full-body template covers each
required movement slot once; the resulting plan must disclose this limited weekly coverage.

## Catalog candidates by slot

IDs and names below come from the existing PostgreSQL catalog seed (`AppDatabaseWorkoutPlanningSeed`).
They are pattern-matched candidates, not yet eligible v0.2 prescriptions. Eligibility is determined
from the user's actual equipment and restrictions at request time.

| Slot / movement pattern | Catalog candidates | Required equipment |
| --- | --- | --- |
| Knee, `SquatPattern` | 121 Back Squat; 122 Hack Squat; 123 Leg Press; 124 Goblet Squat | Barbell; machine; machine; dumbbell |
| Knee, `Lunge` | 125 Bulgarian Split Squat; 126 Walking Lunge; 127 Step-Up; 128 Reverse Lunge | Dumbbell |
| Hip, `HipHinge` | 105 Cable Pull-Through; 106 45 Degree Back Extension; 111 Romanian Deadlift; 112 Deficit RDL; 113 Dumbbell RDL; 114 Single-Leg RDL; 118 Good Morning | Cable; back-extension station; barbell; barbell; dumbbell; dumbbell; barbell |
| Hip, `HipThrust` | 101 Barbell Hip Thrust; 102 Machine Hip Thrust; 103 B-Stance Hip Thrust; 104 Single-Leg Glute Bridge; 107 Kneeling Cable Kickback | Barbell + hip-thrust bench; machine; barbell + hip-thrust bench; bodyweight; cable |
| Upper push, `HorizontalPush` | 153 Incline DB Press; 154 Machine Chest Press; 155 Push-Up | Dumbbell; machine; bodyweight |
| Upper push, `VerticalPush` | 151 DB Shoulder Press; 152 Machine Shoulder Press | Dumbbell; machine |
| Upper pull, `HorizontalPull` | 143 Chest-Supported Row; 144 Seated Cable Row; 145 Single-Arm DB Row | Machine + dumbbell; cable; dumbbell |
| Upper pull, `VerticalPull` | 141 Pull-Up; 142 Lat Pulldown; 146 Straight-Arm Pulldown | Pull-up bar; machine + cable; cable |
| Trunk, `AntiExtension` | 171 Dead Bug; 172 Ab Wheel Rollout; 173 Hollow Hold; 174 Cable Crunch | Bodyweight; bodyweight; bodyweight; cable |
| Trunk, `AntiRotation` | 175 Pallof Press; 176 Bird Dog | Cable; bodyweight |
| Trunk, `AntiLateralFlexion` | 177 Side Plank; 178 Suitcase Carry | Bodyweight; dumbbell |
| Goal accessory | 115 Seated Leg Curl; 116 Lying Leg Curl; 117 Nordic Curl; 131 Seated Hip Abduction Lean; 132 Seated Hip Abduction Upright; 133 Standing Cable Abduction; 134 Banded Lateral Walk; 135 Side-Lying Abduction; 136 Copenhagen Plank; 137 Seated Adduction Machine; 147 Face Pull; 156 Cable Lateral Raise; 157 DB Lateral Raise; 158 Reverse Pec Deck; 161 Incline DB Curl; 162 Cable Curl; 163 Overhead Cable Extension; 164 Rope Pushdown; 181 Standing Calf Raise; 182 Seated Calf Raise | Machine; machine; bodyweight; machine; machine; cable; band; bodyweight; bodyweight; machine; cable; cable; dumbbell; machine; dumbbell; cable; cable; cable; machine; machine |

The existing exercise primary-muscle values are preserved in the catalog. The PostgreSQL seed mirrors
the strongest catalog muscle-contribution row onto each `Exercise.PrimaryMuscle`; in the candidate
order above, primary muscles are:

- IDs 101–107: Glutes; 111–118: Hamstrings.
- IDs 121–124: Quads, Glutes, Glutes, Quads; 125–128: Glutes.
- IDs 131–135: GluteMed; 136–137: Adductors.
- IDs 141–142: Lats; 143–145: UpperBack; 146: Lats; 147: SideShoulders.
- IDs 151–152: SideShoulders; 153–155: Chest; 156–158: SideShoulders.
- IDs 161–162: Biceps; 163–164: Triceps; 171–178: Core; 181–182: Calves.

## v0.2 prescriptions

| Activity and slot role | Sets | Repetitions | Effort | Rest |
| --- | ---: | --- | ---: | ---: |
| General strength main, beginner/returning/unknown | 2 | 8–12; initial target 8 | 3 RIR | 120 sec |
| General strength main, established | 2 | 6–12; initial target 6 | 3 RIR | 150 sec |
| General strength accessory | 2 | 10–15; initial target 10 | 3 RIR | 90 sec |
| High-volume main (established resistance history only) | 3 | 8–12; initial target 8 | 3 RIR | 150 sec |
| High-volume accessory | 2 | 10–15; initial target 10 | 3 RIR | 90 sec |
| High-intensity heavy main (per-exercise prerequisites met) | 3 | 4–6; initial target 4 | 3 RIR | 180 sec |
| High-intensity accessory | 2 | 10–15; initial target 10 | 3 RIR | 90 sec |

High-volume sessions keep at most five working exercises. They never add work merely to reach a
ceiling, and weekly direct sets per primary muscle stay at or below ten. High-intensity main slots
require four completed exposures to that exact exercise, a recorded manageable load, and current
user-confirmed familiarity. A missing load remains unset.

## Catalog gaps blocking v0.2 eligibility

The existing catalog supplies the IDs, names, movement patterns, required equipment, setup time,
legacy contraindication flags, and some substitution groups. Those fields alone do not establish the
new eligibility data. Migration `20261023140000_AddWorkoutExerciseEligibilityMetadataV020` adds
nullable fields for:

- Catalog version.
- Explicitly supported activity experiences.
- Actual-content demand tags and a reviewed flag.
- Reviewed restriction conflict tags and a reviewed flag.
- Supported timing methods.
- Reviewed substitute exercise IDs.

Existing seed rows intentionally receive no fabricated v0.2 values. Until those nullable fields are
populated and reviewed, the resolver returns specific catalog gaps and does not label any candidate
eligible. In particular, no eligibility is inferred from an exercise name, old skill score, or
injury-friendly label. Existing `Contraindications` remain explicit catalog exclusions when a user
has a matching active restriction.

The catalog also lacks per-activity self-reported history, per-exercise familiarity confirmation,
and accepted v0.2 slot IDs. The current v0.2 inputs use explicit activity context, per-exercise
performance counts/loads, and user confirmation; missing evidence blocks only the affected strength
style or selected main exercise.

## Integration gaps still requiring application work

- The active `/api/v1/workouts/plan` path still calls the legacy four-week generator; this resolver
  is not yet called by proposal creation or acceptance.
- No persisted schedule input currently supplies actual available-day windows, so optional slot
  removal by remaining session minutes and a feasible duration alternative are not yet connected.
- Weekly allocation still needs to limit high-intensity sessions to two, assign any additional strength
  days the general-strength prescription, and enforce the three-hard-day cap. The four-day template
  definitions do not by themselves make four prescribed hard days feasible.
- The high-volume ten-direct-set ceiling requires aggregation by each exercise's primary muscle over
  the full selected week. The current per-session resolver does not yet receive that weekly aggregate.
- Proposal/revision tables exist, but no transaction currently persists slot IDs, selected IDs,
  reasons, policy values, or revalidates acceptance against the current revision.
- The repository test command currently cannot run on this machine because only .NET SDK 9.0.318 is
  installed; the solution targets .NET 10.
