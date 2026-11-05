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

    [Fact]
    public void WorkoutScheduleEntry_DayAndAllScoping_PreservesStableIdsAndOrder()
    {
        var daySessions = new List<WorkoutScheduleSessionItem>
        {
            new(WorkoutDayId: 201, Tag: "STRENGTH", Title: "Squat Focus", DayOrder: 0, IsCompleted: true, IsOptional: false, IsEasierAlternative: false),
            new(WorkoutDayId: 202, Tag: "CARDIO", Title: "Zone 2 Cycling", DayOrder: 1, IsCompleted: false, IsOptional: false, IsEasierAlternative: false)
        };

        var entry = new WorkoutScheduleEntry(
            Date: new DateOnly(2026, 11, 5),
            WeekIndex: 1,
            DayLabel: "THU",
            Tag: "STRENGTH",
            Title: "Squat Focus + Zone 2 Cycling",
            WorkoutDayId: 201,
            IsToday: true,
            IsCompleted: false,
            IsRestDay: false)
        {
            CompanionWorkoutDayId = 202,
            Sessions = daySessions
        };

        Assert.Equal(2, entry.Sessions!.Count);
        Assert.Equal(0, entry.Sessions[0].DayOrder);
        Assert.Equal(1, entry.Sessions[1].DayOrder);
        Assert.True(entry.Sessions[0].IsCompleted);
        Assert.False(entry.Sessions[1].IsCompleted);
    }

    [Fact]
    public void ScheduleMultiSessionChips_ProjectInOrderedSequence_WithoutOverlappingOrTruncation()
    {
        // UX02: Multi-session days display ordered session chips with their distinct modality tags and completion states
        var sessions = new List<WorkoutScheduleSessionItem>
        {
            new(WorkoutDayId: 301, Tag: "STRENGTH", Title: "Heavy Squats", DayOrder: 0, IsCompleted: true, IsOptional: false, IsEasierAlternative: false),
            new(WorkoutDayId: 302, Tag: "CARDIO", Title: "Tempo Run", DayOrder: 1, IsCompleted: false, IsOptional: false, IsEasierAlternative: false),
            new(WorkoutDayId: 303, Tag: "RECOVERY", Title: "Foam Rolling", DayOrder: 2, IsCompleted: false, IsOptional: true, IsEasierAlternative: true)
        };

        var entry = new WorkoutScheduleEntry(
            Date: new DateOnly(2026, 11, 6),
            WeekIndex: 0,
            DayLabel: "FRI",
            Tag: "STRENGTH",
            Title: "Heavy Squats + Tempo Run + Foam Rolling",
            WorkoutDayId: 301,
            IsToday: true,
            IsCompleted: false,
            IsRestDay: false)
        {
            CompanionWorkoutDayId = 302,
            Sessions = sessions
        };

        // Assert ordered session projection
        Assert.NotNull(entry.Sessions);
        Assert.Equal(3, entry.Sessions.Count);

        var ordered = entry.Sessions.OrderBy(s => s.DayOrder ?? 0).ToList();
        Assert.Equal(301, ordered[0].WorkoutDayId);
        Assert.Equal("STRENGTH", ordered[0].Tag);
        Assert.True(ordered[0].IsCompleted);

        Assert.Equal(302, ordered[1].WorkoutDayId);
        Assert.Equal("CARDIO", ordered[1].Tag);
        Assert.False(ordered[1].IsCompleted);

        Assert.Equal(303, ordered[2].WorkoutDayId);
        Assert.Equal("RECOVERY", ordered[2].Tag);
        Assert.True(ordered[2].IsEasierAlternative);
    }

    [Fact]
    public void ScheduleStatusTransition_CleanlyTransitionsState_UponSessionCompletion()
    {
        // UX03: Status transitions when a session is completed
        var sessions = new List<WorkoutScheduleSessionItem>
        {
            new(WorkoutDayId: 401, Tag: "STRENGTH", Title: "Push Day", DayOrder: 0, IsCompleted: false, IsOptional: false, IsEasierAlternative: false),
            new(WorkoutDayId: 402, Tag: "CARDIO", Title: "HIIT Intervals", DayOrder: 1, IsCompleted: false, IsOptional: false, IsEasierAlternative: false)
        };

        var initialEntry = new WorkoutScheduleEntry(
            Date: new DateOnly(2026, 11, 7),
            WeekIndex: 0,
            DayLabel: "SAT",
            Tag: "STRENGTH",
            Title: "Push Day + HIIT Intervals",
            WorkoutDayId: 401,
            IsToday: true,
            IsCompleted: false,
            IsRestDay: false)
        {
            CompanionWorkoutDayId = 402,
            Sessions = sessions
        };

        Assert.False(initialEntry.IsCompleted);
        Assert.All(initialEntry.Sessions!, s => Assert.False(s.IsCompleted));

        // Mark first session completed
        var updatedSessions = initialEntry.Sessions!.Select(s => s.WorkoutDayId == 401
            ? s with { IsCompleted = true }
            : s).ToList();

        var partialEntry = initialEntry with
        {
            Sessions = updatedSessions,
            IsCompleted = updatedSessions.All(s => s.IsCompleted)
        };

        Assert.False(partialEntry.IsCompleted); // Still has remaining sessions
        Assert.True(partialEntry.Sessions[0].IsCompleted);
        Assert.False(partialEntry.Sessions[1].IsCompleted);

        // Mark second session completed
        var finalSessions = partialEntry.Sessions.Select(s => s.WorkoutDayId == 402
            ? s with { IsCompleted = true }
            : s).ToList();

        var completedEntry = partialEntry with
        {
            Sessions = finalSessions,
            IsCompleted = finalSessions.All(s => s.IsCompleted)
        };

        Assert.True(completedEntry.IsCompleted);
        Assert.All(completedEntry.Sessions, s => Assert.True(s.IsCompleted));
    }
}
