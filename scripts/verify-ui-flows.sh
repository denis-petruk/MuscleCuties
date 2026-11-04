#!/usr/bin/env bash
# Guided owner walkthrough for the navigation, copy and style convergence passes.
#
# AGENTS.md forbids the agent from building, running or screenshotting the MAUI client, so the
# observations below can only be made by a person on a simulator or device. This script walks
# them in order and records the answers; it changes no project files.
#
# Usage: bash scripts/verify-ui-flows.sh
#
# ── File → stage mapping (every client file these passes changed) ───────────────────────────
#   AppShell.xaml                              tab titles moved to the catalog ...... stage 3
#   Pages/Nutrition/NutritionPage.xaml         header + entry points + meal card .... stage 4
#   Pages/Nutrition/FoodLogPage.xaml.cs        addMeal / addAt / entryId reading .... stage 5
#   Pages/Nutrition/FoodLogPage.xaml           counts, units and numeric hints ....... stages 5, 6
#   Resources/Converters/FoodLogConverters.cs  add/edit title + warmup labels ..... stages 5, 10
#   Resources/Styles/Colors.xaml              role aliases (raw palette frozen) .. stages 12, 15
#   Resources/Styles/Styles.xaml              shared roles + ModalTitle heading .. stages 12, 15
#   Controls/Shared/AnimatedModalView.cs      focus/isolation/announcement ..... stage 14
#   Controls/Shared/ModalTransition.cs        motion cancel + rapid reopen ..... stage 14
#   Pages/Nutrition/FoodLogPage.xaml             edit sheet host .............. stage 14
#   Pages/Workout/WorkoutHistoryPage.xaml        detail sheet host ............ stage 14
#   Pages/Workout/StrengthPlanGenerationPage.xaml preview sheet host .......... stage 14
#   Pages/Workout/WorkoutPage.xaml               activity chooser host ........ stage 14
#   Controls/Profile/ProfileImageCropModal.xaml  crop sheet host .............. stage 14
#   Controls/Workout/WorkoutSwapPanel.xaml       swap sheet host .............. stage 14
#   Controls/Nutrition/MealEntryModal.xaml    compact meal context ................ stage 16
#   Controls/Nutrition/MealEditorModal.xaml    idle vs no-match copy ................ stage 7
#   Controls/Dashboard/ReadinessRecoveryCard.xaml  `/100` suffix .................... stage 8
#   Controls/Dashboard/DailyCheckInContent.xaml    1–5 scale copy ................... stage 8
#   Pages/Workout/WorkoutSessionPage.xaml      0 / 6:30 input hints ................. stage 9
#   Pages/Workout/WorkoutHistoryPage.xaml      month selection + inline colours ..... stage 10
#   Pages/Auth/OtpVerificationPage.xaml        `123456` code hint .................. stage 11
#   Pages/Auth/PasswordRecoveryPage.xaml       code hint + password mask ........... stage 12
#
#   Not separately walkable: AppShell.xaml.cs derives the profile-root route from the shell's
#   own Route values. It only runs on a tab re-tap reset, so stage 3 covers the tab bar it
#   affects and the route itself is proven statically (no route literal outside Navigation/).
#   Navigation/NutritionNavigation.cs is not a screen; stages 5 and 6 exercise its four members.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TOTAL_STAGES=16
STAGE=0
NOTES=()

bold=$'\033[1m'; dim=$'\033[2m'; green=$'\033[32m'; red=$'\033[31m'; reset=$'\033[0m'

stage() {
  STAGE=$((STAGE + 1))
  clear 2>/dev/null || true
  printf '%s\n' "${bold}UI walkthrough — stage ${STAGE}/${TOTAL_STAGES}: $1${reset}"
  printf '%s\n\n' "${dim}$2${reset}"
}

say() { printf '%s\n\n' "$1"; }

confirm() {
  local prompt="$1" answer
  while true; do
    printf '%s [y/n] ' "${bold}${prompt}${reset}"
    read -r answer
    case "$answer" in
      y|Y|yes) return 0 ;;
      n|N|no) return 1 ;;
      *) say "Answer y or n." ;;
    esac
  done
}

record() {
  local label="$1" result="$2"
  NOTES+=("${label}: ${result}")
  if [ "$result" = "pass" ]; then
    printf '%s\n\n' "${green}✓ recorded${reset}"
  else
    printf '%s\n\n' "${red}✗ recorded as failing — capture a screenshot and the device/OS version${reset}"
  fi
}

check() {
  local label="$1" prompt="$2"
  if confirm "$prompt"; then record "$label" pass; else record "$label" fail; fi
}

# Every screen stage ends with this pair of checks, so large text and Russian are confirmed
# per screen instead of once for the whole app.
check_copy_appearance() {
  local screen="$1"
  check "$screen — catalog copy in Russian" "On $screen: switch the app to Russian, confirm every label, unit, count and placeholder renders (no English fallback, no blank field), then raise the text size two steps and confirm nothing clips, overlaps or shrinks."
}

stage "Build the client" "Runs the documented iOS simulator build. A successful build does not prove the app starts, so launching is a separate stage."
cd "$ROOT"
if dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios --nologo -v q; then
  record "iOS simulator build" pass
else
  record "iOS simulator build" fail
  say "Fix the build before continuing; later stages assume a fresh bundle."
  exit 1
fi
printf '%s' "Press Enter to continue. "; read -r _

stage "Launch and sign in" "Start the app the way you normally do. If a launch aborts inside mono_init with an AOT mismatch, delete the net10.0-ios bin/obj output and rebuild — see AUDIT.md."
check "App launch" "Is the app running and signed in?"

stage "Tab bar — AppShell.xaml (all screens)" "AppShell.xaml no longer holds English Title literals; ApplyLocalizedTitles() reads Tab_Today..Tab_You from the catalog in the shell constructor. Switch to Russian and look at the tab bar: Сегодня, Цикл, Тренировка, Питание, Ты. A blank or English title means that constructor path did not run."
check "Tab titles follow language" "Do all five tab titles render in Russian, with no blank or English title?"
check "Tab bar — large text" "Raise the text size two steps: do all five titles stay legible without truncation?"

stage "Nutrition page — NutritionPage.xaml" "Open Питание. Reachability: the header is the title alone with no icon buttons; 'Log a meal' (primary) and a labelled 'Food log' (secondary) sit side by side under the cards. One task, one entry point: the sheet starts a meal, the button opens the diary."
check "Nutrition entry points" "Both actions visible and labelled, with no header icons?"
check_copy_appearance "the Nutrition page"

stage "Nutrition → diary — preserved intent (addMeal, addAt, entryId)" "Two hops must carry their intent. (1) Tap 'Log a meal', choose a meal and a time earlier than now, then 'Add food': the sheet closes and the diary opens with its logging sheet up for that meal, and saving records the chosen time, not the current clock. (2) Back on Питание, open a meal card and then one logged item: the diary opens directly on that entry instead of today's list."
check "Meal and time survive the hop" "Did the diary open on the requested meal and keep the chosen time?"
check "Recorded entry survives the hop" "Did the logged item open directly, with no re-selection?"

stage "Diary — FoodLogPage.xaml rows and copy" "In the diary, saved templates show '{0} items' and total kcal, recipes show 'Per serving • {0:F0} kcal', all from the catalog. Template and recipe rows keep the title and calories above a full-width Log / Edit / Delete row. The shelves never claim to be empty while still loading."
say "The add/edit sheet title now resolves through the catalog (Nutrition_FoodLog_LogEntryTitle / _EditEntryTitle) instead of the converter's English ternary. The day strip takes its selected surface and text from DayStripCell's theme-aware pairs, so dark appearance no longer pairs a pale fill with pale text."
check "Diary counts and units" "Do template item counts, recipe per-serving text and kcal values all render, in both languages?"
check "Add/edit sheet titles follow language" "Open Add food, then edit a saved entry: switch to Russian and confirm each sheet title is Russian, not English."
check "Selected date in both appearances" "Switch to dark appearance: is the selected day's fill, stroke and text readable, and is the selection still obvious without relying on colour alone?"
check_copy_appearance "the diary row shelves"

stage "Meal editor — MealEditorModal.xaml" "Open a meal's breakdown, then Edit, then 'Add food'. With an empty query the sheet invites a search; only an unmatched query says there is no match. This editor deliberately stays in place instead of routing to the diary, because it holds an unsaved draft — confirm that leaving it would have discarded your edits."
check "Finder idle vs no-match copy" "Is the idle copy shown before searching and the no-match copy only after?"
check_copy_appearance "the meal editor"

stage "Dashboard — ReadinessRecoveryCard.xaml and DailyCheckInContent.xaml" "On Today, the readiness score shows the catalog-backed '/100' suffix. Open the daily check-in from the readiness card and confirm its 1–5 scale labels come from Common_Scale_1..5. Both strings were literals before this pass."
check "Readiness suffix and check-in scale" "Do '/100' and the 1–5 scale render correctly?"
check_copy_appearance "the dashboard and its check-in sheet"

stage "Workout session — WorkoutSessionPage.xaml" "Train → open a workout → start the session. The weight/reps inputs show the catalog-backed '0' hint and the duration input shows '6:30'. Earlier these were literal placeholders that could not be translated."
check "Session input hints" "Do the '0' and '6:30' hints appear in both languages?"
check_copy_appearance "the workout session screen"

stage "Workout history — WorkoutHistoryPage.xaml" "Train → History. The selected month takes the accent colour while the others stay muted (previously both states resolved to the same colour, so selection was invisible). Set rows label each set from the catalog (Workout_History_WarmupSet / _WorkingSet) rather than the converter's English literals. While the next page loads only the footer spinner shows; the 'no sessions' card waits until loading has finished."
check "Month selection visible" "Can you tell the selected month apart at a glance?"
check "Warmup and work labels follow language" "Open a session's set breakdown and switch to Russian: does each set's Warmup/Work label show the catalog's Russian text, with no English left?"
check_copy_appearance "workout history"

stage "OTP verification — OtpVerificationPage.xaml" "Reach the code screen the way a new account does: sign out, register a new account, and continue until the code field appears (shell route OtpVerificationPage). The field hint is now Common_Placeholder_SixDigitCode (123456) from the catalog."
check "OTP code hint" "Does the 123456 hint render, and is the rest of the screen intact?"
check_copy_appearance "the OTP screen"

stage "Password recovery — PasswordRecoveryPage.xaml" "Sign out, open the login screen, and tap 'Forgot password'. The code field shows 123456 (Common_Placeholder_SixDigitCode) and both new-password fields show the mask from Auth_PasswordMaskPlaceholder. Step through all three steps; each keeps its own primary action."
check "Recovery code hint and password mask" "Do the 123456 hint and the password mask render on every step?"
check_copy_appearance "password recovery"

stage "Contracts checklist — copy keys and route keys" "Confirm the two contracts by eye against this list; nothing to interact with here."
say "New catalog keys (present in both EN and RU, one entry each):
  Common_Placeholder_Zero / _One / _Hundred
  Common_Placeholder_TimeExample          (6:30)
  Common_Placeholder_SixDigitCode         (123456)
  Auth_PasswordMaskPlaceholder            (••••••••)
  Dashboard_ReadinessScoreSuffix          (/100)
  Common_Scale_1 … Common_Scale_5         (1–5)
  Nutrition_FoodLog_EditEntryTitle / _LogEntryTitle
  Workout_History_WarmupSet / _WorkingSet"
say "Navigation contract (Navigation/NutritionNavigation.cs — the only place route knowledge lives):
  FoodLogRoute  \"FoodLogPage\"
  ToLogEntry(historyId)             → route?id=… via key \"entryId\"
  ToAddFood(mealType, minuteOfDay)  → route?\"addMeal\"=…&\"addAt\"=…
  TryReadEntryId(query) / TryReadAddFoodIntent(query)
  Callers: FoodLogPage (reads), NutritionViewModel.Meals (builds entry route),
  NutritionViewModel.MealEntry (builds add-food route)."
say "Colour-role contract: Colors.xaml is the only file allowed to name a raw palette value
  (Gray200, White, ...). Every screen and shared style resolves an intent role instead
  (Surface*, Text*, Border*, Track*, Accent*, state roles); the guard fails on a reintroduced
  raw reference and on any new raw token. This was a zero-pixel migration: report any
  appearance change as a defect, not as an accepted role choice."
say "Colour contract: 20 semantic colour sites remain inline across five pages (FoodLogPage 9,
  WorkoutHistoryPage 5, CyclePhaseDetailPage 3, WorkoutPage 2, ProfilePage 1). They are recorded
  as a sanctioned exception in DESIGN.md — accent, warning, success, inverse-on-accent, muted
  de-emphasis and primary emphasis, none repeating its own style's colour. The guard now resolves
  each site's applied style, including BasedOn chains and the implicit label style, and fails on
  a duplicate pair as well as a count change. A shared style is still blocked by the
  two-styles-per-family limit."
check "Contracts checklist" "Have you ticked every key and route above as seen/understood?"

stage "Shared sheets — focus, isolation and announcement" "Open a shared sheet from each family: daily check-in, injury log, meal entry, meal editor, meal detail, cycle warning, phase summary, food-log edit, workout-history detail, strength-plan preview, profile crop, workout activity chooser and workout swap sheet. With VoiceOver on, focus should enter the first actionable control, the sheet title should be announced once, covered page content should not be reachable, and closing should return focus to the invoking control. Enable Reduce Motion and repeat one open/close; then close and reopen immediately to confirm no stale focus or background state. The day-overview state inside an active session is not a dismissible sheet and is not part of this check."
check "Focus enters and returns" "Did focus enter the sheet and return to a sensible invoking control after close?"
check "Background content is not reachable" "With the sheet open, can VoiceOver still reach the page behind it?"
check "Title announced once" "Was the sheet title announced once, and not repeated while the sheet stayed open?"
check "Reduce Motion and rapid reopen" "With Reduce Motion on, and after an immediate close/reopen, did the sheet still enter and leave cleanly?"

stage "Meal entry — compact context before Add food" "Open Nutrition → Log a meal. The four meal choices are compact 44pt label cells with no illustrations; the selected cell keeps the rose surface/stroke. The time field follows, then Add food is the first action. The sheet should reach Add food without scrolling on a small phone, and selecting a different meal must still carry that meal and time into the diary."
check "Add food is visible before scrolling" "On a small phone, is Add food reachable without scrolling past the meal choices?"
check "Meal context survives the handoff" "Choose a different meal and time, tap Add food: does the diary open for that meal and minute?"
check "Selection is announced once" "With VoiceOver, does the selected meal say selected, and the others not selected, without duplicating the meal name?"
check_copy_appearance "the meal entry sheet"

stage "Large text and Russian — cross-screen sweep" "One final pass at the largest readable text size, in Russian, across every screen above: tab bar, Питание, the diary and its logging sheet, the meal editor, Today and the check-in sheet, the workout session, workout history, OTP and password recovery. Look for the failures a per-screen check can hide: a tab title that truncates only when another screen is large, a sheet header that overlaps its close button, a footer action pushed under the keyboard."
check "Cross-screen large text and Russian" "Did every changed screen hold up at the largest text size in Russian?"
check "Colour identity unchanged" "Compared with the previous build, do surfaces, text, borders, tracks and accent states look unchanged in both light and dark appearance?"

clear 2>/dev/null || true
printf '%s\n' "${bold}Result${reset}"
printf '%s\n' "------------"
for note in "${NOTES[@]}"; do printf '  %s\n' "$note"; done
printf '\n%s\n' "${dim}Paste this summary, with any failing screenshots, into the next session so the"
printf '%s\n' "remaining device work is recorded instead of rediscovered.${reset}"
