#!/usr/bin/env bash
# Guided device verification for the UI consistency & accessibility package.
#
# These checks need an iPhone, VoiceOver and a signed build; an agent cannot perform them.
# Run from the repository root:  bash scripts/verify-ui-consistency-accessibility.sh
# The script changes nothing in the repository; it records what you observe.

set -euo pipefail

TOTAL_STAGES=7
STAGE=0
RESULTS_FILE="${RESULTS_FILE:-ui-device-verification-$(date +%Y%m%d-%H%M).md}"

stage() {
  STAGE=$((STAGE + 1))
  clear 2>/dev/null || true
  printf '\n=== Stage %d/%d — %s ===\n\n' "$STAGE" "$TOTAL_STAGES" "$1"
}

say() { printf '%s\n' "$*"; }

confirm() {
  local answer
  while true; do
    read -r -p "$1 [y/n] " answer
    case "$answer" in
      y|Y) return 0 ;;
      n|N) return 1 ;;
      *) say "Please answer y or n." ;;
    esac
  done
}

ask() {
  local value
  read -r -p "$1 " value
  printf '%s' "$value"
}

record() {
  printf '%s\n' "$1" >> "$RESULTS_FILE"
}

stage "Environment"
say "Record the build under test. The reviewer needs the artifact, not just a branch name."
BUILD_ID=$(ask "Build/TestFlight number or commit SHA:")
DEVICE=$(ask "Device model and iOS version:")
record "# UI device verification — $(date -u +%Y-%m-%dT%H:%MZ)"
record ""
record "- Build: ${BUILD_ID}"
record "- Device: ${DEVICE}"
record ""

stage "F01 — injury tri-state"
say "Open Train → the injury action in the header."
say "1. The clinician-advice question must show three cells: Not answered, Yes, No."
say "2. Select No, save, reopen the injury. No is selected and the restriction text is absent."
say "3. Create a new injury and leave the answer on Not answered. Save and reopen."
say "4. With VoiceOver, each cell must announce its label and selected/not selected."
if confirm "All four observations hold?"; then
  record "- F01 injury tri-state: PASS"
else
  record "- F01 injury tri-state: FAIL — $(ask 'What did you observe?')"
fi

stage "S01 — Food Log date actions"
say "Open Nutrition → Food Log. Turn VoiceOver on."
say "Swipe to the previous-day and next-day controls in the date strip."
say "Each must announce Previous Day / Next Day (or the Russian equivalent) and activate on double-tap."
if confirm "Both date controls announce and activate?"; then
  record "- S01 Food Log date actions: PASS"
else
  record "- S01 Food Log date actions: FAIL — $(ask 'What did you observe?')"
fi

stage "S01 — quick-food selection state"
say "In Food Log open the quick-food list and use the selection control on a row."
say "VoiceOver must announce the food plus selected or not selected, and the state must update."
if confirm "Selection state is announced and updates?"; then
  record "- S01 quick-food selection state: PASS"
else
  record "- S01 quick-food selection state: FAIL — $(ask 'What did you observe?')"
fi

stage "S02 — headings and back actions"
say "Use the VoiceOver rotor (Headings) on Dashboard, Vault, Profile, Food Log, Nutrition and History."
say "Each screen must expose exactly one level-one heading naming the screen."
say "On Food Log, History and Vault, the back control must announce Back and return to the prior screen."
if confirm "Headings and back actions behave as described?"; then
  record "- S02 headings and back actions: PASS"
else
  record "- S02 headings and back actions: FAIL — $(ask 'What did you observe?')"
fi

stage "F06 — units round trip"
say "Profile → Units: choose Imperial and save. Then open Personal info."
say "1. Height hint reads in, weight hint reads lb, and the values look converted (not multiplied)."
say "2. Change weight, save, reopen: the value persists and matches what you typed."
say "3. Open Dashboard → Your progress → weight and workout views: lb, mi and per-mile pace."
say "4. Switch back to Metric and confirm cm, kg, km return."
if confirm "All four observations hold?"; then
  record "- F06 units round trip: PASS"
else
  record "- F06 units round trip: FAIL — $(ask 'What did you observe?')"
fi

stage "Large text"
say "Settings → Accessibility → Display & Text Size → Larger Text: choose the largest non-accessibility size."
say "Re-check Dashboard, Food Log and the injury form. Nothing may clip, overlap or lose its action."
if confirm "Largest supported text keeps every label readable and action reachable?"; then
  record "- Large text: PASS"
else
  record "- Large text: FAIL — $(ask 'Which screen and element failed?')"
fi

stage "Record the result"
record ""
record "Recorded $(date -u +%Y-%m-%dT%H:%MZ)."
say "Written to ${RESULTS_FILE}"
say "Attach this file and any screenshots to the review. A FAIL keeps the package BLOCKED;"
say "do not describe the device gate as passed without this evidence."
