using MuscleCuties.Core.Domain.Hydration;

namespace MuscleCuties.Core.Tests;

public sealed class HydrationNextDomainTests
{
    private const int FixtureYear = 2026;
    private const int SpringMonth = 3;
    private const int SpringDay = 8;
    private const int FallMonth = 11;
    private const int FallDay = 1;
    private const int GapHour = 2;
    private const int FoldHour = 1;
    private const int HalfHourMinutes = 30;
    private const int QuietStartHour = 20;
    private const int QuietEndHour = 1;
    private const int MorningHour = 9;
    private const int StandardOffsetHours = -5;
    private const int DaylightOffsetHours = -4;
    private const int OtherOffsetHours = 3;
    private const int DaylightDeltaHours = 1;
    private const int LegacyMillilitres = 1000;
    private const string SyntheticZoneId = "SyntheticHydrationEastern";
    private const string OtherZoneId = "SyntheticHydrationOther";
    private static readonly Guid Account = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Entry = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateOnly SpringDate = new(FixtureYear, SpringMonth, SpringDay);
    private static readonly DateOnly FallDate = new(FixtureYear, FallMonth, FallDay);
    private static readonly TimeOnly QuietStart = new(QuietStartHour, 0);
    private static readonly TimeOnly QuietEnd = new(QuietEndHour, 0);

    [Fact]
    public void ApprovedDefaultsAreEditableWithoutChangingTheirStableIdentity()
    {
        var catalog = DefaultWaterPresets.Create(Account);
        Assert.Equal(new[] { DefaultWaterPresets.SmallMillilitres, DefaultWaterPresets.MediumMillilitres,
            DefaultWaterPresets.LargeMillilitres }, catalog.Presets.Select(p => p.Portion.Millilitres));
        var first = catalog.Presets[0];
        var edited = catalog.Update(first.Id, "My container", DefaultWaterPresets.LargeMillilitres);
        Assert.Equal(first.Id, edited.Presets[0].Id);
        Assert.Equal(DefaultWaterPresets.LargeMillilitres, edited.Presets[0].Portion.Millilitres);
        Assert.Empty(catalog.Presets.Aggregate(catalog, (current, preset) => current.Delete(preset.Id)).Presets);
    }

    [Fact]
    public void IntakeCorrectUndoAndRetriesPreserveLegacyBaseline()
    {
        var original = Empty(LegacyMillilitres);
        var add = new WaterIntakeChange(Guid.NewGuid(), Entry, WaterIntakeChangeKind.Add, DefaultWaterPresets.SmallMillilitres);
        var added = original.Apply(add);
        var corrected = added.Apply(new(Guid.NewGuid(), Entry, WaterIntakeChangeKind.Correct, DefaultWaterPresets.LargeMillilitres));
        Assert.Equal(LegacyMillilitres + DefaultWaterPresets.LargeMillilitres, corrected.TotalMillilitres);
        Assert.Same(corrected, corrected.Apply(add));
        Assert.Equal(LegacyMillilitres, original.TotalMillilitres);
        var undo = new WaterIntakeChange(Guid.NewGuid(), Entry, WaterIntakeChangeKind.Undo, 0);
        var undone = corrected.Apply(undo);
        Assert.Equal(LegacyMillilitres, undone.TotalMillilitres);
        Assert.Same(undone, undone.Apply(undo));
        Assert.True(Assert.Single(undone.Entries).Undone);
        Assert.Throws<InvalidOperationException>(() => undone.Apply(new(Guid.NewGuid(), Entry, WaterIntakeChangeKind.Correct, DefaultWaterPresets.SmallMillilitres)));
        Assert.Throws<InvalidOperationException>(() => undone.Apply(new(Guid.NewGuid(), Entry, WaterIntakeChangeKind.Undo, 0)));
        Assert.Throws<InvalidOperationException>(() => added.Apply(add with { Millilitres = DefaultWaterPresets.MediumMillilitres }));
        Assert.Throws<InvalidOperationException>(() => added.Apply(add with { OperationId = Guid.NewGuid() }));
        var reopened = WaterIntakeDay.Restore(Account, SpringDate, undone.LegacyMillilitres, undone.Entries, undone.Operations);
        Assert.Same(reopened, reopened.Apply(undo));
        Assert.Equal(undone.TotalMillilitres, reopened.TotalMillilitres);
    }

    [Fact]
    public void IntakeRejectsOverflowMissingEntriesInvalidInputsAndDuplicateIdentities()
    {
        Assert.Throws<OverflowException>(() => Empty(int.MaxValue).Apply(new(Guid.NewGuid(), Entry,
            WaterIntakeChangeKind.Add, DefaultWaterPresets.SmallMillilitres)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Empty().Apply(new(Guid.NewGuid(), Entry, WaterIntakeChangeKind.Add, 0)));
        Assert.Throws<ArgumentException>(() => Empty().Apply(new(Guid.Empty, Entry, WaterIntakeChangeKind.Add, DefaultWaterPresets.SmallMillilitres)));
        Assert.Throws<ArgumentException>(() => Empty().Apply(new(Guid.NewGuid(), Entry, WaterIntakeChangeKind.Undo, DefaultWaterPresets.SmallMillilitres)));
        Assert.Throws<InvalidOperationException>(() => Empty().Apply(new(Guid.NewGuid(), Entry, WaterIntakeChangeKind.Correct, DefaultWaterPresets.SmallMillilitres)));
        var duplicate = new WaterIntakeEntry(Entry, DefaultWaterPresets.SmallMillilitres, false);
        Assert.Throws<ArgumentException>(() => WaterIntakeDay.Restore(Account, SpringDate, 0, [duplicate, duplicate], []));
        var excessive = Enumerable.Range(0, WaterIntakeDay.MaximumEntries + 1)
            .Select(_ => new WaterIntakeEntry(Guid.NewGuid(), DefaultWaterPresets.SmallMillilitres, false));
        Assert.Throws<ArgumentException>(() => WaterIntakeDay.Restore(Account, SpringDate, 0, excessive, []));
        var excessiveOperations = Enumerable.Range(0, WaterIntakeDay.MaximumOperations + 1)
            .Select(_ => new WaterIntakeChange(Guid.NewGuid(), Entry, WaterIntakeChangeKind.Undo, 0));
        Assert.Throws<ArgumentException>(() => WaterIntakeDay.Restore(Account, SpringDate, 0, [], excessiveOperations));
    }

    [Fact]
    public void DstGapIsSkippedInsteadOfMovedIntoAnUnselectedTime()
    {
        var configuration = Configuration(new(GapHour, HalfHourMinutes));
        var before = AtUtcMidnight(SpringDate);
        Assert.Empty(WaterReminderSchedule.ForDate(configuration, SpringDate, Zone(), before, WaterReminderAuthorizationState.Granted));
    }

    [Fact]
    public void DstFoldUsesEarlierInstantOnceAndDoesNotFallBackToSecondOccurrence()
    {
        var configuration = Configuration(new(FoldHour, HalfHourMinutes));
        var first = Assert.Single(WaterReminderSchedule.ForDate(configuration, FallDate, Zone(),
            AtUtcMidnight(FallDate), WaterReminderAuthorizationState.Granted));
        var expected = new DateTimeOffset(FallDate.ToDateTime(new(FoldHour, HalfHourMinutes)), TimeSpan.FromHours(DaylightOffsetHours)).ToUniversalTime();
        Assert.Equal(expected, first.Instant);
        Assert.Empty(WaterReminderSchedule.ForDate(configuration, FallDate, Zone(), expected, WaterReminderAuthorizationState.Granted));
    }

    [Fact]
    public void RepeatedPlanningIsDeterministicAndTimezoneChangePreservesWallTime()
    {
        var configuration = Configuration(new(MorningHour, 0));
        var now = AtUtcMidnight(SpringDate);
        var original = WaterReminderSchedule.ForDate(configuration, SpringDate, Zone(), now, WaterReminderAuthorizationState.Granted);
        Assert.Equal(original, WaterReminderSchedule.ForDate(configuration, SpringDate, Zone(), now, WaterReminderAuthorizationState.Granted));
        var changedZone = TimeZoneInfo.CreateCustomTimeZone(OtherZoneId, TimeSpan.FromHours(OtherOffsetHours), OtherZoneId, OtherZoneId);
        var changed = Assert.Single(WaterReminderSchedule.ForDate(configuration, SpringDate, changedZone, now, WaterReminderAuthorizationState.Granted));
        Assert.Equal(original[0].LocalTime, changed.LocalTime);
        Assert.Equal(Account, changed.AccountId);
        Assert.NotEqual(original[0].Instant, changed.Instant);
        Assert.Empty(WaterReminderSchedule.ForDate(configuration, SpringDate, Zone(), now, WaterReminderAuthorizationState.Denied));
        Assert.Empty(WaterReminderSchedule.ForDate(configuration, SpringDate, Zone(), now, WaterReminderAuthorizationState.Unknown));
        var disabled = WaterReminderConfiguration.Create(Account, false, null, null, null);
        Assert.Empty(WaterReminderSchedule.ForDate(disabled, SpringDate, Zone(), now, WaterReminderAuthorizationState.Granted));
    }

    [Fact]
    public void CalendarBoundaryOutsideUtcRangeProducesNoCandidate()
    {
        var configuration = Configuration(new(FoldHour, HalfHourMinutes));
        var zone = TimeZoneInfo.CreateCustomTimeZone(OtherZoneId, TimeSpan.FromHours(OtherOffsetHours), OtherZoneId, OtherZoneId);
        Assert.Empty(WaterReminderSchedule.ForDate(configuration, DateOnly.MinValue, zone,
            DateTimeOffset.MinValue, WaterReminderAuthorizationState.Granted));
    }

    private static WaterIntakeDay Empty(int legacy = 0) => WaterIntakeDay.Restore(Account, SpringDate, legacy, [], []);
    private static WaterReminderConfiguration Configuration(TimeOnly time) => WaterReminderConfiguration.Create(Account, true, [time], QuietStart, QuietEnd);
    private static DateTimeOffset AtUtcMidnight(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static TimeZoneInfo Zone()
    {
        // Synthetic fixed-date rule avoids dependence on the host timezone or installed tzdata version.
        var transitionTime = DateTime.MinValue.Date.AddHours(GapHour);
        var start = TimeZoneInfo.TransitionTime.CreateFixedDateRule(transitionTime, SpringMonth, SpringDay);
        var end = TimeZoneInfo.TransitionTime.CreateFixedDateRule(transitionTime, FallMonth, FallDay);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new(FixtureYear, 1, 1),
            new(FixtureYear, FallMonth, FallDay), TimeSpan.FromHours(DaylightDeltaHours), start, end);
        return TimeZoneInfo.CreateCustomTimeZone(SyntheticZoneId, TimeSpan.FromHours(StandardOffsetHours),
            SyntheticZoneId, SyntheticZoneId, SyntheticZoneId, [rule]);
    }
}
