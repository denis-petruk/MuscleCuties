# Workout Generation Rules Audit

Updated 2026-10-26. These are product decisions from the workout-generation interview. They describe the intended behavior, not verified implementation or release readiness. The observed Apply failure remains open; the exact server limitation codes and deployed source revision are not yet confirmed.

## Onboarding and entry points

- Collect strength styles, available workout days, strength-session duration, equipment and experience during onboarding. Default equipment to Full Gym; the user chooses it during onboarding and can change it later in Profile → Workout preferences.
- Require at least one strength style and at least two workout days in onboarding and Workout preferences. Show “Pick at least one strength activity” or “Pick at least 2 days” when the relevant selection is missing. Multiple strength styles may be selected.
- Use the onboarding answers without presenting Workout preferences again after registration. If no additional questions, omissions or acknowledgment are needed, generate and apply the first plan automatically.
- If activity-specific questions are needed, ask them before preview and offer **Explore other features**, which opens the dashboard without a plan. The Workout page then shows a Generate plan prompt covering its workout cards and blocks. The prompt also appears when a new user leaves the first-plan preview or preferences without applying.
- If the first plan has omitted activities or requires lower-volume acknowledgment, show the preview before applying. Offer Apply and Edit; Edit opens Workout preferences. Explore other features is not offered in that preview. Applying the first plan requires no replacement confirmation.
- For an existing plan, access changes through Profile → Workout preferences. There is no general Generate plan button on the Workout page. Route to activity-specific questions when required; otherwise go directly to preview. Existing users have a back arrow: first close the preview, then leave generation. Their current plan and preferences remain unchanged if they leave without applying.

## Questions and saved answers

- Use conservative defaults where possible and ask for fewer details. Required safety answers must be collected before entering the preview; highlight every missing required field and ask the user to fill it.
- Ask the user's preferred type of yoga. Ask swimming, climbing and other activity safety questions when needed and no saved answer satisfies the check. Do not show an unnecessary questionnaire.
- Save activity-specific answers when the user taps **Preview workout plan**, even if they do not apply. Prefill saved answers next time and allow changes before preview. Answers cannot be edited while preview is open; return to the questionnaire and create a fresh preview after editing.
- If multiple selected strength styles fit without volume reduction, ask the user to choose one alongside the activity questions. Do not save this choice as a persistent preference; ask again when applicable.

## Plan generation and preview

- Automate decisions wherever possible. Choose activity frequencies and non-strength session durations automatically. The duration chosen in onboarding applies to one individual strength session, not the day's combined activities.
- Select one strength style. Prefer a selected style that fits without reduction. If none fits, choose a style automatically, reduce its volume and show a notice in preview that it is hard to fit the selected duration. Apply remains available; for a user-initiated preview, tapping Apply prompts the user to acknowledge the reduced volume before the plan is applied. Do not display individual set reductions. Automatic plan updates do not require this acknowledgment.
- Schedule at most two activities per day; no two sessions of the same activity and no more than one strength session per day. Different non-strength activities may share a day, and strength may share a day with a non-strength activity. Yoga, mobility and other recovery activities count as recovery. On consecutive days adjust strength muscle groups so the same group is not trained on back-to-back days. If a safe split cannot be made, schedule fewer strength sessions and explain the reduction.
- An activity that cannot fit, or is unsafe based on its answers, is omitted with a brief explanation. The remaining plan can be applied. The preview shows the chosen strength style, adjusted session count and each activity's weekly frequency; list omitted activities with a short reason. It does not include separate editing actions for each omission; use Back to edit.
- If no safe exercise is available for a required movement, try a different strength plan. If a complete alternative is unavailable, omit the movement, explain the gap and allow Apply. Every exercise included must have complete safety and catalog information; the catalog must be complete before release. The product should support a safe plan for all supported onboarding combinations.

## Apply and scheduling

- Tapping Apply for an existing plan prompts the user to confirm replacement, with a short summary and omitted activities; the button is not disabled while awaiting confirmation. A first plan applies without replacement confirmation. After Apply, newly registered users go to the dashboard; existing users go to the Workout page. The dashboard card shows the applied plan and next scheduled workout.
- Start on the next selected workout day. Today is eligible only if it is selected and no activity has already been completed today. A started but unfinished activity does not count as completed for this rule. Remove an unfinished previous-plan workout from the active schedule, but preserve its already logged exercises and sets. Completed workout history is retained.
- Preferences and plan do not change until the user applies. Activity-specific answers are saved at preview separately. A newly unsafe activity is an exception: when the safety answer is saved at preview, remove the activity from the current plan immediately and show a brief notice, even if the new preview is not applied.
- When the saved answer later makes an activity safe again, automatically apply a regenerated plan and adjust upcoming sessions, starting on the next eligible day under the same start rule.
- Plan and preference edits are not available while preview is open, including on another device. Any implementation must enforce this expectation rather than assume conflicting writes cannot occur.

## Injuries and automatic updates

- Use active injuries and injuries cleared within the previous seven calendar days. Replace exercises affected by active injuries with suitable alternatives, without a separate replacement notification.
- Clearing an injury automatically restores the original affected exercise in place of its temporary alternative and marks it optional for seven calendar days. After seven days, it becomes required automatically. A newly recorded active injury triggers replacement in future workouts; the UI does not allow recording an injury during an active workout session.
- Automatic updates change upcoming workouts, not completed history or logged exercises, and otherwise happen silently. They preserve the user's selected days, strength-session duration and activities. For multiple fitting selected styles, ask whether the user wants to try another; if they decline or dismiss, keep the current style. If they choose to switch, show the fitting alternatives. If the current style no longer fits, keep it and reduce its volume.

## Experience progression

- Use the experience level chosen at onboarding and recorded workout history to adjust difficulty. Preserve a selected style and tailor its initial difficulty and volume to experience. For heavy or high-intensity work, use safer alternatives without asking for exercise-specific familiarity or load confirmations.
- Count distinct days on which at least one workout of any type was completed, starting with the first completed workout. Multiple completed workouts on the same day count once. Manually logged workouts do not count, and deleting a completed workout does not remove its day from the count.
- Promote Beginner to Intermediate at 20 completed workout days and Intermediate to Advanced at 130 completed workout days. A user who selected Intermediate also reaches Advanced at 130 days; Advanced remains Advanced. Experience is not manually editable in profile.
- Promotion automatically updates future workouts, preserving selected days, duration and activities; an in-progress triggering workout remains intact and the updated plan begins with the next scheduled workout.
- **Later build:** show experience and progress toward the next level as a gradually filling progress bar, with a celebration screen on promotion. This display is staged for later; it does not defer the promotion rules themselves.

## Connection and session behavior

- For any connection failure, identify it as a connection error and ask the user to try again. Keep an existing plan intact when an operation cannot be saved. Keep a preview available for retry after an Apply failure. No change is saved locally for later synchronization when the connection is lost; safety changes that cannot be saved require retry after reconnection.
- Users should remain signed in during normal use. If the server rejects the session, log the user out.

## Audit findings and outstanding verification

- The observed simulator preview scheduled strength and yoga but disabled Apply while swimming and mobility were omitted. The screen showed a generic limitation. The exact reason has not been captured; do not conclude omissions are the cause without the returned limitation codes.
- Current source requires a feasible, persisted preview to enable Apply and checks the stored proposal and schedule again during acceptance. Optional omissions alone should not block in the current mapper, but deployed-code parity is unverified.
- Current scheduling source permits one available weekday and limits same-day combinations more narrowly than the agreed rules. The interview requires at least two days and permits the stated two-activity combinations; reconcile generation and acceptance consistently.
- Current strength-generation rules include history, catalog, exercise timing, volume and safety limitations; the generic user-facing fallback can obscure the actual blocker. Audit each decision path against the agreed behavior without bypassing exercise safety or persistence checks.
- Experience promotion, cleared-injury timing, automatic updates, questionnaire persistence, first-plan navigation, confirmation and new scheduling behaviors require implementation and end-to-end verification. They are not claimed complete by this document. The onboarding quiz's live experience choices, full catalog coverage, and deployed backend source parity also remain to be verified.
