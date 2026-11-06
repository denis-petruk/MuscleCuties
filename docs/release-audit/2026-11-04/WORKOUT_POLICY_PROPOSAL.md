# Workout policy proposal

2026-11-04. **Proposed behavior, not implemented or clinically validated.** This document translates current user preferences and research into reviewable requirements. It supersedes no production rule until the owner/coordinator adopts a versioned policy and the implementation is verified.

The [full implementation plan](IMPLEMENTATION_PLAN.md) owns package dependencies. PD00 is the policy decision packet; WP01–WP05 below retain their named scopes, with WP02a/b and WP03a/b separating inventory/capture from selection/progression execution. DS00 owns shared compatible data seams. Do not interpret this proposal as production approval or a reason to block independent diagnostics, UI and platform work.

## Confirmed direction

- Continuous progression process; choose exercises by relevant growth evidence rather than favorites.
- Every main muscle gets at least two meaningful weekly exposures; prefer three when feasible.
- Short sessions use fewer high-quality, high-effort sets.
- Readiness adjusts the picked load. Denis requests cycle-aware lighter load, higher repetitions and more sets.
- Avoid exercises that trigger pain; preserve explicit restrictions and unknown safety values.
- Strength before cardio on combined days. **Cardio-only days are explicitly allowed.**

The following are implementation interpretations/recommendations, not additional preferences already accepted by Denis: progression can increase, hold or reduce work; a short session does not force maximal weight/failure; additional cycle sets should be conditional and must not defeat a fatigue/pain reduction. Exact numbers and this set-adjustment safeguard remain proposals requiring review. Current population source is general-fitness adults 18–64, pregnancy Yes unsupported; that boundary alone is not validation of every other health or hormonal condition.

## Evidence basis and limits

| Source | Finding relevant to policy | Limit |
| --- | --- | --- |
| [ACSM 2026](https://pmc.ncbi.nlm.nih.gov/articles/PMC12965823/) | Many resistance-training prescriptions work. Greater weekly volume can support hypertrophy; heavier loads favor strength. Failure is unnecessary for benefits. | Overview-level evidence does not validate this combined adaptive algorithm or an exact optimal RIR. |
| [Pelland, 2025 online / 2026 issue](https://link.springer.com/article/10.1007/s40279-025-02344-w) | Volume has diminishing returns; an independent frequency effect is less clear for hypertrophy. Fractional treatment of indirect sets fit better. | Mostly male studies; fractional contribution is an estimation method, not exact physiology. |
| [Robinson 2024](https://link.springer.com/article/10.1007/s40279-024-02069-2) | Closer estimated failure proximity was associated with growth. | Exploratory meta-regression and estimated RIR do not prove an exact best cutoff. |
| [Hermann 2025](https://pubmed.ncbi.nlm.nih.gov/40249908/) | One-set routines at failure or two RIR produced gains in trained adults over eight weeks. | Does not establish minimal volume as universally optimal. |
| [Zhang 2025](https://link.springer.com/article/10.1007/s40279-025-02176-8) | Supersets can save time with similar adaptations and higher perceived effort. | Protocols differ; pairing similar movements can impair work. |
| [Singer 2024](https://pubmed.ncbi.nlm.nih.gov/39205815/) | Rest review favored avoiding excessively short rests, with considerable uncertainty. | Not a universal timer prescription. |
| [Huang 2025](https://pubmed.ncbi.nlm.nih.gov/40791980/) and [sleep-loss review 2025](https://pubmed.ncbi.nlm.nih.gov/40236824/) | Autoregulation and sleep/performance evidence justify investigating load adjustment. | They do not validate a score-to-load formula or a universal sleep cutoff. |
| [D'Souza 2026](https://pubmed.ncbi.nlm.nih.gov/42160459/) | Phase-emphasized versus continuous training gave similar strength/growth in a small verified-cycle trial. | Does not erase individual symptoms or cover every hormonal condition. |
| [Munteanu 2026](https://link.springer.com/article/10.1007/s40279-026-02459-8) | Small phase-related performance differences; symptom effects varied by exercise. | No universal phase→weight/set rule; a readiness questionnaire is not a physiological measurement. |
| [Concurrent training review 2026](https://pubmed.ncbi.nlm.nih.gov/41762427/) | Resistance/endurance combinations are viable; sequence findings do not establish a universal necessity. | Strength-first is the owner's product preference, not a universal scientific law. |

Focused research covered literature available by November 4, 2026, using publisher/PMC papers and indexed abstracts. It was not an exhaustive systematic review. Exact defaults below are engineering/product proposals, not quoted study protocols. See the optional [research report](https://app.notion.com/p/40f47be9496581ccb0a5f440dd8b1236) for study populations, dates, exercise-specific findings and uncertainty.

## WP00: identify the active authority before changing numbers

Trace `PreviewStrengthPlanV020UseCase` → `StrengthPlanPreviewRule` → accepted prescription → active session → persisted actuals → next prescription. The standalone weekly generator and double-progression helper have no production callers located in the inspected src/services tree. Prove or disprove reachability on the actual candidate; do not wire an entire unused engine merely because its tests exist.

Inventory every active rule with: policy ID/version, caller, population/goal, input/provenance, current value, evidence/source date, evidence strength, owner preference, proposed change, persisted effect and acceptance cases. Reconcile legacy readiness weights/phase priors, Today check-in scoring, phase preview/Apply and automatic session reduction. One decision must own the final dose.

The production review reference currently uses a fixture identifier. Replace that with an authentic review record or leave the relevant dose-changing feature gated. A research document or renamed fixture is not approval provenance. Do not invent clinical sign-off or call heuristic thresholds clinical protocols.

## WP01: coverage, schedule and feasibility

Define main muscles explicitly: chest, lats/upper back, deltoid regions, quadriceps, hamstrings, glutes, biceps, triceps, calves and trunk. Review whether grouping regions hides an important gap. Abductors/adductors may be tracked additionally. Current “Arms” and one-primary-muscle bookkeeping are insufficient for precise claims.

Track direct and indirect work separately. A qualified exposure is meaningful working work, not warm-up, a listed exercise never performed, or an arbitrary secondary-muscle tag. Count at most one exposure per muscle per account-local day; two sessions on Tuesday do not equal two weekly days. Preserve planned and actually completed exposure counts independently. Exact indirect-set contribution and minimum exposure dose require review.

| Available strength days | Proposed scheduling approach |
| --- | --- |
| Two | Two full-body sessions; satisfy twice where safe/feasible. Explain why three is unavailable. |
| Three | Full-body A/B/C with complementary region coverage and distributed volume; preferred route toward three exposures. |
| Four | Upper/lower twice or a reviewed full-body distribution; choose by total coverage/time/recovery. |
| Five or six | Reviewed hybrid or upper/lower distribution can support a third exposure. Never require six days merely to satisfy the preference. |

These are examples, not approved templates or automatic weekly volume increases. A three-day plan distributes its target volume rather than multiplying a two-day dose by 1.5. A hard safety restriction can make coverage impossible: return an explicit limited/withheld outcome and permitted choices. Do not replace excluded strength work with mobility and count it as covered.

Treat real available time as a constraint. Include warm-up, work, rest, setup and transitions. If minimum coverage cannot fit, show the exact gap and alternatives such as another day or a longer session. Do not silently claim complete coverage, invent intensity compensation or hide omitted muscles behind “successful generation.” Keep canonical strength→cardio→recovery DayOrder throughout preview/persistence/UI. Cardio-only dates are valid.

## WP02: evidence-based exercise catalog

Apply hard eligibility first: pain triggers, restrictions, required safety answers, reviewed substitution conditions, valid equipment and feasible technique. Rank the remaining candidates by relevant longitudinal hypertrophy evidence, coverage, stable progression and setup/time feasibility. History/familiarity can represent learning cost, not a hidden favorites-first ranking.

Catalog evidence should name exact variation, muscle/head/region, ROM/position, study population, duration, outcome method, comparative versus efficacy-only status, source and confidence. Prefer MRI/ultrasound change over acute EMG/pump/soreness. Do not use an unvalidated decimal “science score,” infer all heads from one sampled site, or claim longest muscle length always wins.

| Region | Defensible shortlist implication | Primary source |
| --- | --- | --- |
| Glutes | Squats and hip thrusts are both supported; acute activation did not rank their measured growth. | [Plotkin 2023](https://www.frontiersin.org/journals/physiology/articles/10.3389/fphys.2023.1279170/full) |
| Quads | Include knee extension for rectus femoris coverage; less hip flexion favored sampled RF growth in one comparison. | [Larsen 2024/2025](https://www.tandfonline.com/doi/full/10.1080/02640414.2024.2444713) |
| Hamstrings | Cover knee flexion; longer-length eccentric work favored some muscles over Nordics in one specialized protocol. Ordinary equipment generalization is an inference. | [Maeo 2024](https://pmc.ncbi.nlm.nih.gov/articles/PMC11419281/) |
| Triceps | A tolerated overhead cable extension has direct comparative support, particularly for long head. | [Maeo 2022/2023](https://pubmed.ncbi.nlm.nih.gov/35819335/) |
| Calves | Straight-knee raises have stronger direct support for gastrocnemius than seated-only work in one trial; soleus findings were similar. | [Kinoshita 2023](https://pubmed.ncbi.nlm.nih.gov/38156065/) |
| Biceps | Neutral and shoulder-extended curls produced similar sampled growth when other mechanics matched; no automatic behind-body winner. | [Larsen 2026](https://www.frontiersin.org/journals/physiology/articles/10.3389/fphys.2026.1750722/full) |
| Lateral delts | Cable and dumbbell raises remain comparable options. | [Larsen 2025](https://pubmed.ncbi.nlm.nih.gov/40692697/) |
| Chest | Bench pressing has growth evidence; this efficacy trial establishes no universal best implement/angle. | [Borges 2024](https://pubmed.ncbi.nlm.nih.gov/39593465/) |
| Other back/deltoid/trunk regions | Use a reviewed coverage shortlist with explicit uncertainty; this search did not establish universal comparative winners. | Further bounded catalog review; no invented source. |

Keep exercises stable long enough to assess performance. When substitution is necessary, restart comparable exposure history rather than copy load/PR status between different implements or movement variants. Existing reviewed directed injury mappings remain authoritative until deliberately replaced through review.

## WP03: progression from actual work

Steady progression means evaluate each eligible exercise after valid completed work. Outcomes include more reps at comparable effort, a suitable load increment, better execution/ROM, hold, temporary reduction or recovery. It cannot guarantee more weight every session.

Proposed initial progression contract:

1. Store planned work and observed work separately, with exercise/equipment identity, date, policy version and session adjustment provenance.
2. Capture completed working sets, actual reps/load, an effort estimate or explicit Unknown, and a user report of execution difficulty/quality. A checkbox or default true is not verified technique.
3. When all required working sets meet the reviewed rep/effort criteria over the required comparable exposures, suggest an available equipment increment. Exposure count and reliability window are parameters to review, not assumed consecutive calendar weeks.
4. Keep or reduce load when appropriate; partial history, pain, changed exercise, invalid values or missing effort must not manufacture eligibility. Never punish a skipped week by silently treating it as a failed set.
5. Remove arbitrary simultaneous-muscle progression caps if no reviewed rationale supports them; retain reviewed constraints that prevent excessive total change.
6. Reconcile direct/indirect weekly volume and intensity together. Do not chase both volume and load automatically because separate helpers each recommend an increase.

Potential default for normal work is approximately 2–3 RIR; compact familiar exercises might use a lower reviewed target. These are proposals. No automatic failure, maximal attempts or unsafe forced reps. Do not apply low-readiness/injury sessions as evidence of baseline regression without preserving the temporary context. Actual per-set logging must reach the production progression path; helper-only fixtures cannot prove this.

## WP04: one readiness and cycle decision

Keep persistent baseline prescription distinct from a temporary session prescription. Return one composed result containing load, reps, sets, effort target, reason, input freshness/confidence and review/acceptance requirement. Prevent a second layer from reapplying the same adjustment.

Precedence proposal: explicit injury/pain restrictions → current safety concern → current symptoms and actual warm-up/performance → current check-in/recent recovery → established individual cycle response → calendar estimate. A high score cannot override a restricted movement. Unknown is not healthy/false, and an estimated phase is not measured hormone status.

Readiness should use recent comparable performance and a warm-up calibration to choose an appropriate load. Do not equate readiness percentage with weight percentage. Exact decrements, caps and recovery actions need review and validation; none is proven by the app's current 75/50 score thresholds.

Cycle-aware mode honors the requested lighter-load/higher-rep direction. Additional sets may be offered when tolerated within the reviewed weekly/time/recovery budget. A symptomatic or poor-readiness state must not automatically add sets to “compensate” for lower load. For example, 3×8×40 kg is 960 kg while 4×15×30 kg is 1,800 kg: reduced weight is not reduced total work. Tonnage itself still cannot establish physiological fatigue or safety.

Maintain baseline progression on well-tolerated days; use reported symptoms, confirmed preferences and repeated responses for personalization. Do not automatically lift heavier during a predicted ovulatory phase or demand identical phase behavior across people. Current research supports individualization, not a fixed phase-specific percentage/set schedule. Unknown/irregular cycles remain unknown.

Automatic conservative adjustments versus replacement Apply must follow a single adopted lifecycle. Preserve the existing tested first-plan acceptance exception and explicit replacement consent. A new policy must not silently rewrite an already accepted plan or historical session snapshots.

## WP05: pain-trigger handling

Provide a structured “This movement causes pain” action alongside existing swap/skip. Stop the provoking exercise; record relevant restriction provenance without diagnosing. Refilter reviewed alternatives, check the replacement itself against the trigger and clinician restrictions, and explain a safe available alternative or why work is withheld. No diagnosis from muscle name, no generic rehabilitation prescription, no automatic reintroduction because a timer expired. Clinical escalation language and any red-flag thresholds require their own qualified review.

## Definition of done

One adopted versioned policy; active caller map; evidence catalog with uncertainty; deterministic synthetic scenarios; real disposable PostgreSQL round trips; observed logging→next prescription proof; preview/Apply/idempotency and old-client compatibility; all-session scheduling; UI explanation and units; independent review; native acceptance on the same candidate. Record every acceptance case in the [full audit matrix](README.md). No source-only or literature-only conclusion establishes TestFlight readiness.
