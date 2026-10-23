using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;

using MuscleCuties.Core.Models.Nutrition.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Nutrition.Targets;

namespace MuscleCuties.Infrastructure.PostgreSql.Nutrition.Targets;

/// <summary>Owner-scoped calculation over persisted profile facts; unknown fields in saved JSON are never rewritten.</summary>
public sealed class PostgresNutritionTargetProjectionStore(PostgresDbContext db) : IServerNutritionTargetProjectionStore
{
    private const int EarliestValidBirthYear = 1900;
    private const float MinimumAdultAge = 18f;
    private const float MinimumHeightCm = 100f;
    private const float MaximumHeightCm = 300f;
    private const float MinimumWeightKg = 30f;
    private const float MaximumWeightKg = 500f;

    public async Task<ServerNutritionTargetProjection> LoadAsync(Guid userId, DateOnly date, CancellationToken cancellationToken) =>
        await db.Database.CreateExecutionStrategy().ExecuteAsync<ServerNutritionTargetProjection>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(db, userId, cancellationToken).ConfigureAwait(false);
            var profile = await db.UserProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken).ConfigureAwait(false);
            if (profile is null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new(ServerNutritionTargetRule.GeneralCalories, ServerNutritionTargetRule.GeneralProtein, ServerNutritionTargetRule.GeneralCarbs, ServerNutritionTargetRule.GeneralFats, "GeneralStarting", "nutrition.target_source.general_starting", null, null, null, null, null, false, false,
                    CyclePhase.Unknown, "cycle.phase.unknown", "nutrition.targets.general_starting");
            }

            var cycles = await db.CycleLogs.AsNoTracking().Where(x => x.UserId == userId && x.StartDate <= date)
                .OrderBy(x => x.StartDate).Select(x => new { x.StartDate, x.EndDate, x.CycleLength })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var coreHistory = cycles.Select(x => new CycleObservation(
                x.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                x.EndDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), x.CycleLength)).ToArray();
            var latest = cycles.LastOrDefault();
            var active = latest is not null && (latest.EndDate is null || latest.EndDate > date)
                ? new CycleObservation(latest.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                    latest.EndDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), latest.CycleLength)
                : null;
            var cycleEstimate = CyclePredictionDomainRule.CreatePrediction(active, coreHistory,
                profile.CycleLength, date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified));
            var observedPhase = cycleEstimate.CurrentPhase;
            var context = ReproductiveContextCodec.Parse(profile.ReproductiveContextJson);
            var phase = NutritionTargetPhaseRule.ForAutomaticTargets(observedPhase, context);
            var age = profile.DateOfBirth is { } birth ? date.Year - birth.Year - (birth > date.AddYears(-(date.Year - birth.Year)) ? 1 : 0) : 0;
            var height = (float)profile.Height;
            var weight = (float)profile.Weight;
            var usable = BirthDateUsable(profile.DateOfBirth, date, age) && float.IsFinite(height) && height >= MinimumHeightCm && height <= MaximumHeightCm && float.IsFinite(weight) && weight >= MinimumWeightKg && weight <= MaximumWeightKg;
            if (!usable)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new(ServerNutritionTargetRule.GeneralCalories, ServerNutritionTargetRule.GeneralProtein, ServerNutritionTargetRule.GeneralCarbs, ServerNutritionTargetRule.GeneralFats, "GeneralStarting", "nutrition.target_source.general_starting", null, null, null, null, null, false, false,
                    phase, PhaseCode(phase), "nutrition.targets.general_starting");
            }

            if (!Enum.TryParse<UserGoal>(profile.Goal, true, out var goal) || !Enum.IsDefined(goal))
            {
                var unavailable = ServerNutritionTargetRule.Unavailable();
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new(unavailable.Calories, unavailable.Protein, unavailable.Carbs, unavailable.Fats,
                    unavailable.Provenance.Source.ToString(), "nutrition.target_source.unavailable", null, null, null, null,
                    null, false, false, phase, PhaseCode(phase),
                    context.AllowsCyclePhaseTrainingAdjustments ? "nutrition.targets.personalized" : "nutrition.targets.phase_guidance_unavailable");
            }
            var pace = profile.WeightGoalPace.Equals("Aggressive", StringComparison.OrdinalIgnoreCase) ? WeightGoalPace.Aggressive : WeightGoalPace.Steady;
            var saved = ParseGoals(profile.NutritionGoalsJson);
            var result = ServerNutritionTargetRule.Calculate(age, height, weight, profile.WorkoutDaysPerWeek, goal, pace,
                profile.TrainingExperienceLevel, phase, context, saved);
            var provenance = result.Provenance;
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            var sourceCode = provenance.Source switch
            {
                NutritionTargetSource.SavedOverride => "nutrition.target_source.saved_override",
                NutritionTargetSource.Calculated => "nutrition.target_source.calculated",
                _ => "nutrition.target_source.unavailable"
            };
            return new(result.Calories, result.Protein, result.Carbs, result.Fats, provenance.Source.ToString(), sourceCode, provenance.StartingEnergyCalories,
                provenance.GoalAdjustmentCalories, provenance.PhaseAdjustmentCalories, provenance.CalculatedCalories,
                provenance.SavedRequestedCalories, provenance.HasSavedMacroOverride, provenance.SavedRequestDiffersFromShownTarget,
                phase, PhaseCode(phase), context.AllowsCyclePhaseTrainingAdjustments ? "nutrition.targets.personalized" : "nutrition.targets.phase_guidance_unavailable");
        }).ConfigureAwait(false);

    private static ProfileNutritionGoals ParseGoals(string json)
    {
        try { return JsonSerializer.Deserialize<ProfileNutritionGoals>(json) ?? ProfileNutritionGoals.Empty; }
        catch (JsonException) { return ProfileNutritionGoals.Empty; }
    }

    private static bool BirthDateUsable(DateOnly? birth, DateOnly date, int age) => birth.HasValue && birth.Value.Year > EarliestValidBirthYear && birth.Value <= date && age >= MinimumAdultAge;
    private static string PhaseCode(CyclePhase phase) => $"cycle.phase.{phase.ToString().ToLowerInvariant()}";
}
