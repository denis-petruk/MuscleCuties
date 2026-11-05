using System.Text.Json;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

public sealed class ActivityActualsContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void LogWorkoutSessionRequest_BackwardCompatible_WhenActivityActualsOmitted()
    {
        const string legacyJson = """
            {
                "workoutDayId": 42,
                "date": "2026-11-05",
                "exercises": [
                    {
                        "workoutDayExerciseId": 1,
                        "exerciseId": 101,
                        "completedSets": 3,
                        "completedReps": 10,
                        "weightKg": 60.0
                    }
                ]
            }
            """;

        var request = JsonSerializer.Deserialize<LogWorkoutSessionRequest>(legacyJson, JsonOptions);

        Assert.NotNull(request);
        Assert.Equal(42, request.WorkoutDayId);
        Assert.Equal(new DateOnly(2026, 11, 5), request.Date);
        Assert.NotNull(request.Exercises);
        Assert.Single(request.Exercises);
        Assert.Null(request.ActivityActuals);
    }

    [Fact]
    public void LogWorkoutSessionRequest_SerializesAndRoundTripsActivityActuals()
    {
        var actuals = new List<ActivityActualLogDto>
        {
            new(
                WorkoutDayActivityId: 10,
                ActivityType: WorkoutActivityDto.Running,
                DurationSeconds: 1800,
                DistanceKm: 5.0m,
                PaceSecondsPerKm: 360,
                AverageHeartRateBpm: 155,
                EffortRpe: 7,
                Notes: "Tempo run around park")
        };

        var request = new LogWorkoutSessionRequest(
            WorkoutDayId: 50,
            Date: new DateOnly(2026, 11, 5),
            Exercises: null,
            PlannedSessionId: Guid.NewGuid(),
            ActivityActuals: actuals);

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var roundTripped = JsonSerializer.Deserialize<LogWorkoutSessionRequest>(json, JsonOptions);

        Assert.NotNull(roundTripped);
        Assert.Null(roundTripped.Exercises);
        Assert.NotNull(roundTripped.ActivityActuals);
        Assert.Single(roundTripped.ActivityActuals);

        var activity = roundTripped.ActivityActuals[0];
        Assert.Equal(WorkoutActivityDto.Running, activity.ActivityType);
        Assert.Equal(1800, activity.DurationSeconds);
        Assert.Equal(5.0m, activity.DistanceKm);
        Assert.Equal(360, activity.PaceSecondsPerKm);
        Assert.Equal(155, activity.AverageHeartRateBpm);
        Assert.Equal(7, activity.EffortRpe);
        Assert.Equal("Tempo run around park", activity.Notes);
    }

    [Fact]
    public void SwimmingActivityActualLog_SupportsMetersAnd100mPace()
    {
        var swimActual = new ActivityActualLogDto(
            WorkoutDayActivityId: null,
            ActivityType: WorkoutActivityDto.Swimming,
            DurationSeconds: 2400,
            DistanceMeters: 1500,
            PaceSecondsPer100m: 160,
            StrokeCount: 750,
            EffortRpe: 6);

        var json = JsonSerializer.Serialize(swimActual, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ActivityActualLogDto>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(WorkoutActivityDto.Swimming, deserialized.ActivityType);
        Assert.Equal(1500, deserialized.DistanceMeters);
        Assert.Equal(160, deserialized.PaceSecondsPer100m);
        Assert.Equal(750, deserialized.StrokeCount);
    }
}
