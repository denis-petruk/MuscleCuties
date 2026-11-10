#!/usr/bin/env bash
# Interactive wizard for the human-only simulator QA pass.
#
# The agent can build, install and screenshot; only a person can judge the rendered result,
# drive VoiceOver and sign in with their own test account. This wizard prepares the loopback
# stack, launches the app, then walks the owner through the checks from
# docs/review_owner/list_of_changes.md, capturing a screenshot at every gate.
#
# Usage: bash scripts/qa-simulator-wizard.sh
# Env:   MC_SIM_UDID   simulator UDID (default: first booted, else "iPhone 17")
#        MC_LOCAL_PG  loopback-only PostgreSQL (default: QA database on port 55461)
set -euo pipefail

cd "$(dirname "$0")/.."

BOLD=$'\033[1m'; DIM=$'\033[2m'; ROSE=$'\033[38;5;175m'; GREEN=$'\033[32m'; RESET=$'\033[0m'
TOTAL_STAGES=5
STAGE=0
SHOTS="docs/release-audit/2026-11-09/screenshots-qa-2026-11-09"

stage() {
  STAGE=$((STAGE + 1))
  clear
  printf '%s\n' "${ROSE}${BOLD}Stage ${STAGE}/${TOTAL_STAGES} · $1${RESET}"
  printf '%s\n\n' "${DIM}$2${RESET}"
}

say() { printf '  %s\n' "$1"; }
pause() { printf '\n%s' "${BOLD}Press Enter when done (Ctrl+C to stop): ${RESET}"; read -r _; }
confirm() { printf '%s' "${BOLD}$1 [y/N] ${RESET}"; read -r answer; [[ "$answer" == "y" || "$answer" == "Y" ]]; }
shot() {
  mkdir -p "$SHOTS"
  local path="$SHOTS/$1.png"
  xcrun simctl io "$UDID" screenshot "$path" >/dev/null
  say "Screenshot: $path"
}

fail() { printf '%s\n' "${BOLD}$1${RESET}" >&2; exit 1; }

# ---------- Stage 1: loopback stack ----------
stage "Loopback stack" "Xcode, a simulator, the local API and a disposable local database. Nothing here may point at Beta or production."

command -v xcrun >/dev/null || fail "Xcode command-line tools are missing."
xcrun simctl list devices available >/dev/null || fail "No iOS simulators are available."

if [[ -n "${MC_SIM_UDID:-}" ]]; then
  UDID="$MC_SIM_UDID"
else
  UDID="$(xcrun simctl list devices booted -j | python3 -c 'import json,sys; d=[x for r in json.load(sys.stdin)["devices"].values() for x in r]; print(d[0]["udid"] if d else "")')"
  if [[ -z "$UDID" ]]; then
    UDID="$(xcrun simctl list devices available -j | python3 -c 'import json,sys; d=[x for r in json.load(sys.stdin)["devices"].values() for x in r if x["name"]=="iPhone 17"]; print(d[0]["udid"] if d else "")')"
  fi
fi
[[ -n "$UDID" ]] || fail "No simulator found. Open Xcode > Window > Devices and Simulators first."
say "Simulator: $UDID"
xcrun simctl boot "$UDID" 2>/dev/null || true
open -a Simulator --args -CurrentDeviceUDID "$UDID" || true
xcrun simctl bootstatus "$UDID" -b >/dev/null

PG="${MC_LOCAL_PG:-Host=127.0.0.1;Port=55461;Database=mc_qa_20261109;Username=postgres}"
case "$PG" in *Host=127.0.0.1*|*Host=localhost*) ;; *) fail "Refusing non-loopback PostgreSQL: $PG" ;; esac

if ! curl -s -m 3 -o /dev/null http://127.0.0.1:5080/api/v1/accounts/registration/quiz; then
  say "Starting the local API in the background…"
  MC_LOCAL_PG="$PG" Storage__S3__BucketName=mc-local-dummy \
    nohup bash scripts/run-local-api.sh > /tmp/mc-local-api.log 2>&1 &
  for _ in $(seq 1 40); do
    curl -s -m 2 -o /dev/null http://127.0.0.1:5080/api/v1/accounts/registration/quiz && break
    sleep 3
  done
fi
curl -s -m 5 -o /dev/null http://127.0.0.1:5080/api/v1/accounts/registration/quiz \
  || fail "The local API is not answering on 127.0.0.1:5080. See /tmp/mc-local-api.log."
say "Local API is answering on 127.0.0.1:5080."

# ---------- Stage 2: build, install, launch ----------
stage "Launch the app" "Build the Debug simulator app, install it and start it. Sign in with the local test account (OTP prints in the API terminal)."

dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios \
  -p:RuntimeIdentifier=iossimulator-arm64 -p:_DeviceName=":v2:udid=$UDID" -t:Run

pause
shot "wizard-01-launch"

# ---------- Stage 3: E-01 / C-3 onboarding ----------
stage "Onboarding honesty (E-01, C-3)" "Open Create account. Do NOT touch the birthday, height or weight controls."

say "1. Tap Continue on an untouched form. Expect an inline error by Continue — never a saved profile."
say "2. Pick a birthday, confirm it with 'Confirm birthday', choose height and weight, then continue."
say "3. Return to the profile: it must show exactly the values you chose (no 2008-11-09 / 165 / 65 defaults)."
pause
shot "wizard-02-onboarding"

if confirm "Did an untouched form stay blocked AND the chosen values save exactly?"; then
  say "${GREEN}Onboarding gate: PASS${RESET}"
else
  say "Onboarding gate: FAIL — capture the screen and record it in the coverage table."
fi

# ---------- Stage 4: session pain + layout (C-9) ----------
stage "In-session pain (C-9)" "Start a strength session, log one set, then tap This hurts."

say "1. The pain sheet must appear without squeezing the exercise or clipping the set rows."
say "2. Switch must open swaps; if none are reviewed, the empty state says to stop the exercise."
say "3. Stop must advance without erasing the logged set; the summary shows the injury-log reminder."
say "4. With a celebration banner visible, the first action must not be swallowed."
pause
shot "wizard-03-pain"

if confirm "Did the pain sheet, empty-swap copy, stop path and first tap all behave?"; then
  say "${GREEN}Pain gate: PASS${RESET}"
else
  say "Pain gate: FAIL — capture the screen and record it in the coverage table."
fi

# ---------- Stage 5: appearance, language, accessibility ----------
stage "Appearance, RU and VoiceOver (C-1, C-4, C-5, J14)" "These are judgement calls only you can make."

say "1. Light and dark: the preview count and Required tag are readable (≥ 4.5:1)."
say "2. VoiceOver: an invalid Continue announces the reason once, never as a heading."
say "3. RU: Cycle Quick Log says 'Загрузка сохранённой записи…' (not 'входа')."
say "4. iPhone SE + XXXL + RU: no clipped labels or mid-word breaks."
pause
shot "wizard-04-appearance"

printf '\n%s\n' "${ROSE}${BOLD}Wizard complete.${RESET} Evidence is in ${SHOTS}."
say "Record PASS/FAIL per gate in docs/review_owner/list_of_changes.md."
say "Stop the local API with Ctrl+C in its terminal when finished."
