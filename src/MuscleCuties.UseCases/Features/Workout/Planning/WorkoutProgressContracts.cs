namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>The time zone the client is now in, which decides where a day starts.</summary>
public sealed record WorkoutForegroundTimeZoneRequest(string TimeZoneId, long ExpectedVersion, bool IsForeground);
/// <summary>The plan's progress: what is done and what remains.</summary>
public sealed record WorkoutProgressResponse(string Status, string? TimeZoneId, long TimeZoneVersion,
    string? ExperienceLevel, int CreditedDays, long ExperienceVersion, bool PromotionPending);

/// <summary>The progress read.</summary>
public interface IWorkoutProgressStore
{
    Task<WorkoutProgressResponse> ReadAsync(Guid userId, CancellationToken cancellationToken);
    Task<WorkoutProgressResponse> AcceptForegroundTimeZoneAsync(Guid userId, WorkoutForegroundTimeZoneRequest request,
        CancellationToken cancellationToken);
}

/// <summary>Reconciles the plan after a missed or shifted day.</summary>
public interface IWorkoutPlanReconciler
{
    Task<WorkoutReconciliationOutcome> ReconcileAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>What the reconciliation did, so the client can tell the plan changed.</summary>
public enum WorkoutReconciliationOutcome { Completed, Deferred, NeedsReview }

/// <summary>The reconciliation result with the plan it produced.</summary>
public sealed record WorkoutReconciliationResponse(string Status);
