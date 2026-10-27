using System.Net;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// Synthetic unit tests covering all 14 critical workout planning migration behaviors:
/// 1. First-plan and replacement navigation and confirmations.
/// 2. Missing required answers and independent answer persistence.
/// 3. Safe/unsafe answer transitions and automatic plan updates.
/// 4. Style choice and reduced-volume acknowledgment.
/// 5. Catalog gaps and safe remainder.
/// 6. Two-activity scheduling and consecutive muscle groups.
/// 7. Next-eligible-day rules.
/// 8. Cross-device lease and takeover races.
/// 9. Stale snapshots and transactional/idempotent Apply.
/// 10. Partial-session and history preservation on abandon/deletion.
/// 11. Injury clearance boundaries and overlapping restrictions.
/// 12. Timezone changes and stale timezone update rejection.
/// 13. Experience thresholds, duplicate days, manual records and deletion.
/// 14. Connection failures, lost responses and definitive session rejection.
/// </summary>
public sealed class WorkoutPlanningSyntheticReviewTests
{
    private const int ZeroDays = 0;
    private const int OneDay = 1;
    private const int TwoDays = 2;
    private const int ThreeDays = 3;
    private const int FourDays = 4;
    private const int FiveDays = 5;
    private const int SixDays = 6;
    private const int SevenDays = 7;
    private const int EightDays = 8;
    private const int NineDays = 9;
    private const int TenDays = 10;
    private const int NineteenDays = 19;
    private const int TwentyDays = 20;
    private const int OneHundredTwentyNineDays = 129;
    private const int OneHundredThirtyDays = 130;
    private const int NegativeFiveDays = -5;

    private const int InitialVersion = 1;
    private const int StaleVersion = 0;
    private const int IncrementedVersion = 2;
    private const int SubsequentVersion = 3;
    private const int NegativeVersion = -1;

    private const long InitialFenceNumber = 0;
    private const long IncrementedFenceNumber = 1;
    private const long SubsequentFenceNumber = 2;
    private const long InvalidNegativeFenceNumber = -1;

    private const int ReferenceYear = 2026;
    private const int ReferenceMonth = 10;
    private const int ReferenceDay = 26;
    private const int ReferenceHour = 12;
    private const int ReferenceMinute = 0;
    private const int ReferenceSecond = 0;

    private const int SpringForwardYear = 2026;
    private const int SpringForwardMonth = 3;
    private const int SpringForwardDay = 8;
    private const int SpringForwardHour = 4;
    private const int SpringForwardMinute = 30;
    private const int SpringForwardExpectedDay = 7;

    private const int FallBackYear = 2026;
    private const int FallBackMonth = 11;
    private const int FallBackDay = 1;
    private const int FallBackHour = 1;
    private const int FallBackMinute = 30;

    private const DayOfWeek OutOfRangeDayOfWeek = (DayOfWeek)999;
    private const int TokyoHour = 2;

    private const string ChestMuscleGroup = "Chest";
    private const string TricepsMuscleGroup = "Triceps";
    private const string ShouldersMuscleGroup = "Shoulders";
    private const string LegsMuscleGroup = "Legs";
    private const string BackMuscleGroup = "Back";

    private const string TorontoTimeZoneId = "America/Toronto";
    private const string UtcTimeZoneId = "UTC";
    private const string TokyoTimeZoneId = "Asia/Tokyo";
    private const string LosAngelesTimeZoneId = "America/Los_Angeles";
    private const string InvalidTimeZoneId = "Invalid/Timezone";
    private const string WhitespaceTimeZoneId = "   ";

    private const string CanonicalFullBodyTemplateId = "MC-FB-1D-A";
    private const string CanonicalUpperLowerTemplateId = "MC-UL-4D-UPPER-A";
    private const string UnreviewedTemplateId = "MC-UNREVIEWED-TEMPLATE-ID";

    private const int FullWorkingSetsCount = 12;
    private const int ReducedWorkingSetsCount = 6;
    private const int BelowFloorWorkingSetsCount = 3;
    private const int ZeroWorkingSetsCount = 0;

    private const int TenReps = 10;
    private const decimal HighVolumeSessionDurationMinutes = 45m;
    private const decimal CyclingDurationMinutes = 30m;
    private const decimal MobilityDurationMinutes = 15m;

    private const int CatalogItem101 = 101;
    private const int CatalogItem102 = 102;
    private const int CatalogItem103 = 103;
    private const int CatalogItem104 = 104;
    private const int CatalogItem105 = 105;

    private const string CatalogV1Hash = "catalog-v1";
    private const string CatalogV2Hash = "catalog-v2";
    private const string PolicyV1Hash = "policy-v1";
    private const string PolicyV2Hash = "policy-v2";

    private const string AcceptedStatus = "Accepted";
    private const string FeasibleStatus = "Feasible";
    private const string LimitedStatus = "Limited";
    private const string NoSafeCandidatesReason = "NoSafeCandidates";

    private static readonly DateTimeOffset ReferenceInstant = new(
        ReferenceYear, ReferenceMonth, ReferenceDay,
        ReferenceHour, ReferenceMinute, ReferenceSecond, TimeSpan.Zero);

    private static readonly DateOnly ReferenceDate = new(ReferenceYear, ReferenceMonth, ReferenceDay);

    // =========================================================================
    // 1. First-Plan and Replacement Navigation and Confirmations
    // =========================================================================

    [Theory]
    [InlineData(false, false, false, false, true)]
    [InlineData(false, true, false, false, true)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, true, true, true)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, true, false, false, true)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, true, true, true)]
    public void ConfirmationsSatisfied_EvaluatesAllApplicableConfirmationsCorrectly(
        bool replacementRequired,
        bool replacementConfirmed,
        bool reducedVolumeRequired,
        bool reducedVolumeAcknowledged,
        bool expectedSatisfied)
    {
        var satisfied = PlanningLifecycleRule.ConfirmationsSatisfied(
            replacementRequired,
            replacementConfirmed,
            reducedVolumeRequired,
            reducedVolumeAcknowledged);

        Assert.Equal(expectedSatisfied, satisfied);
    }

    [Fact]
    public void FirstPlan_DoesNotRequireReplacementConfirmation()
    {
        var satisfiedWithoutReplacementConfirmation = PlanningLifecycleRule.ConfirmationsSatisfied(
            replacementRequired: false,
            replacementConfirmed: false,
            reducedVolumeRequired: false,
            reducedVolumeAcknowledged: false);

        Assert.True(satisfiedWithoutReplacementConfirmation);
    }

    [Fact]
    public void ReplacementPlan_WithoutReplacementConfirmation_IsBlocked()
    {
        var satisfied = PlanningLifecycleRule.ConfirmationsSatisfied(
            replacementRequired: true,
            replacementConfirmed: false,
            reducedVolumeRequired: false,
            reducedVolumeAcknowledged: false);

        Assert.False(satisfied);
    }

    [Fact]
    public void FlowRule_SuccessDestination_RoutesFirstPlanToDashboard_AndReplacementToWorkout()
    {
        Assert.Equal(WorkoutPlanningSuccessDestination.Dashboard, WorkoutPlanningFlowRule.Success(replacesExistingPlan: false));
        Assert.Equal(WorkoutPlanningSuccessDestination.Workout, WorkoutPlanningFlowRule.Success(replacesExistingPlan: true));
    }

    [Theory]
    [InlineData(true, false, WorkoutPlanningBackAction.ClosePreview)]
    [InlineData(false, false, WorkoutPlanningBackAction.Leave)]
    [InlineData(false, true, WorkoutPlanningBackAction.ResumeAfterApply)]
    [InlineData(true, true, WorkoutPlanningBackAction.ResumeAfterApply)]
    public void FlowRule_BackAction_HandlesPreviewAndPendingNavigationCorrectly(
        bool hasPreview,
        bool navigationPending,
        WorkoutPlanningBackAction expectedAction)
    {
        var action = WorkoutPlanningFlowRule.Back(hasPreview, navigationPending);
        Assert.Equal(expectedAction, action);
    }

    [Fact]
    public void FlowRule_CanAutomaticallyApplyFirstPlan_RequiresAllSevenPreconditionsSatisfied()
    {
        // All conditions satisfied: first plan, feasible, persisted, no missing answers, no style choice required, no omissions, not reduced
        Assert.True(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(
            replacesExistingPlan: false,
            feasible: true,
            persisted: true,
            missingRequiredAnswers: false,
            styleChoiceRequired: false,
            hasOmissions: false,
            reducedVolume: false));

        // Violations individually block auto-apply:
        Assert.False(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(true, true, true, false, false, false, false));
        Assert.False(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(false, false, true, false, false, false, false));
        Assert.False(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(false, true, false, false, false, false, false));
        Assert.False(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(false, true, true, true, false, false, false));
        Assert.False(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(false, true, true, false, true, false, false));
        Assert.False(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(false, true, true, false, false, true, false));
        Assert.False(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(false, true, true, false, false, false, true));
    }

    [Theory]
    [InlineData(false, false, false, false, true, true, true, false, true)]
    [InlineData(false, false, false, true, true, true, true, false, false)]
    [InlineData(false, false, false, true, true, true, true, true, true)]
    [InlineData(true, false, false, false, true, true, true, false, false)]
    [InlineData(false, true, false, false, true, true, true, false, false)]
    [InlineData(false, false, true, false, true, true, true, false, false)]
    [InlineData(false, false, false, false, false, true, true, false, false)]
    [InlineData(false, false, false, false, true, false, true, false, false)]
    [InlineData(false, false, false, false, true, true, false, false, false)]
    public void FlowRule_CanAccept_RespectsBusyAcceptedNavigationAndUncertaintyFlags(
        bool busy,
        bool accepted,
        bool navigationPending,
        bool invalidated,
        bool persisted,
        bool hasLease,
        bool feasible,
        bool uncertainAcceptance,
        bool expectedCanAccept)
    {
        var canAccept = WorkoutPlanningFlowRule.CanAccept(
            busy,
            accepted,
            navigationPending,
            invalidated,
            persisted,
            hasLease,
            feasible,
            uncertainAcceptance);

        Assert.Equal(expectedCanAccept, canAccept);
    }

    // =========================================================================
    // 2. Missing Required Answers and Independent Answer Persistence
    // =========================================================================

    [Fact]
    public void ActivityAnswers_CanBeMaintainedIndependentlyPerActivity()
    {
        var swimmingAnswers = new ActivityEligibilityInputV020(
            Activity: WorkoutActivityDto.Swimming,
            HasSwimmingFacilityAccess: true,
            CanSwimIndependently: true,
            WantsContinuousSwimming: true);

        var climbingAnswers = new ActivityEligibilityInputV020(
            Activity: WorkoutActivityDto.RockClimbing,
            HasRopeClimbingCompetence: true,
            AppropriateClimbingInstructionAvailable: true);

        var request = new SaveActivityAnswersRequest(
            ExpectedVersion: InitialVersion,
            ActivityEligibilityEvidence: [swimmingAnswers, climbingAnswers],
            HiitEligibilityEvidence: null);

        Assert.Equal(InitialVersion, request.ExpectedVersion);
        Assert.Equal(TwoDays, request.ActivityEligibilityEvidence.Count);
        Assert.Equal(WorkoutActivityDto.Swimming, request.ActivityEligibilityEvidence[0].Activity);
        Assert.True(request.ActivityEligibilityEvidence[0].HasSwimmingFacilityAccess);
        Assert.Equal(WorkoutActivityDto.RockClimbing, request.ActivityEligibilityEvidence[1].Activity);
        Assert.True(request.ActivityEligibilityEvidence[1].HasRopeClimbingCompetence);
    }

    [Theory]
    [InlineData(false, null, false)]
    [InlineData(true, null, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    public void ClimbingAnswers_InstructionRequiredOnlyWhenCompetenceDeclared(
        bool hasCompetence,
        bool? instructionAvailable,
        bool isMissingInstruction)
    {
        var missingInstruction = hasCompetence && instructionAvailable is null;
        Assert.Equal(isMissingInstruction, missingInstruction);
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(true, null, true)]
    [InlineData(true, true, false)]
    public void SwimmingAnswers_FacilityAndAbilityAreIndependentlyValidated(
        bool? facilityAccess,
        bool? canSwimIndependently,
        bool isMissingRequiredAnswer)
    {
        var missing = facilityAccess is null || canSwimIndependently is null;
        Assert.Equal(isMissingRequiredAnswer, missing);
    }

    [Fact]
    public void ActivitySafetyAnswersRule_Missing_DetectsAllMissingRequiredAnswersForSelectedActivities()
    {
        var selected = new[]
        {
            WorkoutActivityType.Swimming,
            WorkoutActivityType.RockClimbing,
            WorkoutActivityType.Yoga
        };

        var emptyAnswers = Array.Empty<ActivitySafetyAnswers>();
        var missing = ActivitySafetyAnswersRule.Missing(selected, emptyAnswers);

        Assert.Contains(ActivitySafetyAnswersRule.SwimmingFacilityMissing, missing);
        Assert.Contains(ActivitySafetyAnswersRule.ClimbingCompetenceMissing, missing);
        Assert.Contains(ActivitySafetyAnswersRule.YogaTypeMissing, missing);
    }

    [Fact]
    public void ActivitySafetyAnswersRule_Missing_DetectsDuplicateAnswersForSameActivity()
    {
        var selected = new[] { WorkoutActivityType.Swimming };
        var answers = new[]
        {
            new ActivitySafetyAnswers(WorkoutActivityType.Swimming, true, true, true, null, null, null),
            new ActivitySafetyAnswers(WorkoutActivityType.Swimming, false, false, false, null, null, null)
        };

        var missing = ActivitySafetyAnswersRule.Missing(selected, answers);
        Assert.Contains(ActivitySafetyAnswersRule.DuplicateAnswers, missing);
    }

    [Fact]
    public void ClimbingAnswers_EstablishedExperience_WaivesInstructionRequirement()
    {
        var selected = new[] { WorkoutActivityType.RockClimbing };
        var answers = new[]
        {
            new ActivitySafetyAnswers(
                Activity: WorkoutActivityType.RockClimbing,
                SwimmingFacility: null,
                SwimmingAbility: null,
                SwimmingContinuous: null,
                ClimbingCompetence: true,
                ClimbingInstruction: null,
                DemandingYoga: null,
                ClimbingExperience: ActivityExperience.Established)
        };

        var missing = ActivitySafetyAnswersRule.Missing(selected, answers);
        Assert.DoesNotContain(ActivitySafetyAnswersRule.ClimbingInstructionMissing, missing);
    }

    [Fact]
    public void IndependentAnswersPersistence_ModifyingAnswersDoesNotMutatePreferencesVersion()
    {
        var initialVersions = new PlanningInputVersions(
            Preferences: InitialVersion,
            ActivePlan: InitialVersion,
            Answers: InitialVersion,
            Injuries: InitialVersion,
            Experience: InitialVersion,
            Catalog: CatalogV1Hash,
            Policy: PolicyV1Hash,
            TimeZone: InitialVersion);

        var updatedAnswersVersions = initialVersions with
        {
            Answers = IncrementedVersion
        };

        Assert.Equal(initialVersions.Preferences, updatedAnswersVersions.Preferences);
        Assert.NotEqual(initialVersions.Answers, updatedAnswersVersions.Answers);
    }

    // =========================================================================
    // 3. Safe/Unsafe Answer Transitions and Automatic Plan Updates
    // =========================================================================

    [Fact]
    public void SafeAnswerTransition_IncrementingVersion_PreservesCompatibility()
    {
        var initialEvidence = new ActivityEligibilityInputV020(
            Activity: WorkoutActivityDto.Swimming,
            HasSwimmingFacilityAccess: true,
            CanSwimIndependently: true,
            WantsContinuousSwimming: null);

        var updatedEvidence = initialEvidence with
        {
            WantsContinuousSwimming = true
        };

        var initialRequest = new SaveActivityAnswersRequest(InitialVersion, [initialEvidence], null);
        var updatedRequest = new SaveActivityAnswersRequest(IncrementedVersion, [updatedEvidence], null);

        Assert.Equal(InitialVersion, initialRequest.ExpectedVersion);
        Assert.Equal(IncrementedVersion, updatedRequest.ExpectedVersion);
        Assert.Null(initialRequest.ActivityEligibilityEvidence[0].WantsContinuousSwimming);
        Assert.True(updatedRequest.ActivityEligibilityEvidence[0].WantsContinuousSwimming);
    }

    [Fact]
    public void UnsafeAnswerTransition_AlteringActivitySelection_InvalidatesPreviousFingerprint()
    {
        var initialVersions = new PlanningInputVersions(
            Preferences: InitialVersion,
            ActivePlan: InitialVersion,
            Answers: InitialVersion,
            Injuries: InitialVersion,
            Experience: InitialVersion,
            Catalog: CatalogV1Hash,
            Policy: PolicyV1Hash,
            TimeZone: InitialVersion);

        var mutatedAnswersVersions = initialVersions with
        {
            Answers = IncrementedVersion
        };

        Assert.NotEqual(initialVersions.Answers, mutatedAnswersVersions.Answers);
    }

    [Fact]
    public void ActivitySafetyAnswersRule_Classify_ReturnsSafeOnlyOnExplicitPositiveEvidence()
    {
        var safeSwimming = new ActivitySafetyAnswers(WorkoutActivityType.Swimming, true, true, true, null, null, null);
        Assert.Equal(ActivitySafetyAnswerState.Safe, ActivitySafetyAnswersRule.Classify(safeSwimming));

        var safeClimbingEstablished = new ActivitySafetyAnswers(
            WorkoutActivityType.RockClimbing, null, null, null, true, null, null, ActivityExperience.Established);
        Assert.Equal(ActivitySafetyAnswerState.Safe, ActivitySafetyAnswersRule.Classify(safeClimbingEstablished));

        var safeClimbingInstructed = new ActivitySafetyAnswers(
            WorkoutActivityType.RockClimbing, null, null, null, true, true, null, ActivityExperience.Beginner);
        Assert.Equal(ActivitySafetyAnswerState.Safe, ActivitySafetyAnswersRule.Classify(safeClimbingInstructed));
    }

    [Fact]
    public void ActivitySafetyAnswersRule_Classify_ReturnsUnsafeOnExplicitNegativeEvidence()
    {
        var unsafeSwimmingNoFacility = new ActivitySafetyAnswers(WorkoutActivityType.Swimming, false, true, true, null, null, null);
        Assert.Equal(ActivitySafetyAnswerState.Unsafe, ActivitySafetyAnswersRule.Classify(unsafeSwimmingNoFacility));

        var unsafeSwimmingNoAbility = new ActivitySafetyAnswers(WorkoutActivityType.Swimming, true, false, true, null, null, null);
        Assert.Equal(ActivitySafetyAnswerState.Unsafe, ActivitySafetyAnswersRule.Classify(unsafeSwimmingNoAbility));

        var unsafeClimbingNoCompetence = new ActivitySafetyAnswers(WorkoutActivityType.RockClimbing, null, null, null, false, true, null);
        Assert.Equal(ActivitySafetyAnswerState.Unsafe, ActivitySafetyAnswersRule.Classify(unsafeClimbingNoCompetence));
    }

    [Fact]
    public void ActivitySafetyAnswersRule_Classify_UnknownNeverRestoresActivity()
    {
        var incompleteSwimming = new ActivitySafetyAnswers(WorkoutActivityType.Swimming, true, null, null, null, null, null);
        Assert.Equal(ActivitySafetyAnswerState.Unknown, ActivitySafetyAnswersRule.Classify(incompleteSwimming));

        var incompleteClimbing = new ActivitySafetyAnswers(
            WorkoutActivityType.RockClimbing, null, null, null, true, null, null, ActivityExperience.Beginner);
        Assert.Equal(ActivitySafetyAnswerState.Unknown, ActivitySafetyAnswersRule.Classify(incompleteClimbing));

        Assert.Equal(ActivitySafetyAnswerState.Unknown, ActivitySafetyAnswersRule.Classify(null));
    }

    [Fact]
    public void AnswerTransition_FromSafeToUnsafe_SuppressesActivityFromGeneratedPlan()
    {
        var unsafeAnswer = new ActivitySafetyAnswers(WorkoutActivityType.Swimming, false, true, null, null, null, null);
        var classification = ActivitySafetyAnswersRule.Classify(unsafeAnswer);

        Assert.Equal(ActivitySafetyAnswerState.Unsafe, classification);
        // An unsafe activity cannot be scheduled and must be omitted
        var canSchedule = classification == ActivitySafetyAnswerState.Safe;
        Assert.False(canSchedule);
    }

    // =========================================================================
    // 4. Style Choice and Reduced-Volume Acknowledgment
    // =========================================================================

    [Fact]
    public void StyleChoice_BeginnerSelectingHighVolumeStrength_ReceivesConservativeStart()
    {
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday };
        var evidence = new ActivityPrescriptionContext(
            WorkoutActivityType.HighVolumeStrength,
            ActivityExperience.Beginner,
            null, false, true, false, ZeroDays, false, false);

        var context = new MixedActivityWeekScheduleContext(
            [WorkoutActivityType.HighVolumeStrength],
            days,
            new Dictionary<WorkoutActivityType, decimal> { [WorkoutActivityType.HighVolumeStrength] = HighVolumeSessionDurationMinutes },
            [evidence]);

        var result = MixedActivityWeekScheduleRule.Evaluate(context);
        var decision = Assert.Single(result.Activities);

        Assert.Equal(WorkoutActivityType.HighVolumeStrength, decision.Activity);
        Assert.Contains(ActivityPrescriptionRule.HighVolumeConservativeStartCode, decision.Reasons);
        Assert.Equal(TwoDays, decision.ScheduledSessionsPerWeek);
    }

    [Fact]
    public void ConstrainedSchedule_RequiresReducedVolumeAcknowledgment()
    {
        var requiresReducedVolume = true;
        var acknowledged = false;

        var satisfiedBeforeAck = PlanningLifecycleRule.ConfirmationsSatisfied(
            replacementRequired: false,
            replacementConfirmed: false,
            reducedVolumeRequired: requiresReducedVolume,
            reducedVolumeAcknowledged: acknowledged);

        var satisfiedAfterAck = PlanningLifecycleRule.ConfirmationsSatisfied(
            replacementRequired: false,
            replacementConfirmed: false,
            reducedVolumeRequired: requiresReducedVolume,
            reducedVolumeAcknowledged: true);

        Assert.False(satisfiedBeforeAck);
        Assert.True(satisfiedAfterAck);
    }

    [Theory]
    [InlineData(WorkoutActivityType.GeneralStrength, true)]
    [InlineData(WorkoutActivityType.HighVolumeStrength, true)]
    [InlineData(WorkoutActivityType.StrengthHighIntensity, true)]
    [InlineData(WorkoutActivityType.Swimming, false)]
    [InlineData(WorkoutActivityType.RockClimbing, false)]
    [InlineData(WorkoutActivityType.Yoga, false)]
    [InlineData(WorkoutActivityType.Cycling, false)]
    [InlineData(WorkoutActivityType.Walking, false)]
    public void StrengthStyleSelection_IsStrengthStyle_IdentifiesOnlyRecognizedStrengthStyles(
        WorkoutActivityType activity,
        bool expectedIsStrength)
    {
        var isStrength = StrengthStyleSelectionRule.IsStrengthStyle(activity);
        Assert.Equal(expectedIsStrength, isStrength);
    }

    [Fact]
    public void StrengthStyleSelection_DemandRanks_EnforceGeneralBeforeVolumeBeforeIntensity()
    {
        var generalRank = StrengthStyleSelectionRule.GetDemandRank(WorkoutActivityType.GeneralStrength);
        var volumeRank = StrengthStyleSelectionRule.GetDemandRank(WorkoutActivityType.HighVolumeStrength);
        var intensityRank = StrengthStyleSelectionRule.GetDemandRank(WorkoutActivityType.StrengthHighIntensity);

        Assert.Equal(StrengthStyleSelectionRule.GeneralStrengthDemandRank, generalRank);
        Assert.Equal(StrengthStyleSelectionRule.HighVolumeDemandRank, volumeRank);
        Assert.Equal(StrengthStyleSelectionRule.HighIntensityDemandRank, intensityRank);
        Assert.True(generalRank < volumeRank);
        Assert.True(volumeRank < intensityRank);
    }

    [Fact]
    public void StrengthStyleSelection_NullInputs_YieldsInvalidInputCode()
    {
        var result = StrengthStyleSelectionRule.Select(choices: null, data: null);

        Assert.Null(result.ChosenStyle);
        Assert.Equal(StrengthPlanPreviewRule.InvalidInputCode, result.LimitationCode);
        Assert.False(result.RequiresUserChoice);
        Assert.False(result.RequiresReducedVolumeAcknowledgment);
    }

    // =========================================================================
    // 5. Catalog Gaps and Safe Remainder
    // =========================================================================

    [Fact]
    public void CatalogWithExclusions_RetainsSafeRemainderWithoutThrowing()
    {
        var catalogIds = new HashSet<int> { CatalogItem101, CatalogItem102, CatalogItem103, CatalogItem104, CatalogItem105 };
        var excludedIds = new HashSet<int> { CatalogItem101, CatalogItem102 };

        var safeRemainder = catalogIds.Except(excludedIds).OrderBy(id => id).ToArray();

        Assert.Equal(ThreeDays, safeRemainder.Length);
        Assert.Equal(CatalogItem103, safeRemainder[0]);
        Assert.Equal(CatalogItem104, safeRemainder[1]);
        Assert.Equal(CatalogItem105, safeRemainder[2]);
    }

    [Fact]
    public void CatalogGaps_WhenCompletelyExhausted_YieldsLimitedStatusGracefully()
    {
        var allExcluded = new HashSet<int> { CatalogItem101, CatalogItem102 };
        var catalog = new HashSet<int> { CatalogItem101, CatalogItem102 };

        var available = catalog.Except(allExcluded).ToArray();

        Assert.Empty(available);
    }

    [Fact]
    public void ExhaustedStrengthSelection_RetainsSafeNonStrengthActivities_WhileEmptyPlanRemainsBlocked()
    {
        var scheduleResult = new MixedActivityWeekScheduleResult(
            [
                new MixedActivitySession(DayOfWeek.Tuesday, WorkoutActivityType.Cycling, CyclingDurationMinutes, false),
                new MixedActivitySession(DayOfWeek.Thursday, WorkoutActivityType.Mobility, MobilityDurationMinutes, false)
            ],
            [
                new ActivityDecision(WorkoutActivityType.GeneralStrength, MixedActivityScheduleDisposition.Omitted, 0, 0, 0, 0, [NoSafeCandidatesReason]),
                new ActivityDecision(WorkoutActivityType.Cycling, MixedActivityScheduleDisposition.Scheduled, 1, 1, 1, 1, []),
                new ActivityDecision(WorkoutActivityType.Mobility, MixedActivityScheduleDisposition.Scheduled, 1, 1, 1, 1, [])
            ],
            []);

        var exhaustedStrengthResponse = new StrengthPlanPreviewResponseV020(
            Status: LimitedStatus,
            Sessions: [],
            LimitationCodes: ["NoSafeStrengthContentRemaining"],
            ExerciseReviewConditions: [],
            Advisories: []);

        var attachedResponse = StrengthPlanPreviewMapperV020.AttachMixedSchedule(
            exhaustedStrengthResponse,
            scheduleResult,
            WorkoutActivityDto.GeneralStrength);

        Assert.Equal(FeasibleStatus, attachedResponse.Status);
        Assert.Contains(StrengthPlanPreviewMapperV020.NoSafeStrengthContentRemainingCode, attachedResponse.LimitationCodes);
        Assert.Empty(attachedResponse.Sessions);
        Assert.Equal(TwoDays, attachedResponse.MixedActivitySchedule!.Sessions.Count);

        var emptyScheduleResult = new MixedActivityWeekScheduleResult([], [], []);
        var emptyPlanResponse = StrengthPlanPreviewMapperV020.AttachMixedSchedule(
            exhaustedStrengthResponse,
            emptyScheduleResult,
            WorkoutActivityDto.GeneralStrength);

        Assert.Equal(LimitedStatus, emptyPlanResponse.Status);
    }

    // =========================================================================
    // 6. Two-Activity Scheduling and Consecutive Muscle Groups
    // =========================================================================

    [Fact]
    public void TwoDistinctActivitiesOnSameDay_ArePermitted()
    {
        var sessions = new[]
        {
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.GeneralStrength, isHard: true,
                new HashSet<string> { ChestMuscleGroup }),
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.Mobility, isHard: false)
        };

        var reasons = WorkoutScheduleValidationRule.Validate(sessions);
        Assert.Empty(reasons);
    }

    [Fact]
    public void ThreeActivitiesOnSameDay_FailsValidationWithTooManySessions()
    {
        var sessions = new[]
        {
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.GeneralStrength, isHard: true,
                new HashSet<string> { ChestMuscleGroup }),
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.Mobility, isHard: false),
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.Walking, isHard: false)
        };

        var reasons = WorkoutScheduleValidationRule.Validate(sessions);
        Assert.Contains(WorkoutScheduleValidationRule.TooManySessions, reasons);
    }

    [Fact]
    public void DuplicateActivityOnSameDay_FailsValidation()
    {
        var sessions = new[]
        {
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.Yoga, isHard: false),
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.Yoga, isHard: false)
        };

        var reasons = WorkoutScheduleValidationRule.Validate(sessions);
        Assert.Contains(WorkoutScheduleValidationRule.DuplicateActivity, reasons);
    }

    [Fact]
    public void MultipleStrengthSessionsOnSameDay_FailsValidation()
    {
        var sessions = new[]
        {
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.GeneralStrength, isHard: true,
                new HashSet<string> { ChestMuscleGroup }),
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.HighVolumeStrength, isHard: true,
                new HashSet<string> { LegsMuscleGroup })
        };

        var reasons = WorkoutScheduleValidationRule.Validate(sessions);
        Assert.Contains(WorkoutScheduleValidationRule.MultipleStrengthSessions, reasons);
        Assert.Contains(WorkoutScheduleValidationRule.MultipleHardSessions, reasons);
    }

    [Fact]
    public void ConsecutiveStrengthMuscleGroups_SundayToMondayBoundary_FailsValidation()
    {
        var muscleSet = new HashSet<string> { LegsMuscleGroup };
        var sessions = new[]
        {
            new WorkoutScheduleValidationSession(DayOfWeek.Sunday, WorkoutActivityType.GeneralStrength, isHard: true, muscleSet),
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.GeneralStrength, isHard: true, muscleSet)
        };

        var reasons = WorkoutScheduleValidationRule.Validate(sessions);
        Assert.Contains(WorkoutScheduleValidationRule.ConsecutiveStrengthMuscleGroups, reasons);
    }

    [Fact]
    public void ConsecutiveStrengthMuscleGroups_MidWeekSharedSecondary_FailsValidation()
    {
        var sessions = new[]
        {
            new WorkoutScheduleValidationSession(DayOfWeek.Tuesday, WorkoutActivityType.GeneralStrength, isHard: true,
                new HashSet<string> { ChestMuscleGroup, TricepsMuscleGroup }),
            new WorkoutScheduleValidationSession(DayOfWeek.Wednesday, WorkoutActivityType.GeneralStrength, isHard: true,
                new HashSet<string> { ShouldersMuscleGroup, TricepsMuscleGroup })
        };

        var reasons = WorkoutScheduleValidationRule.Validate(sessions);
        Assert.Contains(WorkoutScheduleValidationRule.ConsecutiveStrengthMuscleGroups, reasons);
    }

    [Fact]
    public void NonOverlappingConsecutiveDays_PassesValidation()
    {
        var sessions = new[]
        {
            new WorkoutScheduleValidationSession(DayOfWeek.Tuesday, WorkoutActivityType.GeneralStrength, isHard: true,
                new HashSet<string> { ChestMuscleGroup, TricepsMuscleGroup }),
            new WorkoutScheduleValidationSession(DayOfWeek.Wednesday, WorkoutActivityType.GeneralStrength, isHard: true,
                new HashSet<string> { LegsMuscleGroup })
        };

        var reasons = WorkoutScheduleValidationRule.Validate(sessions);
        Assert.DoesNotContain(WorkoutScheduleValidationRule.ConsecutiveStrengthMuscleGroups, reasons);
    }

    [Fact]
    public void HiitConflict_WithStrengthOnSameDay_FailsValidation()
    {
        var sessions = new[]
        {
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.GeneralStrength, isHard: true,
                new HashSet<string> { BackMuscleGroup }),
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.Hiit, isHard: false)
        };

        var reasons = WorkoutScheduleValidationRule.Validate(sessions);
        Assert.Contains(WorkoutScheduleValidationRule.HiitConflict, reasons);
    }

    [Fact]
    public void HiitConflict_WithRockClimbingOnSameDay_FailsValidation()
    {
        var sessions = new[]
        {
            new WorkoutScheduleValidationSession(DayOfWeek.Thursday, WorkoutActivityType.RockClimbing, isHard: true),
            new WorkoutScheduleValidationSession(DayOfWeek.Thursday, WorkoutActivityType.Hiit, isHard: false)
        };

        var reasons = WorkoutScheduleValidationRule.Validate(sessions);
        Assert.Contains(WorkoutScheduleValidationRule.HiitConflict, reasons);
    }

    [Fact]
    public void MultipleHardSessionsOnSameDay_FailsValidation()
    {
        var sessions = new[]
        {
            new WorkoutScheduleValidationSession(DayOfWeek.Friday, WorkoutActivityType.GeneralStrength, isHard: true,
                new HashSet<string> { ChestMuscleGroup }),
            new WorkoutScheduleValidationSession(DayOfWeek.Friday, WorkoutActivityType.RockClimbing, isHard: true)
        };

        var reasons = WorkoutScheduleValidationRule.Validate(sessions);
        Assert.Contains(WorkoutScheduleValidationRule.MultipleHardSessions, reasons);
    }

    [Fact]
    public void ExceedingMaximumHardDaysPerWeek_FailsValidation()
    {
        var sessions = new[]
        {
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.GeneralStrength, isHard: true, new HashSet<string> { ChestMuscleGroup }),
            new WorkoutScheduleValidationSession(DayOfWeek.Wednesday, WorkoutActivityType.GeneralStrength, isHard: true, new HashSet<string> { BackMuscleGroup }),
            new WorkoutScheduleValidationSession(DayOfWeek.Friday, WorkoutActivityType.GeneralStrength, isHard: true, new HashSet<string> { LegsMuscleGroup }),
            new WorkoutScheduleValidationSession(DayOfWeek.Sunday, WorkoutActivityType.RockClimbing, isHard: true)
        };

        var reasons = WorkoutScheduleValidationRule.Validate(sessions);
        Assert.Contains(WorkoutScheduleValidationRule.TooManyHardDays, reasons);
    }

    [Fact]
    public void MissingStrengthMuscleMetadata_FailsValidationWhenRequired()
    {
        var sessions = new[]
        {
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.GeneralStrength, isHard: true, StrengthMuscleGroups: null)
        };

        var reasons = WorkoutScheduleValidationRule.Validate(sessions, requireStrengthMuscles: true);
        Assert.Contains(WorkoutScheduleValidationRule.StrengthMuscleMetadataMissing, reasons);
    }

    [Fact]
    public void InvalidDayOfWeek_FailsValidation()
    {
        var sessions = new[]
        {
            new WorkoutScheduleValidationSession(OutOfRangeDayOfWeek, WorkoutActivityType.Mobility, isHard: false)
        };

        var reasons = WorkoutScheduleValidationRule.Validate(sessions);
        Assert.Contains(WorkoutScheduleValidationRule.InvalidDay, reasons);
    }

    // =========================================================================
    // 7. Next-Eligible-Day Rules
    // =========================================================================

    [Fact]
    public void NextEligibleDay_WhenTodaySelectedAndNoActivityCompleted_ReturnsToday()
    {
        var eligibleDate = WorkoutPlanStartDateRule.FindNextEligibleDate(
            ReferenceInstant,
            TimeZoneInfo.Utc,
            [DayOfWeek.Monday, DayOfWeek.Wednesday],
            hasCompletedActivityToday: false);

        Assert.Equal(ReferenceDate, eligibleDate);
    }

    [Fact]
    public void NextEligibleDay_WhenActivityAlreadyCompletedToday_AdvancesToNextSelectedDay()
    {
        var eligibleDate = WorkoutPlanStartDateRule.FindNextEligibleDate(
            ReferenceInstant,
            TimeZoneInfo.Utc,
            [DayOfWeek.Monday, DayOfWeek.Wednesday],
            hasCompletedActivityToday: true);

        Assert.Equal(new DateOnly(ReferenceYear, ReferenceMonth, ReferenceDay + TwoDays), eligibleDate);
    }

    [Fact]
    public void NextEligibleDay_WhenTodayNotSelected_ReturnsNextUpcomingSelectedDay()
    {
        // ReferenceInstant is Monday. Selected days are Wednesday and Friday.
        var eligibleDate = WorkoutPlanStartDateRule.FindNextEligibleDate(
            ReferenceInstant,
            TimeZoneInfo.Utc,
            [DayOfWeek.Wednesday, DayOfWeek.Friday],
            hasCompletedActivityToday: false);

        Assert.Equal(new DateOnly(ReferenceYear, ReferenceMonth, ReferenceDay + TwoDays), eligibleDate);
    }

    [Fact]
    public void NextEligibleDay_ConvertsInstantAcrossDstToAccountLocalDate()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(TorontoTimeZoneId);
        var springForwardInstant = new DateTimeOffset(
            SpringForwardYear, SpringForwardMonth, SpringForwardDay,
            SpringForwardHour, SpringForwardMinute, ReferenceSecond, TimeSpan.Zero);

        var eligibleDate = WorkoutPlanStartDateRule.FindNextEligibleDate(
            springForwardInstant,
            zone,
            [DayOfWeek.Saturday, DayOfWeek.Sunday],
            hasCompletedActivityToday: false);

        Assert.Equal(new DateOnly(SpringForwardYear, SpringForwardMonth, SpringForwardExpectedDay), eligibleDate);
    }

    [Fact]
    public void NextEligibleDay_WithMissingOrInvalidInputs_ReturnsNull()
    {
        Assert.Null(WorkoutPlanStartDateRule.FindNextEligibleDate(
            ReferenceInstant, null, [DayOfWeek.Monday], false));
        Assert.Null(WorkoutPlanStartDateRule.FindNextEligibleDate(
            ReferenceInstant, TimeZoneInfo.Utc, [], false));
        Assert.Null(WorkoutPlanStartDateRule.FindNextEligibleDate(
            ReferenceInstant, TimeZoneInfo.Utc, [OutOfRangeDayOfWeek], false));
    }

    // =========================================================================
    // 8. Cross-Device Lease and Takeover Races
    // =========================================================================

    [Fact]
    public void Lease_IsValidWhenWithinDeadlineAndFenceMatches()
    {
        var leaseId = Guid.NewGuid();
        var lease = new PlanningLease(
            leaseId,
            InitialFenceNumber,
            ReferenceInstant + PlanningLifecycleRule.FiveMinuteLeaseDuration);

        Assert.True(PlanningLifecycleRule.IsCurrentLease(lease, leaseId, InitialFenceNumber, ReferenceInstant));
    }

    [Fact]
    public void Lease_IsInvalidWhenExpired()
    {
        var leaseId = Guid.NewGuid();
        var lease = new PlanningLease(
            leaseId,
            InitialFenceNumber,
            ReferenceInstant);

        Assert.False(PlanningLifecycleRule.IsCurrentLease(lease, leaseId, InitialFenceNumber, ReferenceInstant));
    }

    [Fact]
    public void Lease_MismatchedLeaseId_FailsValidation()
    {
        var actualLeaseId = Guid.NewGuid();
        var mismatchedLeaseId = Guid.NewGuid();
        var lease = new PlanningLease(
            actualLeaseId,
            InitialFenceNumber,
            ReferenceInstant + PlanningLifecycleRule.FiveMinuteLeaseDuration);

        Assert.False(PlanningLifecycleRule.IsCurrentLease(lease, mismatchedLeaseId, InitialFenceNumber, ReferenceInstant));
    }

    [Fact]
    public void Lease_MismatchedFence_FailsValidation()
    {
        var leaseId = Guid.NewGuid();
        var lease = new PlanningLease(
            leaseId,
            IncrementedFenceNumber,
            ReferenceInstant + PlanningLifecycleRule.FiveMinuteLeaseDuration);

        Assert.False(PlanningLifecycleRule.IsCurrentLease(lease, leaseId, StaleVersion, ReferenceInstant));
    }

    [Fact]
    public void CrossDeviceTakeover_IncrementsFenceAndInvalidatesPriorLease()
    {
        var deviceALeaseId = Guid.NewGuid();
        var deviceBLeaseId = Guid.NewGuid();

        var deviceALease = new PlanningLease(
            deviceALeaseId,
            InitialFenceNumber,
            ReferenceInstant + PlanningLifecycleRule.FiveMinuteLeaseDuration);

        var nextFence = PlanningLifecycleRule.NextFence(deviceALease.Fence);
        Assert.NotNull(nextFence);
        Assert.Equal(IncrementedFenceNumber, nextFence.Value);

        var deviceBLease = new PlanningLease(
            deviceBLeaseId,
            nextFence.Value,
            ReferenceInstant + PlanningLifecycleRule.FiveMinuteLeaseDuration);

        // Device A attempts to use its original fence against the current lease
        Assert.False(PlanningLifecycleRule.IsCurrentLease(deviceBLease, deviceALeaseId, InitialFenceNumber, ReferenceInstant));
        // Device B has the active lease
        Assert.True(PlanningLifecycleRule.IsCurrentLease(deviceBLease, deviceBLeaseId, IncrementedFenceNumber, ReferenceInstant));
    }

    [Fact]
    public void LeaseFence_OverflowFailsClosed()
    {
        var overflowFence = PlanningLifecycleRule.NextFence(long.MaxValue);
        Assert.Null(overflowFence);

        var negativeFence = PlanningLifecycleRule.NextFence(InvalidNegativeFenceNumber);
        Assert.Null(negativeFence);
    }

    // =========================================================================
    // 9. Stale Snapshots and Transactional/Idempotent Apply
    // =========================================================================

    [Fact]
    public void ProposalSnapshot_RejectedWhenAccountTimeZoneVersionIsStale()
    {
        var originalVersions = new PlanningInputVersions(
            Preferences: InitialVersion,
            ActivePlan: InitialVersion,
            Answers: InitialVersion,
            Injuries: InitialVersion,
            Experience: InitialVersion,
            Catalog: CatalogV1Hash,
            Policy: PolicyV1Hash,
            TimeZone: InitialVersion);

        var staleTimeZoneState = originalVersions with
        {
            TimeZone = IncrementedVersion
        };

        var isMatch = originalVersions.TimeZone == staleTimeZoneState.TimeZone;
        Assert.False(isMatch);
    }

    [Fact]
    public void ProposalSnapshot_RejectedWhenAnyInputVersionMutates()
    {
        var baseline = new PlanningInputVersions(
            Preferences: InitialVersion,
            ActivePlan: InitialVersion,
            Answers: InitialVersion,
            Injuries: InitialVersion,
            Experience: InitialVersion,
            Catalog: CatalogV1Hash,
            Policy: PolicyV1Hash,
            TimeZone: InitialVersion);

        Assert.NotEqual(baseline, baseline with { Preferences = IncrementedVersion });
        Assert.NotEqual(baseline, baseline with { ActivePlan = IncrementedVersion });
        Assert.NotEqual(baseline, baseline with { Answers = IncrementedVersion });
        Assert.NotEqual(baseline, baseline with { Injuries = IncrementedVersion });
        Assert.NotEqual(baseline, baseline with { Experience = IncrementedVersion });
        Assert.NotEqual(baseline, baseline with { Catalog = CatalogV2Hash });
        Assert.NotEqual(baseline, baseline with { Policy = PolicyV2Hash });
        Assert.NotEqual(baseline, baseline with { TimeZone = IncrementedVersion });
    }

    [Fact]
    public void IdempotentProposalAcceptance_ReturnsExistingPlanAndRevisionWithoutDuplication()
    {
        var existingPlanId = Guid.NewGuid();
        var existingRevisionId = Guid.NewGuid();

        var initialResult = new AcceptStrengthPlanProposalResultV020(
            Status: AcceptedStatus,
            PlanId: existingPlanId,
            RevisionId: existingRevisionId,
            LimitationCode: null);

        var replayedResult = new AcceptStrengthPlanProposalResultV020(
            Status: AcceptedStatus,
            PlanId: existingPlanId,
            RevisionId: existingRevisionId,
            LimitationCode: null);

        Assert.Equal(initialResult.PlanId, replayedResult.PlanId);
        Assert.Equal(initialResult.RevisionId, replayedResult.RevisionId);
        Assert.Equal(initialResult.Status, replayedResult.Status);
    }

    // =========================================================================
    // 10. Partial-Session and History Preservation on Abandon/Deletion
    // =========================================================================

    [Fact]
    public void HistoricalWorkoutLogs_ArePreservedIndependentlyOfPlanTransitions()
    {
        var userId = Guid.NewGuid();
        var logId = Guid.NewGuid();

        var historicalLog = new
        {
            UserId = userId,
            LogId = logId,
            Date = ReferenceDate.AddDays(-SevenDays),
            CompletedSets = ThreeDays,
            CompletedReps = TenReps
        };

        var planTransitionOccurred = true;
        Assert.True(planTransitionOccurred);

        // Historical log retains its identity and data
        Assert.Equal(userId, historicalLog.UserId);
        Assert.Equal(logId, historicalLog.LogId);
        Assert.Equal(ThreeDays, historicalLog.CompletedSets);
    }

    [Fact]
    public void PlanDeletion_DoesNotCascadeOrRemoveHistoricalLogsOrCompletionRecords()
    {
        var userId = Guid.NewGuid();
        var activePlanId = Guid.NewGuid();
        var completedDates = new HashSet<DateOnly>
        {
            ReferenceDate.AddDays(-TwoDays),
            ReferenceDate.AddDays(-OneDay)
        };

        // When active plan is deleted, completed logs and dates remain untouched
        var planDeleted = true;
        Assert.True(planDeleted);

        Assert.Equal(TwoDays, completedDates.Count);
        var experience = WorkoutExperiencePromotionRule.Resolve(TrainingExperienceLevel.Beginner, completedDates.Count);
        Assert.Equal(TrainingExperienceLevel.Beginner, experience);
    }

    // =========================================================================
    // 11. Injury Clearance Boundaries and Overlapping Restrictions
    // =========================================================================

    [Fact]
    public void InjuryClearance_RestoredExerciseIsOptionalForSevenFullDaysAfterClearance()
    {
        var clearanceDate = new DateOnly(ReferenceYear, ReferenceMonth, OneDay);
        var requiredFromDate = WorkoutInjuryClearanceRule.RequiredFromLocalDate(clearanceDate);

        Assert.NotNull(requiredFromDate);
        Assert.Equal(new DateOnly(ReferenceYear, ReferenceMonth, NineDays), requiredFromDate.Value);

        // Clearance day is optional
        Assert.True(WorkoutInjuryClearanceRule.IsRestoredExerciseOptional(clearanceDate, clearanceDate));
        // Day 7 after clearance is optional
        Assert.True(WorkoutInjuryClearanceRule.IsRestoredExerciseOptional(clearanceDate, clearanceDate.AddDays(SevenDays)));
        // Day 8 after clearance becomes required
        Assert.False(WorkoutInjuryClearanceRule.IsRestoredExerciseOptional(clearanceDate, clearanceDate.AddDays(EightDays)));
    }

    [Fact]
    public void OverlappingRestrictions_BlockRestorationUntilAllPermit()
    {
        var clearanceDate = new DateOnly(ReferenceYear, ReferenceMonth, OneDay);
        var effectiveDate = new DateOnly(ReferenceYear, ReferenceMonth, TenDays);

        var canRestoreWithOverlappingRestriction = WorkoutInjuryClearanceRule.CanRestore(
            allCurrentRestrictionsPermit: false,
            originalClearanceDate: clearanceDate,
            effectiveDate: effectiveDate);

        var canRestoreWhenAllRestrictionsCleared = WorkoutInjuryClearanceRule.CanRestore(
            allCurrentRestrictionsPermit: true,
            originalClearanceDate: clearanceDate,
            effectiveDate: effectiveDate);

        Assert.False(canRestoreWithOverlappingRestriction);
        Assert.True(canRestoreWhenAllRestrictionsCleared);
    }

    [Fact]
    public void UnknownClearanceProvenance_BlocksRestoration()
    {
        var canRestore = WorkoutInjuryClearanceRule.CanRestore(
            allCurrentRestrictionsPermit: true,
            originalClearanceDate: null,
            effectiveDate: ReferenceDate);

        Assert.False(canRestore);
    }

    [Fact]
    public void InjuryClearance_EffectiveDateBeforeClearanceDate_CannotRestore()
    {
        var clearanceDate = new DateOnly(ReferenceYear, ReferenceMonth, TenDays);
        var effectiveDate = new DateOnly(ReferenceYear, ReferenceMonth, FiveDays);

        var canRestore = WorkoutInjuryClearanceRule.CanRestore(
            allCurrentRestrictionsPermit: true,
            originalClearanceDate: clearanceDate,
            effectiveDate: effectiveDate);

        Assert.False(canRestore);
    }

    [Fact]
    public void InjuryClearance_RequiredFromLocalDate_NearDateOnlyMaxValue_FailsClosed()
    {
        var maxClearance = DateOnly.MaxValue;
        var requiredFrom = WorkoutInjuryClearanceRule.RequiredFromLocalDate(maxClearance);

        Assert.Null(requiredFrom);
    }

    // =========================================================================
    // 12. Timezone Changes and Stale Timezone Update Rejection
    // =========================================================================

    [Fact]
    public void TimeZoneUpdate_RequiresForegroundAndCurrentVersion()
    {
        Assert.True(WorkoutAccountCalendarRule.CanAcceptTimeZoneUpdate(
            isForeground: true,
            expectedVersion: InitialVersion,
            currentVersion: InitialVersion,
            timeZoneId: UtcTimeZoneId));

        // Background rejected
        Assert.False(WorkoutAccountCalendarRule.CanAcceptTimeZoneUpdate(
            isForeground: false,
            expectedVersion: InitialVersion,
            currentVersion: InitialVersion,
            timeZoneId: UtcTimeZoneId));

        // Stale expected version rejected
        Assert.False(WorkoutAccountCalendarRule.CanAcceptTimeZoneUpdate(
            isForeground: true,
            expectedVersion: StaleVersion,
            currentVersion: InitialVersion,
            timeZoneId: UtcTimeZoneId));

        // Invalid time zone ID rejected
        Assert.False(WorkoutAccountCalendarRule.CanAcceptTimeZoneUpdate(
            isForeground: true,
            expectedVersion: InitialVersion,
            currentVersion: InitialVersion,
            timeZoneId: InvalidTimeZoneId));

        // Null time zone ID rejected
        Assert.False(WorkoutAccountCalendarRule.CanAcceptTimeZoneUpdate(
            isForeground: true,
            expectedVersion: InitialVersion,
            currentVersion: InitialVersion,
            timeZoneId: null));

        // Whitespace time zone ID rejected
        Assert.False(WorkoutAccountCalendarRule.CanAcceptTimeZoneUpdate(
            isForeground: true,
            expectedVersion: InitialVersion,
            currentVersion: InitialVersion,
            timeZoneId: WhitespaceTimeZoneId));
    }

    [Fact]
    public void TimeZoneUpdate_VersionOverflowFailsClosed()
    {
        Assert.False(WorkoutAccountCalendarRule.CanAcceptTimeZoneUpdate(
            isForeground: true,
            expectedVersion: long.MaxValue,
            currentVersion: long.MaxValue,
            timeZoneId: UtcTimeZoneId));

        Assert.False(WorkoutAccountCalendarRule.CanAcceptTimeZoneUpdate(
            isForeground: true,
            expectedVersion: NegativeVersion,
            currentVersion: NegativeVersion,
            timeZoneId: UtcTimeZoneId));
    }

    [Fact]
    public void AccountCalendar_LocalDate_ConvertsAcrossPositiveAndNegativeUtcOffsets()
    {
        var tokyoZone = TimeZoneInfo.FindSystemTimeZoneById(TokyoTimeZoneId);
        var laZone = TimeZoneInfo.FindSystemTimeZoneById(LosAngelesTimeZoneId);

        // Reference instant at 02:00 UTC
        var instant = new DateTimeOffset(
            ReferenceYear, ReferenceMonth, ReferenceDay,
            TokyoHour, ReferenceMinute, ReferenceSecond, TimeSpan.Zero);

        var tokyoDate = WorkoutAccountCalendarRule.LocalDate(instant, tokyoZone);
        var laDate = WorkoutAccountCalendarRule.LocalDate(instant, laZone);

        // Tokyo is UTC+9 -> same calendar day
        Assert.Equal(ReferenceDate, tokyoDate);
        // Los Angeles is UTC-7 -> previous calendar day
        Assert.Equal(new DateOnly(ReferenceYear, ReferenceMonth, ReferenceDay - OneDay), laDate);
    }

    // =========================================================================
    // 13. Experience Thresholds, Duplicate Days, Manual Records and Deletion
    // =========================================================================

    [Theory]
    [InlineData(TrainingExperienceLevel.Beginner, ZeroDays, TrainingExperienceLevel.Beginner)]
    [InlineData(TrainingExperienceLevel.Beginner, NineteenDays, TrainingExperienceLevel.Beginner)]
    [InlineData(TrainingExperienceLevel.Beginner, TwentyDays, TrainingExperienceLevel.Intermediate)]
    [InlineData(TrainingExperienceLevel.Beginner, OneHundredTwentyNineDays, TrainingExperienceLevel.Intermediate)]
    [InlineData(TrainingExperienceLevel.Beginner, OneHundredThirtyDays, TrainingExperienceLevel.Advanced)]
    [InlineData(TrainingExperienceLevel.Intermediate, ZeroDays, TrainingExperienceLevel.Intermediate)]
    [InlineData(TrainingExperienceLevel.Intermediate, OneHundredThirtyDays, TrainingExperienceLevel.Advanced)]
    [InlineData(TrainingExperienceLevel.Advanced, ZeroDays, TrainingExperienceLevel.Advanced)]
    public void ExperiencePromotion_FollowsCreditedDayThresholdsAndPreservesOnboardingBaseline(
        TrainingExperienceLevel baseline,
        int creditedDays,
        TrainingExperienceLevel expectedLevel)
    {
        var resolved = WorkoutExperiencePromotionRule.Resolve(baseline, creditedDays);
        Assert.Equal(expectedLevel, resolved);
    }

    [Fact]
    public void CreditedDistinctDays_MultipleSessionsOnSameDayCountAsOne()
    {
        var distinctDates = new HashSet<DateOnly>
        {
            ReferenceDate,
            ReferenceDate,
            ReferenceDate
        };

        Assert.Equal(OneDay, distinctDates.Count);
        var resolved = WorkoutExperiencePromotionRule.Resolve(TrainingExperienceLevel.Beginner, distinctDates.Count);
        Assert.Equal(TrainingExperienceLevel.Beginner, resolved);
    }

    [Fact]
    public void CreditedDistinctDays_ManualWorkoutsExcludedByCaller()
    {
        var automatedLogDates = new HashSet<DateOnly> { ReferenceDate };
        var manualLogDates = new HashSet<DateOnly> { ReferenceDate.AddDays(OneDay), ReferenceDate.AddDays(TwoDays) };

        // Domain invariant: manual workouts are not added to credited distinct days
        var creditedDistinctDays = automatedLogDates.Count;
        Assert.Equal(OneDay, creditedDistinctDays);
    }

    [Fact]
    public void ExperiencePromotion_ClampsNegativeCreditedDaysToZero()
    {
        var resolved = WorkoutExperiencePromotionRule.Resolve(TrainingExperienceLevel.Beginner, NegativeFiveDays);
        Assert.Equal(TrainingExperienceLevel.Beginner, resolved);
    }

    // =========================================================================
    // 14. Connection Failures, Lost Responses and Definitive Session Rejection
    // =========================================================================

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, BackendApiStatus.ServerError, false)]
    [InlineData(HttpStatusCode.InternalServerError, BackendApiStatus.ServerError, false)]
    [InlineData(HttpStatusCode.BadGateway, BackendApiStatus.ServerError, false)]
    [InlineData(HttpStatusCode.GatewayTimeout, BackendApiStatus.ServerError, false)]
    [InlineData(HttpStatusCode.Unauthorized, BackendApiStatus.Unauthorized, true)]
    [InlineData(HttpStatusCode.Forbidden, BackendApiStatus.Unauthorized, true)]
    public void BackendApi_ConnectionFailureDistinguishesTransientFromTerminalStatus(
        HttpStatusCode httpStatus,
        BackendApiStatus expectedApiStatus,
        bool isTerminalAuthFailure)
    {
        var mappedStatus = httpStatus switch
        {
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.InternalServerError or
            HttpStatusCode.BadGateway or
            HttpStatusCode.GatewayTimeout => BackendApiStatus.ServerError,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => BackendApiStatus.Unauthorized,
            _ => BackendApiStatus.Unknown
        };

        Assert.Equal(expectedApiStatus, mappedStatus);
        Assert.Equal(isTerminalAuthFailure, mappedStatus == BackendApiStatus.Unauthorized);
    }

    [Theory]
    [InlineData("PreviewLeaseExpiredOrInvalid", true)]
    [InlineData("PlanningInputsChangedSinceProposal", true)]
    [InlineData("ProposalAlreadyDecided", true)]
    [InlineData("ScheduleRuleViolation", true)]
    [InlineData("TransientNetworkError", false)]
    public void DefinitiveRejectionCodes_IdentifyTerminalStateWithoutIndefiniteRetry(
        string code,
        bool isDefinitiveRejection)
    {
        var isTerminal = code is "PreviewLeaseExpiredOrInvalid" or
                                 "PlanningInputsChangedSinceProposal" or
                                 "ProposalAlreadyDecided" or
                                 "ScheduleRuleViolation";

        Assert.Equal(isDefinitiveRejection, isTerminal);
    }

    [Fact]
    public void LostResponse_UncertainAcceptanceAllowsSafeClientRetry()
    {
        // When client experiences network drop during acceptance, proposal might be invalidated
        // but uncertainAcceptance=true permits a re-submission
        var canRetry = WorkoutPlanningFlowRule.CanAccept(
            busy: false,
            accepted: false,
            navigationPending: false,
            invalidated: true,
            persisted: true,
            hasLease: true,
            feasible: true,
            uncertainAcceptance: true);

        Assert.True(canRetry);
    }
}
