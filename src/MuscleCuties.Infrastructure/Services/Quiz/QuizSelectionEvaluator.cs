using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Workout.Planning;

namespace MuscleCuties.Core.Services.Quiz;

internal sealed record QuizSelection(
    UserQuizResponse Response,
    QuizQuestion Question,
    QuizAnswer Answer);

internal static class QuizSelectionEvaluator
{
    public static List<QuizSelection> BuildValidSelections(
        int userId,
        IEnumerable<UserQuizResponse> responses,
        IReadOnlyDictionary<int, QuizQuestion> questionMap,
        DateTime answeredAt)
    {
        var selections = responses
            .Select(response => BuildSelection(userId, response, questionMap, answeredAt))
            .Where(selection => selection is not null)
            .Select(selection => selection!)
            .ToList();

        return selections
            .GroupBy(selection => selection.Question.Id)
            .SelectMany(NormalizeQuestionSelections)
            .OrderBy(selection => selection.Question.OrderIndex)
            .ThenBy(selection => selection.Answer.OrderIndex)
            .ToList();
    }

    public static QuizSelection? BuildSelection(
        int userId,
        UserQuizResponse response,
        IReadOnlyDictionary<int, QuizQuestion> questionMap,
        DateTime answeredAt)
    {
        if (!questionMap.TryGetValue(response.QuizQuestionId, out var question))
            return null;

        var answer = question.Answers.FirstOrDefault(a => a.Id == response.QuizAnswerId);
        if (answer is null)
            return null;

        response.UserId = userId;
        response.AnsweredAt = answeredAt;
        response.QuizQuestionId = question.Id;
        response.QuizAnswerId = answer.Id;

        return new QuizSelection(response, question, answer);
    }

    public static void ApplyQuizSelectionsToProfile(
        UserProfile profile,
        IReadOnlyList<QuizSelection> selections,
        IReadOnlyCollection<WorkoutActivityType>? stagedActivities = null,
        bool preserveStagedWorkoutDays = false,
        int minWorkoutDays = 0)
    {
        var dietarySelections = selections
            .Where(selection => selection.Question.QuestionType is QuizQuestionType.DietaryPreference)
            .ToList();

        foreach (var selection in selections.Where(selection =>
                     selection.Question.QuestionType is not QuizQuestionType.DietaryPreference))
            switch (selection.Question.QuestionType)
            {
                case QuizQuestionType.Goal:
                    profile.Goal = MapEnum(selection.Answer.MappedValue, UserGoal.MaintainHealth);
                    break;
                case QuizQuestionType.GoalPace:
                    profile.WeightGoalPace = MapEnum(selection.Answer.MappedValue, WeightGoalPace.Steady);
                    break;
                case QuizQuestionType.ExperienceLevel:
                    profile.TrainingExperienceLevel =
                        MapEnum(selection.Answer.MappedValue, TrainingExperienceLevel.Unknown);
                    break;
                case QuizQuestionType.WorkoutDaysPerWeek:
                    if (!preserveStagedWorkoutDays)
                        profile.WorkoutDaysPerWeek = Math.Clamp(selection.Answer.MappedValue, minWorkoutDays, 7);
                    break;
                case QuizQuestionType.CurrentCyclePhase:
                    profile.CycleTrackingMode = CycleTrackingMode.ManualPhaseLogging;
                    profile.CurrentCyclePhase = MapEnum(selection.Answer.MappedValue, CyclePhase.Follicular);
                    break;
                case QuizQuestionType.SessionDuration:
                    profile.SessionDurationMinutes = MapSessionDuration(selection.Answer.MappedValue);
                    break;
                case QuizQuestionType.Equipment:
                    profile.EquipmentLevel = MapEquipment(selection.Answer.MappedValue);
                    break;
            }

        if (!selections.Any(selection => selection.Question.QuestionType is QuizQuestionType.GoalPace) ||
            profile.Goal is not (UserGoal.FatLoss or UserGoal.Strength))
            profile.WeightGoalPace = WeightGoalPace.Steady;

        var strengthStyle = profile.SessionDurationMinutes is > 0 and <= 45
            ? StrengthTrainingStyle.ExpressHard
            : StrengthTrainingStyle.ComfortableModerate;

        var selectedActivities = stagedActivities is { Count: > 0 }
            ? stagedActivities
            : WorkoutActivityPreferences.Parse(profile.PreferredWorkoutActivityTypes);

        profile.PreferredWorkoutActivityTypes = WorkoutActivityPreferences.Serialize(
            selectedActivities,
            strengthStyle);

        if (dietarySelections.Count > 0)
            profile.DietaryTags = BuildDietaryTags(dietarySelections.Select(s => s.Answer.MappedValue));

        profile.PhaseBaselinesJson = BuildPhaseBaselinesJson(selections);
    }

    public static int MapSessionDuration(int mappedValue)
    {
        return mappedValue switch
        {
            1 => 30,
            2 => 45,
            3 => 60,
            4 => 75,
            5 => 90,
            _ => 60
        };
    }

    public static string MapEquipment(int mappedValue)
    {
        return mappedValue switch
        {
            1 => Equipment.FullGym.ToString(),
            2 => Equipment.HomeDumbbellsBands.ToString(),
            3 => Equipment.Bodyweight.ToString(),
            _ => Equipment.FullGym.ToString()
        };
    }

    public static string BuildPhaseBaselinesJson(IReadOnlyCollection<QuizSelection> selections)
    {
        var values = selections
            .Where(s => IsBaselineQuestion(s.Question.QuestionType))
            .GroupBy(s => s.Question.QuestionType)
            .ToDictionary(g => g.Key, g => g.Last().Answer.MappedValue);

        if (values.Count == 0)
            return string.Empty;

        var baselines = new CyclePhaseBaselines(
            new PhaseBaseline(
                values.GetValueOrDefault(QuizQuestionType.MenstrualPain, 3),
                values.GetValueOrDefault(QuizQuestionType.MenstrualEnergy, 2)),
            new PhaseBaseline(
                values.GetValueOrDefault(QuizQuestionType.FollicularPain, 1),
                values.GetValueOrDefault(QuizQuestionType.FollicularEnergy, 4)),
            new PhaseBaseline(
                values.GetValueOrDefault(QuizQuestionType.OvulatoryPain, 2),
                values.GetValueOrDefault(QuizQuestionType.OvulatoryEnergy, 5)),
            new PhaseBaseline(
                values.GetValueOrDefault(QuizQuestionType.LutealPain, 3),
                values.GetValueOrDefault(QuizQuestionType.LutealEnergy, 3)));

        return AdaptiveProfileMapper.SerializeBaselines(baselines);
    }

    public static bool IsBaselineQuestion(QuizQuestionType type)
    {
        return type is
            QuizQuestionType.MenstrualPain or QuizQuestionType.MenstrualEnergy or
            QuizQuestionType.FollicularPain or QuizQuestionType.FollicularEnergy or
            QuizQuestionType.OvulatoryPain or QuizQuestionType.OvulatoryEnergy or
            QuizQuestionType.LutealPain or QuizQuestionType.LutealEnergy;
    }

    public static TEnum MapEnum<TEnum>(int value, TEnum fallback) where TEnum : struct, Enum
    {
        return Enum.IsDefined(typeof(TEnum), value) ? (TEnum)(object)value : fallback;
    }

    public static IEnumerable<QuizSelection> NormalizeQuestionSelections(IGrouping<int, QuizSelection> group)
    {
        var selections = group
            .GroupBy(selection => selection.Answer.Id)
            .Select(answerGroup => answerGroup.Last())
            .ToList();

        if (selections.First().Question.QuestionType is not QuizQuestionType.DietaryPreference)
            return [selections.Last()];

        var selectedTags = selections
            .Where(selection => selection.Answer.MappedValue != (int)DietaryTag.None)
            .ToList();

        return selectedTags.Count > 0 ? selectedTags : [selections.Last()];
    }

    public static string BuildDietaryTags(IEnumerable<int> mappedValues)
    {
        var tags = mappedValues
            .Select(value => MapEnum(value, DietaryTag.None))
            .Where(tag => tag is not DietaryTag.None)
            .Distinct()
            .Select(tag => tag.ToString())
            .ToList();

        return string.Join(",", tags);
    }
}
