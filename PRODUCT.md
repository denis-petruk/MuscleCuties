# Product

## Platform

ios

## Users

Women who train for strength and also track nourishment, recovery and their menstrual cycle. They often train at home or in a gym with limited equipment, have a small number of reliable training days, and need the app to respect an injury, low readiness or a cycle-related symptom without turning that context into a diagnosis.

The primary job is to decide what to do next and record it with as little cross-app arithmetic as possible: open Today, see the next useful action, complete a workout set, log a meal, or check in.

## Product Purpose

MuscleCuties combines strength planning, workout logging, nutrition logging, cycle awareness and recovery context in one iOS app. It exists so a trainee does not have to reconcile a generic workout template, a calorie tracker and a cycle calendar by hand.

Success means a user can complete the daily loop — check in, train or rest, eat, and understand why the plan changed — without a dead end, an unsafe prescription, or a number they have to calculate elsewhere.

## Positioning

The plan is cycle- and injury-aware, deterministic and reviewed rather than generated as open-ended advice. A missing or stale field never relaxes a restriction or increases load. Unsafe strength work is withheld with a review reason instead of being replaced by an invented session.

The plan-acceptance path is equally specific: a feasible first foreground plan applies automatically; an active plan is replaced only through **Apply plan** after the replacement consequence is stated. There is no cached preview or failed API call that quietly substitutes for that decision.

## Operating Context

- Native .NET MAUI client, iOS only, with an ASP.NET Core API on AWS Lambda backed by PostgreSQL.
- The client is offline-capable: sensitive offline records live in encrypted JSON with keys in SecureStorage. There is no SQLite and no Preferences store for sensitive records.
- Five persistent destinations: **Today**, **Cycle**, **Train**, **Nutrition**, **You**.
- Sign-in, registration, OTP verification, profile setup and a quiz precede the main tabs for a new account.
- Apple Health is read-only and limited to steps, sleep stages, active kcal, resting HR and HRV.
- The client calls the server for account, plan generation, progression and USDA FoodData Central search. The USDA key never ships in the client.
- English and Russian, light and dark appearance, and Dynamic Type through the accessibility sizes are supported inputs, not post-release checks.

## Capabilities and Constraints

- **Workout planning:** generation requires a saved strength activity, a valid saved weekday set (1–7 distinct days) and every other field that influences the workout. Gaps are reported together with an action that opens the editable setup.
- **Safety:** injury exclusions and cycle context are applied before the preview. A cycle estimate alone never changes reps, sets or load; symptoms and readiness drive adjustments. A session with no permitted strength block is withheld with a review reason.
- **Logging:** strength sets capture reps, load, optional RIR and technique. Timed activities send duration and effort without invented sets. Edited rows are sent as observed sets; warm-up and empty placeholder rows are excluded.
- **Progression:** recommendations consume persisted observed sets against completed sessions and immutable prescriptions. Missing actual data is never filled from aggregates, and an incomplete recent exposure cannot earn a load increase.
- **Nutrition:** daily targets, meal logging, templates, suggestions, barcode/custom entry where implemented, hydration and a daily food log. The shared meal block is reused between Nutrition and Food Log.
- **Cycle:** logging, estimates, calendar context and phase explanations preserve uncertainty and privacy. Pain and red-flag handling is separate from ordinary soreness.
- **Deferred:** fasting has storage but no client surface and is not presented as available; the session screen renders text guidance only, with no exercise image or video placeholder.
- **Platform:** no Android target and no web client. C#/.NET is the only production runtime.
- **Terminology:** authored copy uses named localization keys; runtime English resolves through the runtime map. New copy is key-identical in English and Russian.

## Brand Commitments

Warm, reassuring and adult. The product supports women through training, nourishment, recovery and cycle awareness without shame, judgment or medical diagnosis. Strength training remains central; cycle and injury context never becomes decorative.

The approved visual direction is Soft Minimal — soft rose, warm neutral and semantic phase palettes — with one Operate posture across every screen. Visual authority is recorded in `DESIGN.md` and the presentation specification; changing the palette, type scale or geometry is an owner decision, not an incidental implementation choice.

## Evidence on Hand

- `DESIGN.md` and `docs/UI_PRESENTATION_DESIGN_SPEC.md` — owner-approved visual and component authority.
- `docs/PRODUCT_WORKFLOWS.md` — workout, progression, nutrition, Health and deferred-surface behaviour.
- `CONTEXT.md` — localization terminology.
- `README.md` — repository layout, build, test and deployment commands.
- `QA_REPORT.md` and `tests/` — existing verification evidence; use synthetic fixtures only.
- `ROADMAP.md` — current sequencing and open work.

No customer testimonials, logos, benchmarks, press or production datasets are present. Future work must not fabricate them.

## Product Principles

1. **The next useful action leads.** Every screen states what the user can do now before optional insight; known context is editable context, not a gate.
2. **Safety is not a preference.** A missing field, stale value or low-readiness signal may reduce or withhold work, never relax a restriction or increase load.
3. **One decision, one path.** The preview explains the week; **Apply plan** is the only replacement decision. There is no per-exercise acknowledgment or hidden fallback generator.
4. **Say a fact once.** Reassurance, units and next steps appear where they are needed, not repeated until they crowd out the action.
5. **Accessibility is the design.** Large text, the longer language, VoiceOver, Reduce Motion and both appearances are inputs to every layout, not a final verification pass.

## Accessibility & Inclusion

- Support Dynamic Type through the accessibility sizes; body and essential guidance must scale and wrap rather than truncate.
- Support VoiceOver with meaningful names, button traits, hints, logical focus order and a reachable primary action.
- Honor Reduce Motion with a static equivalent that carries the same information.
- Maintain 44-point minimum touch targets and 4.5:1 normal-text contrast (3:1 for qualifying large text and essential non-text indicators).
- English and Russian are both first-class; the longer string is the design case.
