namespace MuscleCuties.Core.Services.Workout;

/// <summary>
///     Pattern-based movement notes, tempo, and warm-up previews.
///     The guidance requires qualified review before a release claim of clinical approval.
/// </summary>
public static class WorkoutExerciseGuidance
{
    private static readonly (string[] Patterns, string Notes)[] TechniqueNotesCatalog =
    [
        (["Goblet Squat"],
            "Setup: hold the weight high against your chest, feet about shoulder width, ribs stacked over hips.\nMove: sit between your hips, let knees track with toes, pause if depth gets messy, then drive the floor away.\nFinish: stand tall without leaning back or letting the weight drift forward."),
        (["Bulgarian Split Squat"],
            "Setup: front foot far enough forward that the heel stays down, back foot relaxed on the bench, ribs over pelvis.\nMove: lower straight down with a slight forward torso lean, let the front knee track over toes, and keep pressure through the whole front foot.\nFinish: drive up without bouncing off the back leg; stop the set when balance starts stealing the glute/quad work."),
        (["Walking Lunge"],
            "Setup: tall posture, eyes forward, steps long enough to keep the front heel planted.\nMove: lower under control, push through the front leg, and let the back knee travel down instead of collapsing inward.\nFinish: step through smoothly and reset your balance before the next rep."),
        (["Leg Press"],
            "Setup: feet planted evenly, hips heavy against the pad, knees lined with toes.\nMove: lower until hips stay down and knees track cleanly, then press through mid-foot without locking out hard.\nFinish: control every rep; do not let the sled bounce or pull your pelvis under."),
        (["Leg Extension"],
            "Setup: knee joint lined with the machine pivot, pad above the ankle, torso still.\nMove: extend until quads fully squeeze, pause briefly, then lower with control.\nFinish: keep hips down and avoid swinging the first half of the rep."),
        (["Seated Leg Curl"],
            "Setup: knees lined with the machine pivot, thigh pad snug, toes relaxed.\nMove: curl through the full range, pause when hamstrings are shortest, then return slowly.\nFinish: keep hips pinned down so the hamstrings do the work."),
        (["Hip Thrust", "Glute Bridge"],
            "Setup: upper back supported, chin slightly tucked, feet close enough that shins are near vertical at the top.\nMove: brace first, tuck the pelvis slightly, drive through mid-foot and heels, then squeeze glutes at the top.\nFinish: lower under control without turning it into a lower-back arch."),
        (["Cable Glute Kickback"],
            "Setup: cable attached low, torso braced, working hip square to the floor.\nMove: drive the heel back and slightly up without arching the lower back.\nFinish: squeeze the glute, return slowly, and stop before the pelvis rotates open."),
        (["Cable Hip Abduction"],
            "Setup: stand tall with the cable on the outside leg, support hand light, pelvis level.\nMove: sweep the leg out from the hip, not the lower back, and keep toes mostly forward.\nFinish: pause where the side glute works hardest, then return under control."),
        (["Cable Pull-Through"],
            "Setup: face away from a low cable, soft knees, cable between legs, lats gently tight.\nMove: hinge back until hamstrings load, then drive hips through while keeping ribs down.\nFinish: squeeze glutes without leaning back or turning it into an arm pull."),
        (["Back Extension"],
            "Setup: pad below hip crease, chin tucked, ribs down, upper back slightly rounded if you want more glute bias.\nMove: lower until hamstrings stretch, then lift by squeezing glutes and driving hips into the pad.\nFinish: stop at a straight body line; avoid hyperextending the lower back."),
        (["Romanian Deadlift"],
            "Setup: soft knees, weight close to thighs, shoulders packed down.\nMove: push hips back like closing a car door, keep the weight close, stop when hamstrings are loaded or back position wants to change.\nFinish: stand by driving hips forward, not by shrugging or leaning back."),
        (["Step-Up"],
            "Setup: whole working foot on the box, ribs down, light forward lean.\nMove: push through the working leg until the hip and knee finish together; keep the back foot quiet.\nFinish: step down slowly and reset balance before the next rep."),
        (["Reverse Lunge"],
            "Setup: tall posture, feet hip width, eyes forward.\nMove: step back far enough to keep the front heel down, lower with control, and keep the front knee tracking over toes.\nFinish: push the floor away with the front leg and return without bouncing."),
        (["Incline Push-Up", "Incline Dumbbell Press"],
            "Setup: shoulder blades back and down, ribs quiet, wrists stacked under the load.\nMove: lower until chest and shoulders stay controlled, then press up while keeping elbows about 30 to 45 degrees from the body.\nFinish: lock out smoothly without shrugging."),
        (["Overhead Press"],
            "Setup: squeeze glutes lightly, ribs down, forearms vertical.\nMove: press in a straight path, move your head through after the weight clears the face, and keep wrists over elbows.\nFinish: biceps near ears, shoulders down, no lower-back lean."),
        (["Assisted Pull-Up"],
            "Setup: choose assistance that lets reps stay smooth, ribs down, hands just outside shoulders.\nMove: pull elbows down toward ribs while keeping the neck long.\nFinish: lower until shoulders move naturally, then start the next rep without swinging."),
        (["Dumbbell Row", "Chest Supported Row", "Single-Arm Cable Row", "Seated Cable Row", "Lat Pulldown"],
            "Setup: brace your trunk before the pull and set shoulders away from ears.\nMove: lead with elbows, pull to ribs or upper chest depending on the exercise, and pause briefly where the back is most active.\nFinish: return slowly until the shoulder blade moves, without losing posture."),
        (["Rear Delt Fly"],
            "Setup: hinge or support your chest, soften elbows, shoulders away from ears.\nMove: open the arms wide with rear shoulders, not traps, and stop around shoulder height.\nFinish: pause briefly, then lower slowly without swinging."),
        (["Face Pull"],
            "Setup: cable around upper-chest height, thumbs slightly back, ribs stacked.\nMove: pull toward face while elbows travel wide, then rotate gently so hands finish near temples.\nFinish: pause, feel rear shoulders and upper back, then return slowly."),
        (["Lateral Raise"],
            "Setup: slight bend in elbows, shoulders relaxed, weight just in front of thighs.\nMove: raise to shoulder height with control, lead with elbows, and stop before traps take over.\nFinish: lower slowly and keep the torso still."),
        (["Biceps Curl", "Triceps Pressdown"],
            "Setup: elbows pinned near ribs, shoulders relaxed, wrists neutral.\nMove: use the full range you can control without swinging.\nFinish: pause where the target muscle is loaded and lower with the same tempo."),
        (["Dead Bug"],
            "Setup: low back gently heavy on the floor, ribs down, knees over hips.\nMove: reach opposite arm and leg only as far as you can without the back arching.\nFinish: exhale, return slowly, and switch sides with control."),
        (["Copenhagen Side Plank"],
            "Setup: top leg supported on a bench, elbow under shoulder, ribs stacked.\nMove: lift hips and pull the lower leg toward the bench without twisting.\nFinish: hold only while hips stay level; regress by bending the top knee if form breaks."),
        (["Side Plank", "Plank"],
            "Setup: elbows under shoulders, ribs down, glutes lightly squeezed.\nMove: create a long line from head to heels and breathe behind the brace.\nFinish: stop the set before hips sag or shoulders pinch."),
        (["Pallof Press"],
            "Setup: stand tall side-on to the cable or band, knees soft, ribs stacked.\nMove: press straight out and resist rotation instead of twisting back.\nFinish: pause at full reach, breathe, then bring hands back in slowly."),
        (["Cable Woodchop"],
            "Setup: set the cable high or low, stand athletic, ribs down before you rotate.\nMove: rotate through the upper back and hips together while the arms guide the handle.\nFinish: control the return and avoid yanking from the lower back."),
        (["Hanging Knee Raise"],
            "Setup: hang with shoulders active and ribs slightly down.\nMove: curl knees toward chest by rolling the pelvis, not just swinging the legs.\nFinish: lower slowly until the body is still before the next rep."),
        (["Reverse Crunch"],
            "Setup: lie down with ribs heavy and knees bent.\nMove: curl the pelvis up first, then bring knees toward chest without throwing momentum.\nFinish: lower slowly and keep the low back controlled."),
        (["Bird Dog"],
            "Setup: hands under shoulders, knees under hips, spine quiet.\nMove: reach opposite arm and leg long without shifting hips.\nFinish: pause, return softly, and keep each rep clean."),
        (["Rock Climbing", "Climb"],
            "Setup: warm up with easy routes, open the shoulders, and keep footwork deliberate.\nMove: drive from legs first, keep hips close to the wall, and use arms to position instead of yanking every move.\nFinish: stop before grip turns sloppy; quality routes beat exhausted attempts."),
        (["HIIT Intervals"],
            "Setup: warm up until joints feel ready and breathing is awake.\nMove: make the hard blocks powerful, keep the easy blocks truly easy, and stop before form turns messy.\nFinish: cool down for a few minutes and log heart rate, duration, and effort."),
        (["Bike Intervals", "Cycling Intervals"],
            "Setup: warm up until cadence feels smooth and breathing is ready.\nMove: push the hard blocks with strong cadence, then make the easy blocks truly easy.\nFinish: cool down until breathing settles and log pace or heart rate honestly."),
        (["Running Intervals", "Tempo Run", "Easy Run"],
            "Setup: start with easy jogging and a few relaxed strides.\nMove: keep posture tall, land quietly, and use pace as feedback instead of chasing every split.\nFinish: walk it down, then log distance, pace, and heart rate if available."),
        (["Zone 2 Ride", "Easy Walk", "Swimming", "Dance Cardio"],
            "Setup: start easy for the first few minutes.\nMove: stay at a pace where breathing is controlled and repeatable; you should feel like you could continue longer.\nFinish: taper down instead of stopping suddenly, then log time, distance, and heart rate if available."),
        (["Yoga", "Vinyasa", "Mobility", "Recovery", "Pilates", "Breathing"],
            "Setup: choose a calm pace and give each position enough time to change how you feel.\nMove: breathe into the tight area, use pain-free range, and keep transitions smooth.\nFinish: leave the session feeling clearer, not crushed.")
    ];

    public static string BuildTechniqueNotes(string? exerciseName, bool usesEnduranceMetrics)
    {
        if (!string.IsNullOrWhiteSpace(exerciseName))
            foreach (var (patterns, notes) in TechniqueNotesCatalog)
                if (ContainsAny(exerciseName, patterns))
                    return notes;

        return usesEnduranceMetrics
            ? "Setup: start easy, find a sustainable rhythm, and keep effort trackable.\nMove: hold the planned time with clean breathing and steady posture.\nFinish: cool down and log the metric that best describes the session."
            : "Setup: brace first and own the starting position.\nMove: use the full range you can control, keep tempo smooth, and stop reps when form changes.\nFinish: log the weight only if the target reps were clean.";
    }

    public static string BuildQuickTips(string? exerciseName, bool usesEnduranceMetrics)
    {
        var name = exerciseName ?? string.Empty;

        if (ContainsAny(name, "Yoga", "Vinyasa", "Mobility", "Recovery", "Breathing", "Pilates"))
            return
                "Keep effort at a 4 to 6 out of 10. Longer holds are fine; sharp pain is not. Good recovery should make the next session easier.";

        if (ContainsAny(name, "Rock Climbing", "Climb"))
            return
                "Log total climbing time and keep 2 to 3 good attempts in reserve. If grip or elbows feel irritated, switch to easier routes or finish early.";

        if (usesEnduranceMetrics)
            return
                "Use the same bike, route, or pool setup when you can. Better data comes from repeatable efforts, not random hero days.";

        if (ContainsAny(name, "Squat", "Lunge", "Step-Up", "Romanian Deadlift", "Hip Thrust", "Dumbbell Press",
                "Overhead Press", "Row", "Pulldown"))
            return
                "Aim for 1 to 3 reps in reserve. Add weight next time only when every set hits the target with the same form.";

        return "Make the last rep look like the first. If the target muscle disappears, slow down before adding load.";
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
