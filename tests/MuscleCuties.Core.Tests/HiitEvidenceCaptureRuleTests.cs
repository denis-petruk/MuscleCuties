using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class HiitEvidenceCaptureRuleTests
{
    private const int InitialVersion = 1;
    private const int ChangedVersion = 2;
    private const int LastValidDayOffset = HiitEvidenceCaptureRule.EvidenceValidityCalendarDays - 1;
    private const int FutureDayOffset = -1;
    private static readonly DateTimeOffset Issued = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly LocalDate = new(2026, 10, 27);

    [Fact]
    public void EvidenceIsCurrentForThirtyCalendarDaysInTheSameAccountCalendar()
    {
        Assert.True(HiitEvidenceCaptureRule.IsCaptureCurrent(LocalDate, InitialVersion, LocalDate, InitialVersion));
        Assert.True(HiitEvidenceCaptureRule.IsCaptureCurrent(LocalDate, InitialVersion, LocalDate.AddDays(LastValidDayOffset), InitialVersion));
        Assert.False(HiitEvidenceCaptureRule.IsCaptureCurrent(LocalDate, InitialVersion, LocalDate.AddDays(HiitEvidenceCaptureRule.EvidenceValidityCalendarDays), InitialVersion));
        Assert.False(HiitEvidenceCaptureRule.IsCaptureCurrent(LocalDate, InitialVersion, LocalDate.AddDays(FutureDayOffset), InitialVersion));
        Assert.False(HiitEvidenceCaptureRule.IsCaptureCurrent(LocalDate, InitialVersion, null, InitialVersion));
        Assert.False(HiitEvidenceCaptureRule.IsCaptureCurrent(LocalDate, InitialVersion, LocalDate, ChangedVersion));
    }

    [Fact]
    public void OnlyFreshUnusedChallengeInSameAccountCalendarCanBeConsumed()
    {
        var expiry = Issued.Add(HiitEvidenceCaptureRule.ChallengeLifetime);
        Assert.True(HiitEvidenceCaptureRule.CanConsume(Issued, expiry, Issued, LocalDate, LocalDate, InitialVersion, InitialVersion, false));
        Assert.False(HiitEvidenceCaptureRule.CanConsume(Issued, expiry, expiry, LocalDate, LocalDate, InitialVersion, InitialVersion, false));
        Assert.False(HiitEvidenceCaptureRule.CanConsume(Issued, expiry, Issued, LocalDate, LocalDate, InitialVersion, InitialVersion, true));
        Assert.False(HiitEvidenceCaptureRule.CanConsume(Issued, expiry, Issued, LocalDate, LocalDate.AddDays(1), InitialVersion, InitialVersion, false));
        Assert.False(HiitEvidenceCaptureRule.CanConsume(Issued, expiry, Issued, LocalDate, LocalDate, InitialVersion, ChangedVersion, false));
        Assert.False(HiitEvidenceCaptureRule.CanConsume(Issued, expiry.Add(HiitEvidenceCaptureRule.ChallengeLifetime), Issued, LocalDate, LocalDate, InitialVersion, InitialVersion, false));
    }
}
