using System.Collections.Frozen;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum StrengthTemplateSplitV020
{
    FullBody,
    UpperLower
}

public enum StrengthTemplateSlotIdV020
{
    KneeDominant,
    HipDominant,
    UpperPush,
    UpperPull,
    Trunk,
    GoalSpecificAccessory
}

public enum StrengthPrescriptionRoleV020
{
    Main,
    Accessory
}

/// <summary>A versioned slot definition. It contains no exercise identity or fabricated clinical mapping.</summary>
public sealed record StrengthTemplateSlotV020(
    string SlotId,
    StrengthTemplateSlotIdV020 MovementSlot,
    IReadOnlySet<MovementPattern> AcceptedPatterns,
    IReadOnlySet<MovementPattern> PreferredPatterns,
    bool IsRequired,
    StrengthPrescriptionRoleV020 GeneralRole,
    StrengthPrescriptionRoleV020 HighVolumeRole,
    StrengthPrescriptionRoleV020 HighIntensityRole,
    bool IsHeavyMainInHighIntensity);

public sealed record StrengthTemplateDefinitionV020(
    string TemplateId,
    string Version,
    StrengthTemplateSplitV020 Split,
    int SessionIndex,
    IReadOnlyList<StrengthTemplateSlotV020> Slots);

/// <summary>Explicit v0.2 templates; catalog exercise IDs are resolved by the infrastructure adapter.</summary>
public static class StrengthTemplateCatalogV020
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

    public static IReadOnlyList<StrengthTemplateDefinitionV020> Templates { get; } = BuildTemplates();

    public static IReadOnlyList<StrengthTemplateDefinitionV020> ForSchedule(int strengthDaysPerWeek) =>
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

    private static IReadOnlyList<StrengthTemplateDefinitionV020> BuildTemplates()
    {
        var templates = new List<StrengthTemplateDefinitionV020>
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

    private static StrengthTemplateDefinitionV020 FullBody(string id, int sessionIndex,
        MovementPattern knee, MovementPattern hip, MovementPattern push, MovementPattern pull) =>
        Template(id, StrengthTemplateSplitV020.FullBody, sessionIndex,
        [
            Required("knee-dominant", StrengthTemplateSlotIdV020.KneeDominant, KneePatterns, Set(knee), heavyMain: true),
            Required("hip-dominant", StrengthTemplateSlotIdV020.HipDominant, HipPatterns, Set(hip), heavyMain: true),
            Required("upper-push", StrengthTemplateSlotIdV020.UpperPush, PushPatterns, Set(push), highIntensityAccessory: true),
            Required("upper-pull", StrengthTemplateSlotIdV020.UpperPull, PullPatterns, Set(pull), highIntensityAccessory: true),
            Optional("trunk", StrengthTemplateSlotIdV020.Trunk, TrunkPatterns),
            Optional("goal-specific-accessory", StrengthTemplateSlotIdV020.GoalSpecificAccessory,
                AccessoryPatterns)
        ]);

    private static StrengthTemplateDefinitionV020 Upper(string id, int sessionIndex,
        MovementPattern push, MovementPattern pull) =>
        Template(id, StrengthTemplateSplitV020.UpperLower, sessionIndex,
        [
            Required("upper-push", StrengthTemplateSlotIdV020.UpperPush, PushPatterns, Set(push), heavyMain: true),
            Required("upper-pull", StrengthTemplateSlotIdV020.UpperPull, PullPatterns, Set(pull), highIntensityAccessory: true),
            Optional("trunk", StrengthTemplateSlotIdV020.Trunk, TrunkPatterns),
            Optional("goal-specific-accessory", StrengthTemplateSlotIdV020.GoalSpecificAccessory,
                AccessoryPatterns)
        ]);

    private static StrengthTemplateDefinitionV020 Lower(string id, int sessionIndex,
        MovementPattern knee, MovementPattern hip) =>
        Template(id, StrengthTemplateSplitV020.UpperLower, sessionIndex,
        [
            Required("knee-dominant", StrengthTemplateSlotIdV020.KneeDominant, KneePatterns, Set(knee), heavyMain: true),
            Required("hip-dominant", StrengthTemplateSlotIdV020.HipDominant, HipPatterns, Set(hip), heavyMain: true),
            Optional("trunk", StrengthTemplateSlotIdV020.Trunk, TrunkPatterns),
            Optional("goal-specific-accessory", StrengthTemplateSlotIdV020.GoalSpecificAccessory,
                AccessoryPatterns)
        ]);

    private static StrengthTemplateDefinitionV020 Template(string id, StrengthTemplateSplitV020 split,
        int sessionIndex, IReadOnlyList<StrengthTemplateSlotV020> slots) =>
        new(id, Version, split, sessionIndex, Array.AsReadOnly(slots.ToArray()));

    private static StrengthTemplateSlotV020 Required(string id, StrengthTemplateSlotIdV020 slot,
        IReadOnlySet<MovementPattern> patterns, IReadOnlySet<MovementPattern> preferredPatterns,
        bool heavyMain = false,
        bool highIntensityAccessory = false) =>
        new(id, slot, patterns, preferredPatterns, true, StrengthPrescriptionRoleV020.Main,
            StrengthPrescriptionRoleV020.Main,
            highIntensityAccessory ? StrengthPrescriptionRoleV020.Accessory : StrengthPrescriptionRoleV020.Main,
            heavyMain);

    private static StrengthTemplateSlotV020 Optional(string id, StrengthTemplateSlotIdV020 slot,
        IReadOnlySet<MovementPattern> patterns) =>
        new(id, slot, patterns, patterns, false, StrengthPrescriptionRoleV020.Accessory,
            StrengthPrescriptionRoleV020.Accessory, StrengthPrescriptionRoleV020.Accessory, false);

    private static IReadOnlySet<MovementPattern> Set(params MovementPattern[] patterns) =>
        patterns.ToFrozenSet();
}
