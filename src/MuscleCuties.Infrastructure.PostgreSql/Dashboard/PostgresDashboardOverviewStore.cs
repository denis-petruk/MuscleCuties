using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Domain.Dashboard;
using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.Dashboard.CheckIn;
using MuscleCuties.UseCases.Features.Dashboard.Overview;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.Targets;

namespace MuscleCuties.Infrastructure.PostgreSql.Dashboard;

public sealed class PostgresDashboardOverviewStore(
    PostgresDbContext dbContext,
    IServerNutritionTargetProjectionStore targetStore,
    TimeProvider timeProvider) : IServerDashboardOverviewStore
{
    private const int DefaultCycleLengthDays = 28;
    private const int DefaultWorkoutDaysPerWeek = 3;
    private const int TrendWindowDays = 13;
    private const int WeeklyWeightLookbackDays = 84;
    private const int WorkoutProgressLookbackDays = 90;
    private const int InclusiveRangeDayAdjustment = 1;
    private const int WeeklyVolumeLookbackDays = 6;
    private const int DaysInAWeek = 7;
    private const int MondayOffsetAdjustment = 6;
    private const double GridWeekDivisor = 7.0;
    private const float MillilitersPerLiter = 1000f;

    public async Task<DashboardOverviewResponse> LoadOverviewAsync(
        Guid userId,
        DateOnly today,
        string? timeZoneId = null,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync<DashboardOverviewResponse>(async () =>
        {
            var targets = await targetStore.LoadAsync(userId, today, cancellationToken).ConfigureAwait(false);

            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var user = await dbContext.Users.AsNoTracking()
                .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
                .ConfigureAwait(false);

            var profile = await dbContext.UserProfiles.AsNoTracking()
                .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            var displayName = ResolveFirstName(profile?.Name ?? user?.Username ?? "Athlete");

            // 1. Cycle & Prediction
            var cycles = await dbContext.CycleLogs.AsNoTracking()
                .Where(c => c.UserId == userId && c.StartDate <= today)
                .OrderBy(c => c.StartDate)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var coreHistory = cycles.Select(c => new CycleObservation(
                c.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                c.EndDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                c.CycleLength)).ToArray();

            var latestCycle = cycles.LastOrDefault();
            var activeCycle = latestCycle is not null && (latestCycle.EndDate is null || latestCycle.EndDate > today)
                ? new CycleObservation(
                    latestCycle.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                    latestCycle.EndDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                    latestCycle.CycleLength)
                : null;

            var latestPhaseLog = await dbContext.CyclePhaseLogs.AsNoTracking()
                .Where(row => row.UserId == userId && row.LocalDate <= today)
                .OrderByDescending(row => row.LocalDate).ThenByDescending(row => row.Id)
                .Select(row => new { row.Phase, row.LocalDate })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            CyclePhaseObservation? manualPhaseLog = null;
            if (latestPhaseLog is not null &&
                !int.TryParse(latestPhaseLog.Phase, out _) &&
                Enum.TryParse<CyclePhase>(latestPhaseLog.Phase, true, out var loggedPhase) &&
                Enum.IsDefined(loggedPhase))
            {
                manualPhaseLog = new CyclePhaseObservation(
                    loggedPhase,
                    latestPhaseLog.LocalDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified));
            }

            var cycleLength = profile?.CycleLength > 0 ? profile.CycleLength : DefaultCycleLengthDays;
            var cycleEstimate = CyclePredictionDomainRule.CreatePrediction(
                activeCycle,
                coreHistory,
                cycleLength,
                today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                manualPhaseLog);

            var profilePhase = Enum.TryParse<CyclePhase>(profile?.CurrentCyclePhase, true, out var parsedProfilePhase)
                ? parsedProfilePhase
                : CyclePhase.Unknown;
            var currentPhase = DashboardPhaseSelectionRule.Resolve(cycleEstimate.CurrentPhase, profilePhase);
            var hasProfilePhase = cycleEstimate.CurrentPhase == CyclePhase.Unknown &&
                                  currentPhase != CyclePhase.Unknown;
            var predictionSource = hasProfilePhase ? "profile phase" : cycleEstimate.PredictionSource;
            var hasActiveCycle = activeCycle is not null;
            var currentCycleStartDate = latestCycle?.StartDate;
            var currentCycleDay = cycleEstimate.CurrentDay;
            var predictedCycleLength = cycleEstimate.PredictedCycleLength;
            var daysUntilPeriod = cycleEstimate.DaysUntilPeriod;
            var cycleCount = cycles.Count;

            // 2. Daily Readiness Check-In & Trends
            var trendStartDate = today.AddDays(-TrendWindowDays);
            var checkIns = await dbContext.DailyReadinessLogs.AsNoTracking()
                .Where(l => l.UserId == userId && l.Date >= trendStartDate && l.Date <= today)
                .OrderBy(l => l.Date)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var todayCheckIn = checkIns.FirstOrDefault(l => l.Date == today);
            var needsDailyCheckIn = todayCheckIn is null;

            var trendObservations = checkIns
                .Select(l => new DashboardTrendObservation(l.Date, (double?)l.WeightKg, l.ReadinessScore))
                .ToList();

            var trendPoints = DashboardTrendRule.Build(trendObservations, today)
                .Select(p => new DashboardTrendPointDto(p.Date, p.SmoothedWeightKg, p.ReadinessScore))
                .ToList();

            // 3. Nutrition Consumed & Targets
            var dailyNutritionLog = await dbContext.DailyNutritionLogs.AsNoTracking()
                .SingleOrDefaultAsync(l => l.UserId == userId && l.LocalDate == today && !l.IsDeleted, cancellationToken)
                .ConfigureAwait(false);

            float consumedCalories;
            float consumedProtein;
            float consumedCarbs;
            float consumedFats;
            var hydrationConsumed = "0.0 L";

            if (dailyNutritionLog is not null)
            {
                consumedCalories = (float)dailyNutritionLog.TotalCalories;
                consumedProtein = (float)dailyNutritionLog.TotalProtein;
                consumedCarbs = (float)dailyNutritionLog.TotalCarbs;
                consumedFats = (float)dailyNutritionLog.TotalFat;
                hydrationConsumed = $"{dailyNutritionLog.WaterIntakeMl / MillilitersPerLiter:F1} L";
            }
            else
            {
                var foodEntries = await dbContext.FoodItemEntries.AsNoTracking()
                    .Where(e => e.UserId == userId && e.LocalDate == today && !e.IsDeleted)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                consumedCalories = (float)foodEntries.Sum(e => e.Calories);
                consumedProtein = (float)foodEntries.Sum(e => e.Protein);
                consumedCarbs = (float)foodEntries.Sum(e => e.Carbs);
                consumedFats = (float)foodEntries.Sum(e => e.Fat);
            }

            // 4. Streaks
            var nutritionStreakDays = await ComputeNutritionStreakAsync(userId, today, cancellationToken)
                .ConfigureAwait(false);
            var workoutStreakDays = await ComputeWorkoutStreakAsync(userId, today, cancellationToken)
                .ConfigureAwait(false);

            // 5. Active Injuries
            var hasActiveInjuries = await dbContext.WorkoutInjuryLogs.AsNoTracking()
                .AnyAsync(l => l.UserId == userId && l.Status != "Cleared", cancellationToken)
                .ConfigureAwait(false);

            // 6. Today's Photo
            var hasTodayPhoto = await dbContext.PhotoJourneyEntries.AsNoTracking()
                .AnyAsync(p => p.UserId == userId && p.Date == today, cancellationToken)
                .ConfigureAwait(false);

            // 7. Workout Summary for Today
            var (workoutTitle, workoutSubtitle, workoutDurationText, workoutExercisesCount,
                 workoutIntensity, sessionProgressText, workoutActivityTag, workoutDayId,
                 activitySections) = await ResolveWorkoutSummaryAsync(userId, today, cancellationToken)
                .ConfigureAwait(false);

            // 8. Support Summary via Pure Domain Rule
            var calorieProgress = targets.Calories > 0
                ? Math.Clamp(consumedCalories / targets.Calories, 0f, 1f)
                : 0f;

            var cycleMetrics = new DashboardCycleMetrics(
                hasActiveCycle,
                daysUntilPeriod <= 0,
                cycleEstimate.PredictedNextPeriodDate is { } np ? DateOnly.FromDateTime(np) : null,
                cycleEstimate.PredictedOvulationDate is { } no ? DateOnly.FromDateTime(no) : null,
                currentCycleDay,
                predictedCycleLength,
                daysUntilPeriod);

            var supportResult = DashboardSupportRule.Build(
                cycleMetrics,
                currentPhase,
                calorieProgress,
                (float?)profile?.Weight,
                profile?.WorkoutDaysPerWeek ?? DefaultWorkoutDaysPerWeek,
                sessionProgressText,
                health: null,
                recordedReadinessScore: todayCheckIn?.ReadinessScore,
                hasActiveInjury: hasActiveInjuries,
                localSteps: todayCheckIn?.StepsYesterday ?? todayCheckIn?.Steps7dAvg,
                localAverageSleepHours: (double?)todayCheckIn?.SleepHours);

            var (weeklyNutrition, weeklyWeights, workoutProgress) = await LoadTrendSummariesAsync(
                userId, today, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new DashboardOverviewResponse(
                displayName,
                currentPhase.ToString(),
                predictionSource,
                hasActiveCycle,
                currentCycleStartDate,
                currentCycleDay,
                predictedCycleLength,
                daysUntilPeriod,
                cycleCount,
                needsDailyCheckIn,
                hasActiveInjuries,
                consumedCalories,
                consumedCarbs,
                consumedFats,
                consumedProtein,
                targets.Calories,
                targets.Carbs,
                targets.Fats,
                targets.Protein,
                workoutStreakDays,
                nutritionStreakDays,
                hasTodayPhoto,
                supportResult.CycleInsightText,
                hydrationConsumed,
                supportResult.HydrationTarget,
                supportResult.SleepGoal,
                supportResult.ReadinessScore,
                supportResult.ReadinessLabel,
                supportResult.RecoveryScore,
                supportResult.RecoveryLabel,
                workoutTitle,
                workoutSubtitle,
                workoutDurationText,
                workoutExercisesCount,
                workoutIntensity,
                sessionProgressText,
                workoutActivityTag,
                workoutDayId,
                activitySections,
                trendPoints,
                weeklyNutrition,
                weeklyWeights,
                workoutProgress,
                todayCheckIn is null ? null : TodayReadinessCheckInMapper.Map(
                    todayCheckIn.PlanningSleepQuality,
                    todayCheckIn.Energy,
                    todayCheckIn.PlanningSoreness,
                    todayCheckIn.PlanningStress,
                    todayCheckIn.Date,
                    todayCheckIn.PlanningTimeZoneId,
                    todayCheckIn.PlanningRecordedAtUtc is { } recorded
                        ? new DateTimeOffset(DateTime.SpecifyKind(recorded, DateTimeKind.Utc))
                        : null,
                    todayCheckIn.PlanningRevision,
                    timeProvider.GetUtcNow(),
                    timeZoneId));
        }).ConfigureAwait(false);

    private async Task<(DashboardWeeklyNutritionDto?, IReadOnlyList<DashboardWeeklyWeightDto>,
        DashboardWorkoutProgressDto)> LoadTrendSummariesAsync(
        Guid userId, DateOnly today, CancellationToken cancellationToken)
    {
        var weekStart = DashboardTrendSummaryRule.StartOfWeek(today);
        var dailyNutrition = await dbContext.DailyNutritionLogs.AsNoTracking()
            .Where(row => row.UserId == userId && !row.IsDeleted &&
                          row.LocalDate >= weekStart && row.LocalDate <= today)
            .Select(row => new { row.LocalDate, row.TotalCalories, row.TotalProtein, row.TotalCarbs, row.TotalFat })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var foodEntries = await dbContext.FoodItemEntries.AsNoTracking()
            .Where(row => row.UserId == userId && !row.IsDeleted &&
                          row.LocalDate >= weekStart && row.LocalDate <= today)
            .Select(row => new { row.LocalDate, row.Calories, row.Protein, row.Carbs, row.Fat })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var nutritionObservations = dailyNutrition
            .Select(row => new DashboardNutritionObservation(row.LocalDate,
                (double)row.TotalCalories, (double)row.TotalProtein, (double)row.TotalCarbs,
                (double)row.TotalFat, true))
            .Concat(foodEntries.Select(row => new DashboardNutritionObservation(row.LocalDate,
                (double)row.Calories, (double)row.Protein, (double)row.Carbs, (double)row.Fat, false)));
        var weeklyTotals = DashboardTrendSummaryRule.BuildWeeklyNutrition(nutritionObservations, today);
        var weeklyNutrition = weeklyTotals is null ? null : new DashboardWeeklyNutritionDto(
            weeklyTotals.WeekStart, weeklyTotals.Calories, weeklyTotals.ProteinGrams,
            weeklyTotals.CarbsGrams, weeklyTotals.FatGrams);

        var weightStart = today.AddDays(-WeeklyWeightLookbackDays);
        var weightRows = await dbContext.DailyReadinessLogs.AsNoTracking()
            .Where(row => row.UserId == userId && row.Date >= weightStart && row.Date <= today &&
                          row.WeightKg != null)
            .Select(row => new { row.Date, row.WeightKg })
            .OrderBy(row => row.Date)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var weeklyWeights = DashboardTrendSummaryRule.BuildWeeklyWeights(
                weightRows.Select(row => new DashboardWeightObservation(row.Date, (double)row.WeightKg!.Value)),
                today)
            .Select(row => new DashboardWeeklyWeightDto(row.WeekStart, row.WeightKg))
            .ToArray();

        var workoutLogStart = today.AddDays(InclusiveRangeDayAdjustment - WorkoutProgressLookbackDays);
        var sessionStart = workoutLogStart
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var sessionEnd = today.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var sessions = await dbContext.WorkoutSessionLogs.AsNoTracking()
            .Where(row => row.UserId == userId && !row.IsDeleted && row.CompletedAt != null &&
                          row.StartedAt >= sessionStart && row.StartedAt <= sessionEnd)
            .Select(row => new { row.Id, row.TotalDurationSeconds, row.TotalVolumeKg })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var sessionIds = sessions.Select(row => row.Id).ToArray();
        var sets = await dbContext.ExerciseSetLogs.AsNoTracking()
            .Where(row => row.UserId == userId && sessionIds.Contains(row.WorkoutSessionLogId) &&
                          !row.IsDeleted && !row.IsWarmup)
            .Select(row => new { row.WorkoutSessionLogId, row.WeightKg })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var activeSessionIds = sets.Select(row => row.WorkoutSessionLogId).ToHashSet();
        var workoutObservations = sessions
            .Where(row => activeSessionIds.Contains(row.Id))
            .Select(row => new DashboardWorkoutProgressObservation(false, null,
                (double)row.TotalVolumeKg, row.TotalDurationSeconds, null, null))
            .Concat(sets.Select(row => new DashboardWorkoutProgressObservation(false,
                (double)row.WeightKg, null, null, null, null)))
            .ToList();

        var exerciseRows = await (
            from exercise in dbContext.WorkoutExerciseLogs.AsNoTracking()
            join log in dbContext.WorkoutLogs.AsNoTracking() on exercise.WorkoutLogId equals log.Id
            join day in dbContext.WorkoutDays.AsNoTracking() on log.WorkoutDayId equals day.Id
            where exercise.UserId == userId && log.UserId == userId && day.UserId == userId &&
                  log.Date >= workoutLogStart && log.Date <= today
            select new { day.WorkoutType, exercise.WeightKg, exercise.CompletedDurationSeconds,
                exercise.DistanceKm, exercise.PaceSecondsPerKm })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        workoutObservations.AddRange(exerciseRows.Select(row => new DashboardWorkoutProgressObservation(
            row.WorkoutType.Equals("Rest", StringComparison.OrdinalIgnoreCase) ||
            row.WorkoutType.Equals("Recovery", StringComparison.OrdinalIgnoreCase),
            row.WeightKg is { } weight ? (double)weight : null,
            null,
            row.CompletedDurationSeconds,
            row.DistanceKm is { } distance ? (double)distance : null,
            row.PaceSecondsPerKm)));
        var progress = DashboardTrendSummaryRule.BuildWorkoutProgress(workoutObservations);
        var workoutProgress = new DashboardWorkoutProgressDto(progress.HeaviestLiftKg,
            progress.HighestSessionVolumeKg, progress.LongestSessionMinutes,
            progress.LongestDistanceKm, progress.FastestPaceSecondsPerKm);

        return (weeklyNutrition, weeklyWeights, workoutProgress);
    }

    public async Task<DashboardCalendarResponse> LoadCalendarAsync(
        Guid userId,
        int year,
        int month,
        DateOnly today,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync<DashboardCalendarResponse>(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var startOfMonth = new DateOnly(year, month, 1);
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var offset = ((int)startOfMonth.DayOfWeek + MondayOffsetAdjustment) % DaysInAWeek;
            var cellCount = (int)Math.Ceiling((offset + daysInMonth) / GridWeekDivisor) * DaysInAWeek;
            var startDate = startOfMonth.AddDays(-offset);
            var endDate = startDate.AddDays(cellCount - 1);

            var startDateTime = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var endDateTime = endDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

            var workoutSessions = await dbContext.WorkoutSessionLogs.AsNoTracking()
                .Where(s => s.UserId == userId && s.CompletedAt != null && s.StartedAt >= startDateTime && s.StartedAt <= endDateTime)
                .Select(s => DateOnly.FromDateTime(s.StartedAt))
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var workoutSet = new HashSet<DateOnly>(workoutSessions);

            var mealDates = await dbContext.LoggedMeals.AsNoTracking()
                .Where(m => m.UserId == userId && m.Date >= startDate && m.Date <= endDate)
                .Select(m => m.Date)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var foodDates = await dbContext.FoodItemEntries.AsNoTracking()
                .Where(f => f.UserId == userId && !f.IsDeleted && f.LocalDate >= startDate && f.LocalDate <= endDate)
                .Select(f => f.LocalDate)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var mealSet = new HashSet<DateOnly>(mealDates);
            mealSet.UnionWith(foodDates);

            var activePlan = await dbContext.WorkoutPlans.AsNoTracking()
                .Where(p => p.UserId == userId && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            var plannedDaysSet = new HashSet<int>();
            if (activePlan is not null)
            {
                var plannedDays = await dbContext.WorkoutDays.AsNoTracking()
                    .Where(d => d.UserId == userId && d.WorkoutPlanId == activePlan.Id && !d.IsRemovedByReadiness)
                    .Select(d => d.DayOfWeek)
                    .Distinct()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                plannedDaysSet = new HashSet<int>(plannedDays);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            var days = new List<DashboardCalendarDayDto>(cellCount);
            for (var i = 0; i < cellCount; i++)
            {
                var date = startDate.AddDays(i);
                var isCurrentMonth = date.Month == month && date.Year == year;
                var isToday = date == today;
                var hasWorkout = workoutSet.Contains(date);
                var hasMealLogged = mealSet.Contains(date);
                var hasPlannedWorkout = isCurrentMonth &&
                                        plannedDaysSet.Contains((int)date.DayOfWeek) &&
                                        !hasWorkout &&
                                        date >= today;

                days.Add(new DashboardCalendarDayDto(
                    date,
                    isCurrentMonth,
                    isToday,
                    hasWorkout,
                    hasPlannedWorkout,
                    hasMealLogged));
            }

            return new DashboardCalendarResponse(days);
        }).ConfigureAwait(false);

    private async Task<int> ComputeNutritionStreakAsync(
        Guid userId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var dates = await dbContext.DailyNutritionLogs.AsNoTracking()
            .Where(l => l.UserId == userId && !l.IsDeleted && l.LocalDate <= today && l.TotalCalories > 0)
            .Select(l => l.LocalDate)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var foodDates = await dbContext.FoodItemEntries.AsNoTracking()
            .Where(e => e.UserId == userId && !e.IsDeleted && e.LocalDate <= today)
            .Select(e => e.LocalDate)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var allDates = new HashSet<DateOnly>(dates);
        allDates.UnionWith(foodDates);

        if (allDates.Count == 0)
            return 0;

        var current = today;
        if (!allDates.Contains(current))
        {
            current = today.AddDays(-1);
            if (!allDates.Contains(current))
                return 0;
        }

        var streak = 0;
        while (allDates.Contains(current))
        {
            streak++;
            current = current.AddDays(-1);
        }

        return streak;
    }

    private async Task<int> ComputeWorkoutStreakAsync(
        Guid userId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var sessions = await dbContext.WorkoutSessionLogs.AsNoTracking()
            .Where(s => s.UserId == userId && s.CompletedAt != null && DateOnly.FromDateTime(s.StartedAt) <= today)
            .Select(s => DateOnly.FromDateTime(s.StartedAt))
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var allDates = new HashSet<DateOnly>(sessions);
        if (allDates.Count == 0)
            return 0;

        var current = today;
        if (!allDates.Contains(current))
        {
            current = today.AddDays(-1);
            if (!allDates.Contains(current))
                return 0;
        }

        var streak = 0;
        while (allDates.Contains(current))
        {
            streak++;
            current = current.AddDays(-1);
        }

        return streak;
    }

    private async Task<(string Title, string Subtitle, string DurationText, string ExercisesCount,
        string Intensity, string SessionProgressText, string ActivityTag, int WorkoutDayId,
        IReadOnlyList<DashboardWorkoutActivityDto> Sections)> ResolveWorkoutSummaryAsync(
        Guid userId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var todayStart = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var todayEnd = today.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var todaySession = await dbContext.WorkoutSessionLogs.AsNoTracking()
            .Where(s => s.UserId == userId && s.StartedAt >= todayStart && s.StartedAt <= todayEnd)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (todaySession is not null)
        {
            var isFinished = todaySession.CompletedAt is not null;
            var durationMinutes = Math.Max(1, todaySession.TotalDurationSeconds / 60);
            var setsCount = await dbContext.ExerciseSetLogs.AsNoTracking()
                .Where(s => s.UserId == userId && s.WorkoutSessionLogId == todaySession.Id && !s.IsDeleted)
                .CountAsync(cancellationToken)
                .ConfigureAwait(false);

            return (
                todaySession.WorkoutName,
                isFinished ? "Completed Today" : "In Progress",
                $"{durationMinutes} min",
                $"{setsCount} sets completed",
                "High",
                isFinished ? $"Completed ({todaySession.TotalVolumeKg:N0} kg volume)" : "Active Session",
                "Strength",
                1,
                [new DashboardWorkoutActivityDto("Strength", todaySession.WorkoutName, setsCount)]);
        }

        var activePlan = await dbContext.WorkoutPlans.AsNoTracking()
            .Where(p => p.UserId == userId && p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (activePlan is not null)
        {
            var dayOfWeek = (int)today.DayOfWeek;
            var plannedDay = await dbContext.WorkoutDays.AsNoTracking()
                .Where(d => d.UserId == userId && d.WorkoutPlanId == activePlan.Id &&
                            d.DayOfWeek == dayOfWeek && !d.IsRemovedByReadiness)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (plannedDay is not null)
            {
                var exercisesCount = await dbContext.WorkoutDayExercises.AsNoTracking()
                    .Where(e => e.UserId == userId && e.WorkoutDayId == plannedDay.Id && !e.IsRemovedByReadiness)
                    .CountAsync(cancellationToken)
                    .ConfigureAwait(false);

                return (
                    plannedDay.Name,
                    $"{plannedDay.WorkoutType} Focus",
                    "45-60 min",
                    $"{exercisesCount} exercises",
                    "Moderate",
                    "Scheduled for today",
                    plannedDay.WorkoutType,
                    MuscleCuties.Infrastructure.PostgreSql.Workout.Session.PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(plannedDay.Id),
                    [new DashboardWorkoutActivityDto(plannedDay.WorkoutType, plannedDay.Name, exercisesCount)]);
            }
        }

        return (
            "Rest & Recovery",
            "Active Recovery",
            "30 min",
            "0 exercises",
            "Low",
            "Rest day",
            "Recovery",
            0,
            [new DashboardWorkoutActivityDto("Recovery", "Rest & Mobility", 0)]);
    }

    private static string ResolveFirstName(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
            ? "Athlete"
            : trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
    }
}
