namespace MuscleCuties.UseCases.Features.Workout.Planning;

public sealed record WorkoutForegroundTimeZoneRequest(string TimeZoneId, long ExpectedVersion, bool IsForeground);
public sealed record WorkoutProgressResponse(string Status, string? TimeZoneId, long TimeZoneVersion,
    string? ExperienceLevel, int CreditedDays, long ExperienceVersion, bool PromotionPending);

public interface IWorkoutProgressStore
{
    Task<WorkoutProgressResponse> ReadAsync(Guid userId, CancellationToken cancellationToken);
    Task<WorkoutProgressResponse> AcceptForegroundTimeZoneAsync(Guid userId, WorkoutForegroundTimeZoneRequest request,
        CancellationToken cancellationToken);
}

public interface IWorkoutPlanReconciler
{
    Task ReconcileAsync(Guid userId, CancellationToken cancellationToken = default);
}

