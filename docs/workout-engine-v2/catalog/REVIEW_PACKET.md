# Catalog review packet — consolidated decisions

One review request covering every catalog item that needs owner judgement. Answering these eight items
unblocks V2 catalog activation; nothing else is pending. Anything not listed here is either already
reviewed or mechanically derived and asserted by tests.

## 1. Muscle-contribution rows (127 rows, all 60 definitions)

The seed carries direct (`1.0`) and indirect (`0.5`) contributions. The reviewed snapshot does not cover
them. Confirm they may be treated as the approved contribution model, or name the rows to change.

Decision needed: **approve as a bundle / amend specific rows.**

## 2. Ambiguous primary classification (5 exercises)

106 (Glutes+Hamstrings), 121 (Glutes+Quads), 123 (Glutes+Quads), 125 (Glutes+Quads), 126 (Glutes+Quads)
carry two direct contributions. The engine keeps both and reports the classification as ambiguous; it
does not invent a single primary. Confirm that dual-primary is acceptable, or supply the intended
primary per exercise.

## 3. Deltoid split

Lateral delts and rear delts are derived from movement pattern, not from a catalogued head. Pressing
currently credits `AnteriorDelts` only. Confirm that no pressing movement should credit lateral delts,
and that `Face Pull` (147) and `Reverse Pec Deck` (158) are the accepted direct rear-delt sources.

## 4. Equipment equivalence

No cross-equipment load equivalence exists and none is derived: a machine load never transfers to
another machine or a free-weight variant. Confirm this stays the rule for V2 (it is the current default
and is asserted).

## 5. Experience conditions (3 exercises)

112 Deficit RDL, 118 Good Morning, 172 Ab Wheel Rollout are labelled Established-only. Under V2 this is
a familiarity condition, not a blanket Beginner ban. Confirm these three must require an explicit
familiarity/technique acknowledgement before a Beginner receives them.

## 6. Timed-only movements (4 exercises)

136 Copenhagen Plank, 173 Hollow Hold, 177 Side Plank, 178 Suitcase Carry support timed work only. They
are inactive for repetition sets and active for timed strength/core work. Confirm that is intended, and
whether `Hollow Hold` and `Side Plank` should stay available at all given the dynamic-core preference.

## 7. Missing demand tags (2 exercises)

171 Dead Bug and 173 Hollow Hold were assessed with no demand tags. Confirm "assessed, no tags" or supply
tags — this affects body-zone restriction filtering only.

## 8. Home-equipment coverage — the only content blocker

Bodyweight-only profiles have no eligible `knee-dominant` or `upper-pull` candidate. Bands provide no
pulling option either; no reviewed movement assumes a band anchor or a pull-up bar for home use.

Decision needed: commission reviewed records for a bodyweight knee-dominant pattern (e.g. bodyweight
squat / reverse lunge / split squat) and a band pulling movement with an explicitly stated anchor
requirement, **or** accept an explicit coverage limitation for home users. Until reviewed candidates
exist, full home coverage is reported BLOCKED — it is never simulated with a placeholder movement.

---

## Not awaiting review

- `191 Rock Climbing` stays inactive and is not deleted; its historical definition is preserved.
- Rejected automatic substitutions stay excluded; manual alternatives never become automatic.
- Slot candidate sets remain exactly the reviewed sets.
