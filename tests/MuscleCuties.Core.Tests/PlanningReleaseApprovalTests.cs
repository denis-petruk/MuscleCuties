using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// D2/OD-8: the approval that gates automatic dose changes is the owner's recorded Beta workout
/// policy, never a test fixture reference.
/// </summary>
public sealed class PlanningReleaseApprovalTests
{
    private const string FixtureApprovalReference = "MC-PLANNING-REVIEW-TEST";
    private const string ApprovedPolicyReference = "MC-BETA-WORKOUT-1";
    private static readonly DateTimeOffset OwnerDecisionDate = new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
    private static readonly string RepoRoot = FindRepositoryRoot();

    [Fact]
    public void Production_approval_cites_the_owner_approved_beta_policy()
    {
        var approval = Assert.IsType<PlanningReviewApproval>(PlanningReleaseApproval.CycleReadinessPrescription);

        Assert.Equal(ApprovedPolicyReference, approval.ApprovalReference);
        Assert.Equal(OwnerDecisionDate, approval.ApprovedAtUtc);
        Assert.True(approval.CoveredSections.SetEquals(
        [
            PlanningReviewSection.TrainingPrescriptions,
            PlanningReviewSection.AutomatedProgression
        ]));
    }

    [Fact]
    public void Production_approval_never_equals_a_fixture_reference()
    {
        var approval = Assert.IsType<PlanningReviewApproval>(PlanningReleaseApproval.CycleReadinessPrescription);

        Assert.NotEqual(FixtureApprovalReference, approval.ApprovalReference);
        Assert.DoesNotContain("TEST", approval.ApprovalReference, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FIXTURE", approval.ApprovalReference, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SYNTHETIC", approval.ApprovalReference, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Approved_reference_is_the_policy_version_recorded_in_the_owner_decision()
    {
        var policy = File.ReadAllText(Path.Combine(
            RepoRoot, "docs", "release-audit", "2026-11-05", "WORKOUT_POLICY.md"));

        Assert.Contains($"**Policy version** `{PlanningReleaseApproval.BetaWorkoutPolicyReference}`", policy,
            StringComparison.Ordinal);
        Assert.Contains("November 5, 2026", policy, StringComparison.Ordinal);
    }

    [Fact]
    public void Fixture_approval_reference_is_confined_to_tests()
    {
        var offenders = new[] { "src", "services" }
            .Select(folder => Path.Combine(RepoRoot, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path) && IsSourceFile(path))
            .Where(path => File.ReadAllText(path).Contains(FixtureApprovalReference, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(RepoRoot, path))
            .ToArray();

        Assert.True(offenders.Length == 0,
            "The fixture approval reference must stay in tests:\n" + string.Join("\n", offenders));
    }

    private static bool IsSourceFile(string path) =>
        Path.GetExtension(path) is ".cs" or ".json" or ".sql" or ".xaml";

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string FindRepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "MuscleCuties.sln")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException($"Could not locate repository root starting from '{AppContext.BaseDirectory}'.");
    }
}
