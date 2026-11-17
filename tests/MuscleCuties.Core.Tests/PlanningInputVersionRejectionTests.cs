using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// P2 versioned input validation: a request built against an input contract this build does not
/// implement must be refused explicitly, never answered from defaults.
/// </summary>
public sealed class PlanningInputVersionRejectionTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void An_unsupported_input_contract_version_is_rejected_instead_of_using_defaults()
    {
        var request = new StrengthPlanPreviewRequestV020(
            WorkoutActivityDto.GeneralStrength, new DateOnly(2026, 11, 15), null)
        {
            InputContractVersion = "MC-PLANNING-INPUT-999"
        };

        var response = PreviewStrengthPlanV020UseCase.EvaluateLoadedSource(UserId, request, sourceData: null);

        Assert.Equal(nameof(StrengthPlanPreviewStatus.InvalidInput), response.Status);
        Assert.Contains(StrengthPlanPreviewRule.UnsupportedInputVersionCode, response.LimitationCodes);
        Assert.Empty(response.Sessions);
    }

    [Fact]
    public void The_current_input_contract_version_is_accepted()
    {
        var request = new StrengthPlanPreviewRequestV020(
            WorkoutActivityDto.GeneralStrength, new DateOnly(2026, 11, 15), null)
        {
            InputContractVersion = StrengthPlanPreviewRule.InputContractVersion
        };

        var response = PreviewStrengthPlanV020UseCase.EvaluateLoadedSource(UserId, request, sourceData: null);

        Assert.DoesNotContain(StrengthPlanPreviewRule.UnsupportedInputVersionCode, response.LimitationCodes);
    }
}
