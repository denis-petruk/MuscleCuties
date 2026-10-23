using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Repositories.Workout;
using MuscleCuties.Core.Repositories.Workout.Planning;
using MuscleCuties.Core.Services.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout;

public class WorkoutScheduleService : IWorkoutScheduleService
{
    private const int MissingUserId = 0;
    private const int MinimumScheduleWeeks = 1;
    private const int MaximumScheduleWeeks = 12;
    private const int DaysPerWeek = 7;
    private const int FinalDayOffset = 1;
    private static readonly DateTime MinSupportedDate = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MaxSupportedDate = new(2099, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    private readonly AppDatabase _db;
    private readonly IWorkoutInjuryRepository _injuryRepository;
    private readonly IUserRepository _userRepository;
    private readonly IWorkoutPlanGenerator _workoutPlanGenerator;
    private readonly IWorkoutRepository _workoutRepository;

    public WorkoutScheduleService(
        AppDatabase db,
        IWorkoutRepository workoutRepository,
        IUserRepository userRepository,
        IWorkoutInjuryRepository injuryRepository,
        IWorkoutPlanGenerator workoutPlanGenerator)
    {
        _db = db;
        _workoutRepository = workoutRepository;
        _userRepository = userRepository;
        _injuryRepository = injuryRepository;
        _workoutPlanGenerator = workoutPlanGenerator;
    }

    public async Task<IReadOnlyList<ScheduleDayItem>> GetDatedScheduleAsync(
        int userId,
        DateTime startDate,
        int weeksCount = 4,
        CyclePhase? phase = null)
    {
        if (userId <= MissingUserId)
            return [];

        var effectivePhase = phase ?? CyclePhase.Follicular;
        var activePlan = await EnsureGeneratedPlanAsync(userId, effectivePhase).ConfigureAwait(false);
        var workoutDays = activePlan is not null
            ? await _workoutRepository.GetWorkoutDaysByPlanAsync(activePlan.Id).ConfigureAwait(false)
            : [];

        return await GetDatedScheduleForLoadedPlanAsync(userId, startDate, weeksCount, workoutDays)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ScheduleDayItem>> GetDatedScheduleForLoadedPlanAsync(
        int userId,
        DateTime startDate,
        int weeksCount,
        IReadOnlyList<WorkoutDay> workoutDays)
    {
        if (userId <= MissingUserId)
            return [];

        ArgumentNullException.ThrowIfNull(workoutDays);
        var safeStartDate = ClampSafeDate(startDate);
        var safeWeeksCount = Math.Clamp(weeksCount, MinimumScheduleWeeks, MaximumScheduleWeeks);

        var startMonday = GetSafeWeekStart(safeStartDate);
        var totalDays = safeWeeksCount * DaysPerWeek;
        var endDate = SafeAddDays(startMonday, totalDays - FinalDayOffset);

        var workoutDayIds = workoutDays.Select(d => d.Id).ToHashSet();
        var logs = await _workoutRepository.GetWorkoutLogsByDateRangeAsync(userId, startMonday, endDate)
            .ConfigureAwait(false);
        var logsByDate = logs
            .GroupBy(l => l.Date.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        var today = DateTime.Today;
        var result = new List<ScheduleDayItem>(totalDays);

        for (var i = 0; i < totalDays; i++)
        {
            var date = SafeAddDays(startMonday, i);
            var weekIndex = i / DaysPerWeek;
            var dayOfWeek = (int)date.DayOfWeek;

            var matchingDay = MatchWorkoutDay(workoutDays, date, weekIndex, dayOfWeek);
            var isCompleted = IsDayCompleted(matchingDay, workoutDayIds, logsByDate.GetValueOrDefault(date.Date));
            result.Add(BuildScheduleDayItem(date, weekIndex, today, matchingDay, isCompleted));
        }

        return result;
    }

    public async Task RescheduleWorkoutAsync(
        int userId,
        DateTime sourceDate,
        DateTime targetDate)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");

        var safeSource = ClampSafeDate(sourceDate);
        var safeTarget = ClampSafeDate(targetDate);

        if (safeSource.Date == safeTarget.Date)
            return;

        var activePlan = await _workoutRepository.GetActivePlanAsync(userId).ConfigureAwait(false);
        if (activePlan is null)
            throw new InvalidOperationException("No active workout plan found.");

        var days = await _db.WorkoutDays
            .Include(d => d.WorkoutDayExercises)
            .Where(d => d.WorkoutPlanId == activePlan.Id)
            .ToListAsync().ConfigureAwait(false);

        var sourceDay = days.FirstOrDefault(d => d.Date.HasValue && d.Date.Value.Date == safeSource.Date)
                        ?? days.FirstOrDefault(d => d.DayOfWeek == (int)safeSource.DayOfWeek);
        var targetDay = days.FirstOrDefault(d => d.Date.HasValue && d.Date.Value.Date == safeTarget.Date)
                        ?? days.FirstOrDefault(d => d.DayOfWeek == (int)safeTarget.DayOfWeek);

        if (sourceDay is null || targetDay is null)
            throw new InvalidOperationException("Specified schedule dates are not in the active plan.");

        // Swap workout contents between sourceDay and targetDay while keeping each slot's date and week
        var tempType = sourceDay.WorkoutType;
        var tempName = sourceDay.Name;

        sourceDay.WorkoutType = targetDay.WorkoutType;
        sourceDay.Name = targetDay.Name;

        targetDay.WorkoutType = tempType;
        targetDay.Name = tempName;

        var sourceExercises = sourceDay.WorkoutDayExercises.ToList();
        var targetExercises = targetDay.WorkoutDayExercises.ToList();

        sourceDay.WorkoutDayExercises.Clear();
        targetDay.WorkoutDayExercises.Clear();

        foreach (var se in sourceExercises)
        {
            se.WorkoutDayId = targetDay.Id;
            targetDay.WorkoutDayExercises.Add(se);
        }

        foreach (var te in targetExercises)
        {
            te.WorkoutDayId = sourceDay.Id;
            sourceDay.WorkoutDayExercises.Add(te);
        }

        await _db.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task RescheduleWorkoutDayAsync(
        int userId,
        int workoutDayId,
        DateTime targetDate)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");
        if (workoutDayId <= 0)
            throw new ArgumentOutOfRangeException(nameof(workoutDayId), "Workout day ID must be positive.");

        var day = await _db.WorkoutDays
            .Include(d => d.WorkoutPlan)
            .FirstOrDefaultAsync(d => d.Id == workoutDayId).ConfigureAwait(false);

        if (day is null || day.WorkoutPlan?.UserId != userId)
            throw new KeyNotFoundException("Workout session was not found or does not belong to the user.");

        var sourceDate = day.Date ?? DateTime.Today.AddDays(day.DayOfWeek - (int)DateTime.Today.DayOfWeek);
        await RescheduleWorkoutAsync(userId, sourceDate, targetDate).ConfigureAwait(false);
    }

    private static WorkoutDay? MatchWorkoutDay(
        IReadOnlyList<WorkoutDay> workoutDays,
        DateTime date,
        int weekIndex,
        int dayOfWeek)
    {
        return workoutDays.FirstOrDefault(d =>
                   (d.Date.HasValue && d.Date.Value.Date == date.Date && d.WorkoutType != WorkoutType.Rest) ||
                   (d.WeekIndex == weekIndex && d.DayOfWeek == dayOfWeek && d.WorkoutType != WorkoutType.Rest))
               ?? workoutDays.FirstOrDefault(d =>
                   (d.Date.HasValue && d.Date.Value.Date == date.Date) ||
                   (d.WeekIndex == weekIndex && d.DayOfWeek == dayOfWeek))
               // Backward-compatible fallback for 7-day template plans
               ?? workoutDays.FirstOrDefault(d => d.DayOfWeek == dayOfWeek && d.WorkoutType != WorkoutType.Rest)
               ?? workoutDays.FirstOrDefault(d => d.DayOfWeek == dayOfWeek);
    }

    private static bool IsDayCompleted(
        WorkoutDay? matchingDay,
        HashSet<int> workoutDayIds,
        List<WorkoutLog>? dayLogs)
    {
        if (dayLogs is null || dayLogs.Count == 0)
            return false;

        return dayLogs.Any(l =>
            (matchingDay != null && l.WorkoutDayId == matchingDay.Id && l.CompletionPercent >= 100) ||
            (matchingDay != null && workoutDayIds.Contains(l.WorkoutDayId) && l.CompletionPercent >= 100) ||
            l.CompletionPercent >= 100);
    }

    private static ScheduleDayItem BuildScheduleDayItem(
        DateTime date,
        int weekIndex,
        DateTime today,
        WorkoutDay? matchingDay,
        bool isCompleted)
    {
        var dayLabel = FormatDayLabel(date.DayOfWeek);

        if (matchingDay is null || matchingDay.WorkoutType == WorkoutType.Rest)
            return new ScheduleDayItem
            {
                DayLabel = dayLabel,
                Date = date,
                WeekIndex = weekIndex,
                WorkoutTag = WorkoutActivityClassifier.RestTag,
                WorkoutTitle = matchingDay?.Name ?? "Living happy life",
                WorkoutDayId = matchingDay?.Id ?? 0,
                IsToday = date.Date == today.Date,
                IsCompleted = isCompleted,
                IsRestDay = true,
                AccentColor = WorkoutActivityClassifier.GetBackground(WorkoutActivityClassifier.RestTag),
                AccentTextColor = WorkoutActivityClassifier.GetTextColor(WorkoutActivityClassifier.RestTag)
            };

        var tag = WorkoutActivityClassifier.BuildPrimaryTag(matchingDay);
        return new ScheduleDayItem
        {
            DayLabel = dayLabel,
            Date = date,
            WeekIndex = weekIndex,
            WorkoutTag = tag,
            WorkoutTitle = matchingDay.Name,
            WorkoutDayId = matchingDay.Id,
            IsToday = date.Date == today.Date,
            IsCompleted = isCompleted,
            IsRestDay = false,
            AccentColor = WorkoutActivityClassifier.GetBackground(tag),
            AccentTextColor = WorkoutActivityClassifier.GetTextColor(tag)
        };
    }

    private async Task<WorkoutPlan?> EnsureGeneratedPlanAsync(int userId, CyclePhase phase)
    {
        var activePlan = await _workoutRepository.GetActivePlanAsync(userId).ConfigureAwait(false);
        var profile = await _userRepository.GetProfileAsync(userId).ConfigureAwait(false);
        if (profile is null)
            return activePlan;

        var activePlanDays = activePlan is null
            ? []
            : await _workoutRepository.GetWorkoutDaysByPlanAsync(activePlan.Id).ConfigureAwait(false);
        var activeInjuries = await _injuryRepository.GetActiveAsync(userId).ConfigureAwait(false);
        if (!_workoutPlanGenerator.ShouldRegenerate(activePlan, activePlanDays, profile, phase, activeInjuries))
            return activePlan;

        try
        {
            var generatedPlan = await _workoutPlanGenerator.GenerateAsync(profile, phase, DateTime.UtcNow)
                .ConfigureAwait(false);
            return await ReplacePlanPreservingCompletedLogsAsync(_db, _workoutRepository, userId, generatedPlan, profile)
                .ConfigureAwait(false);
        }
        catch (WorkoutPlanningFailureException)
        {
            return activePlan;
        }
    }

    internal static async Task<WorkoutPlan> ReplacePlanPreservingCompletedLogsAsync(AppDatabase db,
        IWorkoutRepository workoutRepository, int userId, WorkoutPlan generatedPlan, UserProfile expectedProfile)
    {
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync().ConfigureAwait(false)
            : null;
        var currentProfile = await db.UserProfiles.AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.UserId == userId).ConfigureAwait(false);
        if (currentProfile is null || !SamePlanInput(currentProfile, expectedProfile))
            throw new WorkoutPlanningFailureException("The profile changed while the workout plan was being generated.");

        // Preserve completed workout logs for this user across regeneration
        var existingCompletedLogs = await db.WorkoutLogs
            .Include(l => l.ExerciseLogs)
            .Where(l => l.UserId == userId && l.CompletionPercent >= 100)
            .ToListAsync().ConfigureAwait(false);

        var savedPlan = await workoutRepository.ReplaceActivePlanAsync(generatedPlan).ConfigureAwait(false);

        // Re-link completed logs to the corresponding new workout days
        foreach (var log in existingCompletedLogs)
        {
            var matchingDay = savedPlan.WorkoutDays.FirstOrDefault(d =>
                                  d.Date.HasValue && d.Date.Value.Date == log.Date.Date)
                              ?? savedPlan.WorkoutDays.FirstOrDefault(d => d.DayOfWeek == (int)log.Date.DayOfWeek);

            if (matchingDay is not null && log.WorkoutDayId != matchingDay.Id)
            {
                log.WorkoutDayId = matchingDay.Id;
                if (matchingDay.WorkoutDayExercises.Count > 0 && log.ExerciseLogs.Count > 0)
                {
                    var exerciseMap = matchingDay.WorkoutDayExercises.ToDictionary(e => e.ExerciseId);
                    foreach (var el in log.ExerciseLogs)
                        if (exerciseMap.TryGetValue(el.ExerciseId, out var wde))
                            el.WorkoutDayExerciseId = wde.Id;
                }
            }
        }

        await db.SaveChangesAsync().ConfigureAwait(false);
        if (transaction is not null)
            await transaction.CommitAsync().ConfigureAwait(false);
        return savedPlan;
    }

    private static bool SamePlanInput(UserProfile current, UserProfile expected) =>
        current.UpdatedAt == expected.UpdatedAt &&
        current.WorkoutDaysPerWeek == expected.WorkoutDaysPerWeek &&
        string.Equals(current.PreferredWorkoutActivityTypes, expected.PreferredWorkoutActivityTypes, StringComparison.Ordinal) &&
        current.Goal == expected.Goal &&
        current.TrainingExperienceLevel == expected.TrainingExperienceLevel &&
        current.CurrentCyclePhase == expected.CurrentCyclePhase &&
        current.CycleLength == expected.CycleLength &&
        current.SessionDurationMinutes == expected.SessionDurationMinutes &&
        string.Equals(current.EquipmentLevel, expected.EquipmentLevel, StringComparison.Ordinal) &&
        string.Equals(current.PhaseBaselinesJson, expected.PhaseBaselinesJson, StringComparison.Ordinal) &&
        string.Equals(current.ReproductiveContextJson, expected.ReproductiveContextJson, StringComparison.Ordinal);

    public static DateTime ClampSafeDate(DateTime date)
    {
        return WorkoutScheduleMathRule.ClampSafeDate(date);
    }

    public static DateTime GetSafeWeekStart(DateTime date)
    {
        return WorkoutScheduleMathRule.GetSafeWeekStart(date);
    }

    public static DateTime SafeAddDays(DateTime baseDate, int days)
    {
        return WorkoutScheduleMathRule.SafeAddDays(baseDate, days);
    }

    public static string FormatDayLabel(DayOfWeek dayOfWeek, string fallback = "DAY")
    {
        return WorkoutScheduleMathRule.FormatDayLabel(dayOfWeek, fallback);
    }
}
