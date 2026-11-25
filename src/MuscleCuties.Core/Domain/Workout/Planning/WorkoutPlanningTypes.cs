namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>How demanding an activity is, which decides where in the week it may sit.</summary>
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

/// <summary>Where a planned session stands, from planned through performed or skipped.</summary>
public enum PlannedSessionState
{
    Planned,
    Completed,
    PartiallyCompleted,
    Skipped
}

/// <summary>What a session is for, which decides its volume and effort.</summary>
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

/// <summary>How an activity is performed, which decides the metrics it records.</summary>
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

/// <summary>The intensity the plan intends, before any reduction is applied.</summary>
public enum IntendedIntensity
{
    Unspecified,
    Easy,
    Moderate,
    Vigorous
}

/// <summary>The training style a session follows.</summary>
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
