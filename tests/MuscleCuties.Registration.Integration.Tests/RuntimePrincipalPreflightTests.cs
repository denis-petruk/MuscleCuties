using MuscleCuties.DbMaintenance.Schema;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class RuntimePrincipalPreflightTests
{
    private static readonly RuntimePrincipalObservation RestrictedCandidate = new(
        SessionUser: "musclecuties_api_login",
        EffectiveUser: "musclecuties_app",
        CanLogin: true,
        IsSuperuser: false,
        BypassesRls: false,
        CanCreateDatabase: false,
        CanCreateRole: false,
        MemberOfApp: true,
        MemberOfMigrator: false,
        MemberOfWorkoutDispatch: false,
        MemberOfWorkoutDiscoveryOwner: false,
        OwnedObjectCount: 0,
        CanCreateInAppSchema: false,
        CanReadUsers: true,
        CanUseEmailChallenges: true,
        CanUseEmailChallengeSessions: true,
        CanReadMigrationHistory: true);

    [Fact]
    public void RestrictedCandidatePassesCatalogChecksOnly()
    {
        var result = RuntimePrincipalPreflight.Assess(RestrictedCandidate);

        Assert.True(result.CatalogCompatible);
        Assert.Equal("catalog-compatible", result.Status);
        Assert.Empty(result.FailureCodes);
    }

    [Fact]
    public void MasterLoginCannotMasqueradeAsAppRole()
    {
        var result = RuntimePrincipalPreflight.Assess(RestrictedCandidate with
        {
            SessionUser = "mc_admin",
            MemberOfMigrator = true
        });

        Assert.False(result.CatalogCompatible);
        Assert.Contains("CANDIDATE_LOGIN_NOT_DEDICATED", result.FailureCodes);
        Assert.Contains("CANDIDATE_ROLE_MEMBERSHIP_INVALID", result.FailureCodes);
    }

    [Fact]
    public void CandidateCannotPassWithoutAuthenticationAndReadinessAccess()
    {
        var result = RuntimePrincipalPreflight.Assess(RestrictedCandidate with
        {
            CanUseEmailChallenges = false,
            CanUseEmailChallengeSessions = false,
            CanReadMigrationHistory = false
        });

        Assert.False(result.CatalogCompatible);
        Assert.Contains("AUTH_TABLE_ACCESS_MISSING", result.FailureCodes);
        Assert.Contains("READINESS_LEDGER_ACCESS_MISSING", result.FailureCodes);
    }

    [Fact]
    public void CandidateCannotOwnSchemaObjectsOrHaveDdlAccess()
    {
        var result = RuntimePrincipalPreflight.Assess(RestrictedCandidate with
        {
            OwnedObjectCount = 1,
            CanCreateInAppSchema = true,
            BypassesRls = true
        });

        Assert.False(result.CatalogCompatible);
        Assert.Contains("CANDIDATE_HAS_DDL_ACCESS", result.FailureCodes);
        Assert.Contains("CANDIDATE_LOGIN_PRIVILEGED", result.FailureCodes);
    }
}
