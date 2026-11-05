using System.Text.Json;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

public sealed class ScheduleContractCompatibilityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void WorkoutScheduleEntry_PreservesPrimaryAndCompanionFields_WhenMultipleSessionsExist()
    {
        var sessions = new List<WorkoutScheduleSessionItem>
        {
            new(WorkoutDayId: 101, Tag: "STRENGTH", Title: "Upper Body", DayOrder: 0, IsCompleted: false, IsOptional: false, IsEasierAlternative: false),
            new(WorkoutDayId: 102, Tag: "CARDIO", Title: "Steady Running", DayOrder: 1, IsCompleted: false, IsOptional: false, IsEasierAlternative: false),
            new(WorkoutDayId: 103, Tag: "RECOVERY", Title: "Hip Mobility", DayOrder: 2, IsCompleted: false, IsOptional: true, IsEasierAlternative: false),
            new(WorkoutDayId: 104, Tag: "WALKING", Title: "Evening Walk", DayOrder: 3, IsCompleted: false, IsOptional: true, IsEasierAlternative: false)
        };

        var entry = new WorkoutScheduleEntry(
            Date: new DateOnly(2026, 11, 4),
            WeekIndex: 0,
            DayLabel: "WED",
            Tag: "STRENGTH",
            Title: "Upper Body + Steady Running + Hip Mobility + Evening Walk",
            WorkoutDayId: 101,
            IsToday: true,
            IsCompleted: false,
            IsRestDay: false)
        {
            CompanionWorkoutDayId = 102,
            Sessions = sessions
        };

        var json = JsonSerializer.Serialize(entry, JsonOptions);
        var roundTripped = JsonSerializer.Deserialize<WorkoutScheduleEntry>(json, JsonOptions);

        Assert.NotNull(roundTripped);
        Assert.Equal(101, roundTripped.WorkoutDayId);
        Assert.Equal(102, roundTripped.CompanionWorkoutDayId);
        Assert.NotNull(roundTripped.Sessions);
        Assert.Equal(4, roundTripped.Sessions.Count);
        Assert.Equal(103, roundTripped.Sessions[2].WorkoutDayId);
        Assert.Equal("RECOVERY", roundTripped.Sessions[2].Tag);
        Assert.Equal(104, roundTripped.Sessions[3].WorkoutDayId);
        Assert.True(roundTripped.Sessions[3].IsOptional);
    }

    [Fact]
    public void WorkoutScheduleEntry_DeserializesLegacyPayloadWithoutSessionsGracefully()
    {
        var legacyJson = """
        {
            "date": "2026-11-04",
            "weekIndex": 0,
            "dayLabel": "WED",
            "tag": "STRENGTH",
            "title": "Upper Body",
            "workoutDayId": 101,
            "isToday": true,
            "isCompleted": false,
            "isRestDay": false,
            "isOptional": false,
            "isEasierAlternative": false,
            "companionWorkoutDayId": 102
        }
        """;

        var roundTripped = JsonSerializer.Deserialize<WorkoutScheduleEntry>(legacyJson, JsonOptions);

        Assert.NotNull(roundTripped);
        Assert.Equal(101, roundTripped.WorkoutDayId);
        Assert.Equal(102, roundTripped.CompanionWorkoutDayId);
        Assert.Null(roundTripped.Sessions);
    }
}
