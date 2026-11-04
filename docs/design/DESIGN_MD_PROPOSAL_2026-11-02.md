# Proposal — changes to DESIGN.md

**Status: approved and applied, 2026-11-02.** The owner approved all seven proposals; `DESIGN.md` was rewritten with them and the specification gained the matching lines. This file is retained as the record of what was proposed, what was approved, and which later restyling work each rule implies. Future amendments belong in a new proposal rather than as edits to this one.

**Why the proposal exists.** The polish pass exposed three rules the app needs that currently live only in the [presentation specification](../UI_PRESENTATION_DESIGN_SPEC.md). `DESIGN.md` is the file every agent reads first, so a rule that exists only in the specification cannot prevent the drift it is meant to prevent. The four owner screenshots of 2026-11-02 show that drift concretely: three selection languages on one screen, three privacy statements on another, and a placeholder occupying half a viewport. Those are not styling mistakes; they are missing direction.

Each proposal below states the current text, the exact replacement, the evidence that motivates it, and whether it is a documentation-only change or implies later restyling work.

## Summary

| ID | Section | Kind | Evidence | Later work implied |
| --- | --- | --- | --- | --- |
| P1 | New `Expression` | New principle | Vault void; uniform density across dense rows and quiet states | Yes — density stratification |
| P2 | New `Selection and status` | New principle | Week chips filled vs filter chips outlined vs day tiles tinted | Yes — one recipe per family |
| P3 | `Language and copy` | Added rule | Vault privacy claim stated three times; "Rest day" + "Recovery day" | Yes — copy de-duplication |
| P4 | `Layout` | Added rule | Vault title wrapping into the action tile | Yes — header/frame consolidation |
| P5 | New `Adaptation` | New principle | Food-log label wrapping into a fixed column | Yes — wrap-capable layouts |
| P6 | New `Motion` | New principle | Specification owns the reduced-motion branch, DESIGN.md is silent | No — no animation exists yet |
| P7 | `Do's and Don'ts`, intro | Governance + two bullets | This workflow itself | No |

All seven are documentation changes. None changes a token value, a style definition, or a screen today.

## P1 — Expression posture (new section)

**Current:** absent. `DESIGN.md` describes palette, type, layout and components, but says nothing about how much expression a screen may carry or how dense it should be.

**Proposed:** new `## Expression` section, placed after `## Overview`:

> **One posture: Operate.** Every screen exists to complete a task. Expression comes from precision — alignment, spacing, weight and restraint — not from decorative layout. A quiet screen must still be informative; do not fill absence with oversized surfaces.
>
> **Density is stratified, not uniform.** Repeated records — food rows, set entries, history — are compact and scannable. Summary, loading and empty states are calm, with whitespace that frames the next action. Density never falls below readable text or the specification's touch minimums, and never rises by shrinking essential text.
>
> **Variance is deliberately low.** A new screen should look like a member of the existing set. If a layout needs to be explained as "the special one", the design is wrong, not the budget.

**Why:** the vault's empty state is a full-height card holding one sentence — absence of information presented as breathing room. Meanwhile the food log packs five date controls into the top third of the screen. One file should state that both are wrong for the same reason.

**Boundary:** this section states posture only. Which spacing role applies where stays in the specification; no values are added to `DESIGN.md`.

## P2 — Selection and status (new section)

**Current:** one paragraph in `Language and copy` assigns the *accessible* selected hint to `CardSecondary`. Nothing owns the *visual* selection recipe, which is why one screen expresses selection three ways.

**Proposed:** new `## Selection and status` section after `Language and copy`:

> **Selected** has one visual recipe per component family, and it comes from the style, not from the page. A screen must not express selection differently in two of its own groups: week pickers, filters and day tiles shown together are one language. Selection is always carried by more than colour — weight, stroke, indicator or explicit label.
>
> **Status** — upcoming, in progress, complete, required — occupies a stable position and uses one treatment across screens. A status is a short label, never a sentence, and never tint alone. A qualifier such as "required" appears once per group, not on every row of the group.

**Why:** the activity schedule shows filled week chips, outlined filter chips and tinted day tiles within one viewport. Each is defensible alone; together they teach the user nothing. The repeated "Required session" chip on four consecutive rows is the same failure in copy form.

**Boundary:** this does not add a style. It reduces three page-local treatments toward the two permitted families.

## P3 — One statement per fact (`Language and copy` addition)

**Current:** the section covers resolution, key naming and selection semantics.

**Proposed:** append:

> **State a fact once per screen.** A fact — a privacy guarantee, a rest instruction, what comes next, a unit convention — appears in one place. Repeated reassurance reads as uncertainty and consumes the space that should hold the next action. Before adding a line, check whether the header, a card or an empty state already says it.

**Why:** the vault says the same privacy claim in the subtitle, the privacy card and the empty state, while the rest-day card says both "Rest day" and "Recovery day". Both screens have room for a fact they are not currently stating.

## P4 — Header ownership (`Layout` addition)

**Current:** "Every page uses the same page/header/body/optional-footer frame… Content and navigation state vary; page styling does not."

**Proposed:** append to `## Layout`:

> **The shared frame composes the header.** Title, back affordance and the single contextual action come from the shared header; a page supplies content, not its own header composition. A title that needs two lines reflows without displacing or resizing the content beneath it, and the header action stays reachable when the title grows.

**Why:** the vault header wraps to two lines and pushes a tall action tile, producing a header whose weight exceeds the content below. The rule that "page styling does not vary" exists but does not name the header as its most-violated part.

## P5 — Adaptation as a design input (new section)

**Current:** the specification requires a large-text fallback for grids; `DESIGN.md` says only "Text must scale and wrap".

**Proposed:** new `## Adaptation` section after `## Layout`:

> **Large text and translation are design inputs, not verification.** Every layout is designed for the largest supported text size, the longer of English and Russian, and both appearances before it is reviewed as finished. No essential label sits in a fixed-width column, in a single-line container that cannot grow, or beside an element with a fixed size. Where a grid genuinely cannot adapt, its documented fallback is part of the design.

**Why:** the copy-yesterday action is a label in a fixed column of a two-column grid. When the localized key resolved to a long string it wrapped into three fragments beside a full-size button. The layout, not the string, was the defect.

## P6 — Motion stance (new section)

**Current:** absent from `DESIGN.md`; the reduced-motion contract lives only in the specification.

**Proposed:** new `## Motion` section before `## Components`:

> **Motion explains state; it never decorates.** Native transitions are the default. Custom motion is reserved for state the user just caused — selection, save, sheet enter and exit — short enough to read as feedback, with a reduced-motion branch that carries the same information statically. If the information exists only in the animation, the design is incomplete.

**Why:** no custom motion ships yet. Stating the stance now is cheaper than retrofitting a reduced-motion branch onto shared sheet behaviour later, which is already the audit's P1 finding.

## P7 — Governance and two guardrails

**Proposed:** one line in the introduction, and two bullets under `## Do's and Don'ts`:

> **Design authority changes by proposal.** A change to this file or to the presentation specification is proposed in writing under `docs/design/`, with the evidence that motivates it, and applied only after owner approval. Screens are restyled under the approved rules in separate, reviewable steps.

- Keep one expression per screen: one dominant action, one selection recipe, one status treatment. A screen needing a second of any of these needs a design decision, not another style.
- Design for large text and the longer language first; if a label cannot grow, the layout is the defect.

**Why:** the current flow lets an agent read a document and edit it in the same pass, so direction and implementation arrive together and cannot be reviewed separately. Naming the flow makes the owner's approval step explicit rather than customary.

## What this proposal deliberately does not do

- **No token values.** `DESIGN.md` keeps routing to the resource dictionaries; no colours, sizes, radii or durations are added.
- **No new styles.** No third card, button or chip style; P2 reduces variants rather than adding one.
- **No restructuring.** Existing sections keep their names, order and wording except where a proposal quotes them. Nothing is merged, renamed or deleted.
- **No screen changes.** No XAML, ViewModel or catalog edit is part of this proposal.
- **No product change.** No feature, platform, analytics, motion library or health-data implication.

## Consistency requirement if approved

The four sources must move together, or the change creates a contradiction:

1. `DESIGN.md` — the sections above.
2. `docs/UI_PRESENTATION_DESIGN_SPEC.md` — §3 (layout/adaptation), §4 (selection and status in the block catalog), §5 (motion, copy de-duplication).
3. `docs/design/AUDIT_2026-11-02.md` — the findings these rules answer, marked as addressed by rule rather than by fix.
4. `docs/design/SOFT_MINIMAL_IMPLEMENTATION_EVIDENCE.md` — a record of the approval and what it authorised.

## Approval

Reply with the IDs to approve, for example `approve P1–P4` or `approve all`. Rejecting an item leaves `DESIGN.md` as it is today and this file as the record of the rejected proposal.

| ID | Decision |
| --- | --- |
| P1 Expression posture | ☑ approved 2026-11-02 |
| P2 Selection and status | ☑ approved 2026-11-02 |
| P3 One statement per fact | ☑ approved 2026-11-02 |
| P4 Header ownership | ☑ approved 2026-11-02 |
| P5 Adaptation as input | ☑ approved 2026-11-02 |
| P6 Motion stance | ☑ approved 2026-11-02 |
| P7 Governance and guardrails | ☑ approved 2026-11-02 |

Approved as a set, including the consistency requirement above. Applied sources: `DESIGN.md` (all seven), specification §3/§4/§5 (matching lines), and the approval record in [the implementation evidence](SOFT_MINIMAL_IMPLEMENTATION_EVIDENCE.md). No screen, style, token or catalog changed under this approval; each restyle remains a separate reviewed step.
