using System.Collections.Frozen;
using System.Collections.ObjectModel;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>How a template divides the week between muscle groups.</summary>
public enum StrengthTemplateSplit
{
    FullBody,
    UpperLower
}

/// <summary>The stable slot identity inside a template, which exercise choices are stored against.</summary>
public enum StrengthTemplateSlotId
{
    KneeDominant,
    HipDominant,
    UpperPush,
    UpperPull,
    Trunk,
    GoalSpecificAccessory
}

/// <summary>The role a slot plays in the session, which decides its volume and effort.</summary>
public enum StrengthPrescriptionRole
{
    Main,
    Accessory
}

/// <summary>A versioned slot definition. It contains no exercise identity or fabricated clinical mapping.</summary>
public sealed record StrengthTemplateSlot(
    string SlotId,
    StrengthTemplateSlotId MovementSlot,
    IReadOnlySet<MovementPattern> AcceptedPatterns,
    IReadOnlySet<MovementPattern> PreferredPatterns,
    bool IsRequired,
    StrengthPrescriptionRole GeneralRole,
    StrengthPrescriptionRole HighVolumeRole,
    StrengthPrescriptionRole HighIntensityRole,
    bool IsHeavyMainInHighIntensity);

/// <summary>One template: its split, its days and the slots each day fills.</summary>
public sealed record StrengthTemplateDefinition(
    string TemplateId,
    string Version,
    StrengthTemplateSplit Split,
    int SessionIndex,
    IReadOnlyList<StrengthTemplateSlot> Slots);

/// <summary>Explicit v0.2 templates; catalog exercise IDs are resolved by the infrastructure adapter.</summary>
public static class StrengthTemplateCatalog
{
    public const string Version = "MC-STRENGTH-TEMPLATES-0.2.0";

    private static readonly IReadOnlySet<MovementPattern> KneePatterns =
        Set(MovementPattern.SquatPattern, MovementPattern.Lunge);
    private static readonly IReadOnlySet<MovementPattern> HipPatterns =
        Set(MovementPattern.HipHinge, MovementPattern.HipThrust);
    private static readonly IReadOnlySet<MovementPattern> PushPatterns =
        Set(MovementPattern.HorizontalPush, MovementPattern.VerticalPush);
    private static readonly IReadOnlySet<MovementPattern> PullPatterns =
        Set(MovementPattern.HorizontalPull, MovementPattern.VerticalPull);
    private static readonly IReadOnlySet<MovementPattern> TrunkPatterns =
        Set(MovementPattern.AntiExtension, MovementPattern.AntiRotation, MovementPattern.AntiLateralFlexion);
    private static readonly IReadOnlySet<MovementPattern> AccessoryPatterns =
        Set(MovementPattern.HipAbduction, MovementPattern.HipAdduction, MovementPattern.KneeFlexion,
            MovementPattern.CalfRaise, MovementPattern.LateralRaise, MovementPattern.RearDelt,
            MovementPattern.ElbowFlexion, MovementPattern.ElbowExtension);

    /// <summary>Every strength template the catalog defines.</summary>
    public static IReadOnlyList<StrengthTemplateDefinition> Templates { get; } = BuildTemplates();

    /// <summary>The templates that fit a week with the given number of strength days.</summary>
    public static IReadOnlyList<StrengthTemplateDefinition> ForSchedule(int strengthDaysPerWeek) =>
        strengthDaysPerWeek switch
        {
            1 => Templates.Where(template => template.TemplateId == "MC-FB-1D-A").ToArray(),
            2 => Templates.Where(template => template.TemplateId.StartsWith("MC-FB-2D-",
                StringComparison.Ordinal)).ToArray(),
            3 => Templates.Where(template => template.TemplateId.StartsWith("MC-FB-3D-",
                StringComparison.Ordinal)).ToArray(),
            4 => Templates.Where(template => template.TemplateId.StartsWith("MC-UL-4D-",
                StringComparison.Ordinal)).ToArray(),
            _ => []
        };

    /// <summary>Alternatives must be explicitly cataloged for the same schedule, split and session position.</summary>
    public static IReadOnlyList<StrengthTemplateDefinition> AlternativesForSchedule(
        int strengthDaysPerWeek, StrengthTemplateDefinition current) =>
        ForSchedule(strengthDaysPerWeek).Where(candidate => candidate.TemplateId != current.TemplateId &&
            candidate.SessionIndex == current.SessionIndex && candidate.Split == current.Split).ToArray();

    /// <summary>Builds the fixed template set, so every plan is chosen from a known catalogue.</summary>
    private static ReadOnlyCollection<StrengthTemplateDefinition> BuildTemplates()
    {
        var templates = new List<StrengthTemplateDefinition>
        {
            FullBody("MC-FB-1D-A", 1, knee: MovementPattern.SquatPattern,
                hip: MovementPattern.HipHinge, push: MovementPattern.HorizontalPush,
                pull: MovementPattern.HorizontalPull),
            FullBody("MC-FB-2D-A", 1, knee: MovementPattern.SquatPattern,
                hip: MovementPattern.HipHinge, push: MovementPattern.HorizontalPush,
                pull: MovementPattern.HorizontalPull),
            FullBody("MC-FB-2D-B", 2, knee: MovementPattern.Lunge,
                hip: MovementPattern.HipThrust, push: MovementPattern.VerticalPush,
                pull: MovementPattern.VerticalPull),
            FullBody("MC-FB-3D-A", 1, knee: MovementPattern.SquatPattern,
                hip: MovementPattern.HipHinge, push: MovementPattern.HorizontalPush,
                pull: MovementPattern.HorizontalPull),
            FullBody("MC-FB-3D-B", 2, knee: MovementPattern.Lunge,
                hip: MovementPattern.HipThrust, push: MovementPattern.VerticalPush,
                pull: MovementPattern.VerticalPull),
            FullBody("MC-FB-3D-C", 3, knee: MovementPattern.SquatPattern,
                hip: MovementPattern.HipThrust, push: MovementPattern.HorizontalPush,
                pull: MovementPattern.VerticalPull),
            Upper("MC-UL-4D-UPPER-A", 1, push: MovementPattern.HorizontalPush,
                pull: MovementPattern.HorizontalPull),
            Lower("MC-UL-4D-LOWER-A", 2, knee: MovementPattern.SquatPattern,
                hip: MovementPattern.HipHinge),
            Upper("MC-UL-4D-UPPER-B", 3, push: MovementPattern.VerticalPush,
                pull: MovementPattern.VerticalPull),
            Lower("MC-UL-4D-LOWER-B", 4, knee: MovementPattern.Lunge,
                hip: MovementPattern.HipThrust)
        };
        return Array.AsReadOnly(templates.ToArray());
    }

    /// <summary>A full-body template for one session of the week.</summary>
    private static StrengthTemplateDefinition FullBody(string id, int sessionIndex,
        MovementPattern knee, MovementPattern hip, MovementPattern push, MovementPattern pull) =>
        Template(id, StrengthTemplateSplit.FullBody, sessionIndex,
        [
            Required("knee-dominant", StrengthTemplateSlotId.KneeDominant, KneePatterns, Set(knee), heavyMain: true),
            Required("hip-dominant", StrengthTemplateSlotId.HipDominant, HipPatterns, Set(hip), heavyMain: true),
            Required("upper-push", StrengthTemplateSlotId.UpperPush, PushPatterns, Set(push), highIntensityAccessory: true),
            Required("upper-pull", StrengthTemplateSlotId.UpperPull, PullPatterns, Set(pull), highIntensityAccessory: true),
            Optional("trunk", StrengthTemplateSlotId.Trunk, TrunkPatterns),
            Optional("goal-specific-accessory", StrengthTemplateSlotId.GoalSpecificAccessory,
                AccessoryPatterns)
        ]);

    /// <summary>An upper-body template for one session of the week.</summary>
    private static StrengthTemplateDefinition Upper(string id, int sessionIndex,
        MovementPattern push, MovementPattern pull) =>
        Template(id, StrengthTemplateSplit.UpperLower, sessionIndex,
        [
            Required("upper-push", StrengthTemplateSlotId.UpperPush, PushPatterns, Set(push), heavyMain: true),
            Required("upper-pull", StrengthTemplateSlotId.UpperPull, PullPatterns, Set(pull), highIntensityAccessory: true),
            Optional("trunk", StrengthTemplateSlotId.Trunk, TrunkPatterns),
            Optional("goal-specific-accessory", StrengthTemplateSlotId.GoalSpecificAccessory,
                AccessoryPatterns)
        ]);

    /// <summary>A lower-body template for one session of the week.</summary>
    private static StrengthTemplateDefinition Lower(string id, int sessionIndex,
        MovementPattern knee, MovementPattern hip) =>
        Template(id, StrengthTemplateSplit.UpperLower, sessionIndex,
        [
            Required("knee-dominant", StrengthTemplateSlotId.KneeDominant, KneePatterns, Set(knee), heavyMain: true),
            Required("hip-dominant", StrengthTemplateSlotId.HipDominant, HipPatterns, Set(hip), heavyMain: true),
            Optional("trunk", StrengthTemplateSlotId.Trunk, TrunkPatterns),
            Optional("goal-specific-accessory", StrengthTemplateSlotId.GoalSpecificAccessory,
                AccessoryPatterns)
        ]);

    /// <summary>A template built from its slots and requirements.</summary>
    private static StrengthTemplateDefinition Template(string id, StrengthTemplateSplit split,
        int sessionIndex, IReadOnlyList<StrengthTemplateSlot> slots) =>
        new(id, Version, split, sessionIndex, Array.AsReadOnly(slots.ToArray()));

    /// <summary>A slot the session must fill.</summary>
    private static StrengthTemplateSlot Required(string id, StrengthTemplateSlotId slot,
        IReadOnlySet<MovementPattern> patterns, IReadOnlySet<MovementPattern> preferredPatterns,
        bool heavyMain = false,
        bool highIntensityAccessory = false) =>
        new(id, slot, patterns, preferredPatterns, true, StrengthPrescriptionRole.Main,
            slot is StrengthTemplateSlotId.KneeDominant or StrengthTemplateSlotId.HipDominant
                ? StrengthPrescriptionRole.Main : StrengthPrescriptionRole.Accessory,
            highIntensityAccessory ? StrengthPrescriptionRole.Accessory : StrengthPrescriptionRole.Main,
            heavyMain);

    /// <summary>A slot the session fills only when time allows.</summary>
    private static StrengthTemplateSlot Optional(string id, StrengthTemplateSlotId slot,
        IReadOnlySet<MovementPattern> patterns) =>
        new(id, slot, patterns, patterns, false, StrengthPrescriptionRole.Accessory,
            StrengthPrescriptionRole.Accessory, StrengthPrescriptionRole.Accessory, false);

    /// <summary>A frozen set of the given movement patterns.</summary>
    private static FrozenSet<MovementPattern> Set(params MovementPattern[] patterns) =>
        patterns.ToFrozenSet();
}
