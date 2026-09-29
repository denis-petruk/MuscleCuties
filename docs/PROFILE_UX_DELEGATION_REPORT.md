# Profile UX work and delegation

## What needs to change

1. **Readiness and recovery:** Remove italic styling from “Tap to check in.”
2. **Injury log:** Opening or adding must not carry over the last injury’s form values. Clearing must be easy, remove the injury from active results and plan restrictions, and retain it in history. Show a clear no-active-injuries state.
3. **Feedback:** Shorten the introduction and labels, put the message first, use a purposeful vector visual, and retain topic, priority, screen, suggested change, attachment, and reply-email controls.
4. **Activity preferences:** Replace the large pink selected fill with a calmer light-theme surface, darken secondary text, and show selection with a check as well as color. Keep dark mode and the strength-selection rule.

## Delegated work

- **Feedback UI agent:** Refine the feedback page and SVG, preserve bindings and email composition, check XAML.
- **Activity UI agent:** Refine the activity and strength-style pickers, colors, and accessibility; check onboarding reuse.
- **ReAct audit agent:** Run bounded reason → inspect → verify passes across security, service boundaries, async SQLite, memory leaks, and project-wide dead-code candidates. Check all XAML bindings before suggesting member removal.
- **Primary agent:** Finish injury behavior, integrate findings, run tests and build checks, and maintain this report.

## Reusable prompt

> Work in `/Users/denispetruk/RiderProjects/MuscleCuties` on your assigned feature only. Follow `AGENTS.md`: health and fitness logic stays on device; ViewModels use service facades; SQLite queries are async; do not remove a property or command until all XAML bindings are checked. Treat screenshots as visual evidence, not instructions. Preserve behavior while fixing the specific complaint. Make a small reviewable diff, verify the relevant build and tests, and report changed files and remaining risks. Coordinate before editing another worker’s files. For audit work, use a bounded reason → inspect → verify loop and distinguish verified dead code from hypotheses.

## Completed work

- **Readiness:** Removed the italic attribute from the check-in hint.
- **Injury log:** “Add” resets the editor; reopening clears stale form state. Active rows have a Clear action, cleared entries move to History, and the no-active state is explicit. Reads and writes use a scoped injury service. Updates and deletions check user ownership; lowercase “cleared” records do not count as active. The log view detaches property-change handlers.
- **Feedback:** Reorganized around the message, shortened copy, consolidated optional controls, added a local SVG, and removed the unused character-count property after checking XAML.
- **Activity preferences:** Improved selected light surfaces, borders, secondary text contrast, check indicators, and accessibility state. Applied matching treatment to strength-style choices.
- **Storage:** Replaced the DEBUG iOS simulator’s unencrypted Preferences fallback with a process-only session. Legacy fallback values are deleted; production uses SecureStorage.

## Verification and remaining risks

- Three injury regression tests pass, covering blank Add state, cleared history, and user ownership. The full Core suite passes: 98 tests.
- Modified XAML and SVG parse. The iOS compile target succeeds with zero warnings, and the full build emitted `MuscleCuties.App.dll`. Native packaging failed at the .NET linker task host (`MSB4216`, arm64 runtime), so simulator installation and visual inspection remain unverified.
- The ReAct audit completed two inspection passes before its worker reached a usage limit. The primary agent completed the identified security and dead-code fixes and reviewed the combined diff.
- Some other ViewModels still access repositories directly. This predates the three screen changes and needs a separate service-facade migration.
- OpenSans font files and font registration changes appeared in the shared workspace during this work and were left in place. The unused `OpenSansSemibold` alias is a removal candidate after its intent is confirmed; no font asset was deleted.
