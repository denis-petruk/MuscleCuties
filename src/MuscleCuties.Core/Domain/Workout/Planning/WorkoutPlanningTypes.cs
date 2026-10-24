namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum ActivityDemandTag
{
    Pulling,
    Grip,
    LowerBody,
    UpperBody,
    Impact,
    Conditioning,
    Mobility
}

public enum PlannedSessionState
{
    Planned,
    Completed,
    PartiallyCompleted,
    Skipped
}

public enum SessionPurpose
{
    Strength,
    Conditioning,
    Mobility,
    Recovery,
    Skill,
    UserActivity,
    Climbing,
    Yoga,
    Rest
}

public enum ActivityModality
{
    Strength,
    Walking,
    Running,
    Cycling,
    Elliptical,
    Rowing,
    Swimming,
    Yoga,
    Mobility,
    Climbing,
    Other
}

public enum IntendedIntensity
{
    Unspecified,
    Easy,
    Moderate,
    Vigorous
}

public enum SessionTrainingStyle
{
    Unspecified,
    General,
    HighVolume,
    HighIntensity,
    Continuous,
    Intervals,
    Hiit,
    Gentle,
    Demanding
}
