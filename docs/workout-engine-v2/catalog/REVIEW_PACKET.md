# Catalog review packet — consolidated decisions (CLOSED — OWNER APPROVED)

All eight review items and the Gate 60 home/bodyweight/band expansion have received final owner approval and are active in Workout Engine 2.0 (`MC-CATALOG-V2.0`).

## 1. Muscle-contribution rows (141 rows across 67 reviewed definitions + `191` inactive)

**Owner decision: APPROVED.** The audited contribution rows (`1.0` direct, `0.5` indirect) are active as the initial versioned programming model.

## 2. Ambiguous primary classification (5 exercises)

**Owner decision: APPROVED.** Exercises 106 (Glutes+Hamstrings), 121 (Glutes+Quads), 123 (Glutes+Quads), 125 (Glutes+Quads), and 126 (Glutes+Quads) retain dual direct contributions without fabricating a single primary.

## 3. Deltoid split

**Owner decision: APPROVED.** Lateral delts (`MovementPattern.LateralRaise`) and rear delts (`MovementPattern.RearDelt`) are derived deterministically by movement pattern; pressing movements credit `AnteriorDelts` directly (`Face Pull` 147 and `Reverse Pec Deck` 158 provide direct rear-delt coverage).

## 4. Equipment equivalence

**Owner decision: APPROVED.** No cross-equipment load equivalence exists or is derived; switching between machine, cable, band, or free-weight variants requires exercise-specific load calibration.

## 5. Experience conditions (3 exercises)

**Owner decision: APPROVED.** Exercises 112 Deficit RDL, 118 Good Morning, and 172 Ab Wheel Rollout use exercise-specific familiarity conditions rather than blanket Beginner bans.

## 6. Timed-only movements (4 exercises)

**Owner decision: APPROVED.** Exercises 136 Copenhagen Plank, 173 Hollow Hold, 177 Side Plank, and 178 Suitcase Carry remain eligible for timed contexts while dynamic core (`171 Dead Bug`, `170 Cable Pallof Press`, `172 Ab Wheel Rollout`, `176 Hanging Knee Raise`) is prioritized by default.

## 7. Missing demand tags (2 exercises)

**Owner decision: APPROVED.** Intentionally empty demand tags on low-impact core movements remain valid subject to existing safety and contraindication rules.

## 8. Home-equipment coverage — Gate 60 CLOSED

**Owner decision: APPROVED.**
- Eight new reviewed home/bodyweight/band exercises are active in `CanonicalCatalogManifest.json`, `ReviewedExerciseCatalogV020.json`, and `CANDIDATE_METADATA_V021.json`:
  - `232` `Bodyweight Squat` (`SquatPattern`, `Bodyweight`)
  - `233` `Reverse Lunge` (`Lunge`, `Bodyweight`, unilateral per-leg)
  - `234` `Bodyweight Split Squat` (`Lunge`, `Bodyweight`, unilateral per-side)
  - `235` `Floor Glute Bridge` (`HipThrust`, `Bodyweight`)
  - `236` `Slider Leg Curl` (`KneeFlexion`, requires confirmed `Sliders`)
  - `237` `Resistance Band Row` (`HorizontalPull`, requires `ResistanceBand` + confirmed `BandAnchor`)
  - `238` `Resistance Band Chest Press` (`HorizontalPush`, requires `ResistanceBand` + confirmed `BandAnchor`)
  - `239` `Resistance Band Biceps Curl` (`ElbowFlexion`, `ResistanceBand`, anchorless)
- **Owner-accepted home coverage limitation:** A bodyweight-only user without pulling equipment or a confirmed band anchor has no viable upper-body pulling movement; the engine generates all safe lower/push/core work and explicitly discloses `WeeklyCoverageMissing_UpperPull_Equipment` / `WeeklyCoverageMissing_UpperPull_BandAnchorRequired` with an equipment-setup action.

---

## Not awaiting review

- `191 Rock Climbing` stays inactive and is not deleted; its historical definition is preserved.
- Rejected automatic substitutions stay excluded; manual alternatives never become automatic.
- Slot candidate sets remain exactly the reviewed sets.
