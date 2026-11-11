# Journey log — iPhone 17 Pro, synthetic user (2026-11-10)

Local API: 127.0.0.1:5080, Development, DB mc_qa_20261109 (loopback, disposable).
Synthetic accounts only; no real email sent (dev delivery prints codes to API output).

| Step | Result | Evidence |
|---|---|---|
| Launch to sign-in | PASS | screenshots/launch-iphone17pro.png |
| Registration form | PASS | (form accepted, email/username checks 200) |
| Email code verify | PASS after restart | API log: start 200, verify 200 |
| Resend code after API restart | FAIL (400) — challenge from prior API process invalid after restart; expected dev-only behavior, needs re-register | API log |
| Onboarding step 1 (birthday, height, weight, wellness ack, activities) | PASS with notes | screenshots/after-verify2.png … ack.png |
| Onboarding step 2 quiz (12 Q) | IN PROGRESS: Q1–2 answered | screenshots/step2.png, q2.png |

## Defects / UX findings (not yet fixed)
1. Quiz questions are English while answer labels are mixed (Q1 Russian answers, Q2 English answers). Localization gap.
2. Activity list titles/descriptions are partly English ("High volume strength", "Plan or log walking time.").
3. Height/weight use wheel pickers; typed input is ignored (picker only).
4. Cycle-phase question is asked of the synthetic profile without a sex/eligibility gate — needs checking against docs/PRODUCT_WORKFLOWS.md before judging.

## Not verified
Quiz Q3–12, workout generation, logging, readiness, safety, cycle personalization, nutrition, returning-user flows.
