# P02 Governance and Protected Integration Ledger

Date: 2026-11-04
Recorded by: Integration Authority (Main Gemini CLI Session)
Target Branch: `platform/p02-governance`
Base SHA: `8f090cc25ec2f42bf1895a08aaeeaf0256d981b2` (P01 Candidate)

---

## 1. Governance Artifacts Implemented

- **`.github/CODEOWNERS`**: Created with granular ownership for `.github/`, `infra/`, schema migrations, `src/MuscleCuties.Core/`, `UseCases/`, and root architecture documentation bound to `@denis-petruk`.
- **`.github/PULL_REQUEST_TEMPLATE.md`**: Created with package tracking, architectural checks, safety contracts (workout planning, tri-state injuries, hydration, zero secrets), and verification evidence fields.

---

## 2. GitHub Branch Protection & Ruleset Specification for `main`

The following rule specification must be configured on GitHub for the `main` branch:

```json
{
  "name": "Protect main branch",
  "target": "branch",
  "enforcement": "active",
  "conditions": {
    "ref_name": {
      "include": ["refs/heads/main"]
    }
  },
  "rules": [
    {
      "type": "pull_request",
      "parameters": {
        "required_approving_review_count": 0,
        "require_code_owner_review": true,
        "dismiss_stale_reviews_on_push": true,
        "require_last_push_approval": false
      }
    },
    {
      "type": "required_status_checks",
      "parameters": {
        "strict_required_status_checks_policy": true,
        "required_status_checks": []
      }
    },
    {
      "type": "non_fast_forward"
    },
    {
      "type": "deletion"
    }
  ]
}
```

*Note on Solo-Owner Review*: As the repository has a single human maintainer (`@denis-petruk`), `required_approving_review_count` is set to 0 with PR required, ensuring every change goes through a reviewable PR without locking the solo owner out of merging. When independent review is available, it is required before merge.

---

## 3. Worktree & Reservation Model

- **Product Orchestrator (A):** Worktrees `product/*`, owns mobile UI, client adapters, domain use-cases, and behavioral fixtures.
- **Platform Orchestrator (B):** Worktrees `platform/*`, owns CI workflows, release tooling, schema migrations, and IaC.
- **Integration Authority (I):** Authoritative coordinator, exclusively holds shared contract reservations (`*Contracts.cs`), project/solution references, root governance, and migration sequence allocation.
