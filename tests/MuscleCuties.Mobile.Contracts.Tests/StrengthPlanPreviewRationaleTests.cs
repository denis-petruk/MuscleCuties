using MuscleCuties.UI.ViewModels.Workout;
using MuscleCuties.UseCases.Features.Workout.Planning;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

/// <summary>
/// Rendered-surface proof for the strength-plan preview rationale row. The XAML binds the row's
/// visibility to <see cref="StrengthPlanSlotDisplay.HasRationale"/>, so these tests exercise the
/// real display model and session projection used by the page, not the transport payload.
/// </summary>
public sealed class StrengthPlanPreviewRationaleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n ")]
    public void Slot_WithBlankRationale_HasNoVisibleRationaleAndNoRenderedText(string? rationale)
    {
        var slot = new StrengthPlanSlotDisplay("Back squat", "4 × 6–8", true) { Rationale = rationale! };

        Assert.False(slot.HasRationale);
        Assert.Equal(string.Empty, slot.Rationale);
    }

    [Fact]
    public void Slot_WithRealRationale_KeepsExactText()
    {
        const string rationale = "Chosen to balance weekly push volume against your three-day plan.";

        var slot = new StrengthPlanSlotDisplay("Back squat", "4 × 6–8", true) { Rationale = rationale };

        Assert.True(slot.HasRationale);
        Assert.Equal(rationale, slot.Rationale);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n ")]
    public void Session_WithBlankSelectionReason_ShowsExerciseWithoutRationaleRow(string? selectionReason)
    {
        var display = StrengthPlanSessionDisplay.From(Session(selectionReason));

        var slot = Assert.Single(display.Slots);
        Assert.Equal("Back squat", slot.Name);
        Assert.False(string.IsNullOrWhiteSpace(slot.Details));
        Assert.False(slot.HasRationale);
        Assert.Equal(string.Empty, slot.Rationale);
    }

    [Fact]
    public void Session_WithRealSelectionReason_PreservesExactRationaleText()
    {
        const string selectionReason = "Chosen to balance weekly push volume against your three-day plan.";

        var display = StrengthPlanSessionDisplay.From(Session(selectionReason));

        var slot = Assert.Single(display.Slots);
        Assert.True(slot.HasRationale);
        Assert.Equal(selectionReason, slot.Rationale);
    }

    [Theory]
    [InlineData("Workout_Generation_EmptyCopy")]
    [InlineData("Workout_Generation_WhitespaceCopy")]
    public void Session_WithCatalogReasonWhoseCopyIsBlank_ShowsNoRationaleRow(string catalogKey)
    {
        var display = StrengthPlanSessionDisplay.From(Session(catalogKey));

        var slot = Assert.Single(display.Slots);
        Assert.False(slot.HasRationale);
        Assert.Equal(string.Empty, slot.Rationale);
    }

    [Fact]
    public void Session_WithCatalogReasonThatHasCopy_RendersTheLocalizedCopy()
    {
        var display = StrengthPlanSessionDisplay.From(Session("Workout_Generation_ReasonBalancedWeeklyVolume"));

        var slot = Assert.Single(display.Slots);
        Assert.True(slot.HasRationale);
        Assert.Equal("Workout_Generation_ReasonBalancedWeeklyVolume", slot.Rationale);
    }

    private static StrengthPlanPreviewSessionV020 Session(string? selectionReason) =>
        new("template-strength-three-day", "v1", 0, 45m,
            new[] { Slot(selectionReason) },
            Array.Empty<string>(),
            new Dictionary<string, string>(),
            DayOfWeek.Monday);

    private static StrengthPlanSlotOutputV020 Slot(string? selectionReason) =>
        new("slot-knee-dominant", (int)StrengthMovementSlotDto.KneeDominant, true, 1, true,
            ExerciseId: 42, ExerciseName: "Back squat", selectionReason,
            AppliedPrescriptionRule: null, Sets: 4, MinimumRepetitions: 6, MaximumRepetitions: 8,
            RepetitionsInReserve: 2, RestSeconds: 180, LimitationCodes: Array.Empty<string>());
}
