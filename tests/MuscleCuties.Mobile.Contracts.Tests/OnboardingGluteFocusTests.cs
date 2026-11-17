using MuscleCuties.Contracts.Onboarding;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.UI.ViewModels.Quiz;
using MuscleCuties.UseCases.Features.Onboarding.Quiz;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

public sealed class OnboardingGluteFocusTests
{
    [Fact]
    public async Task Glute_choice_is_saved_after_account_creation_and_before_first_plan_navigation()
    {
        var useCase = new Quiz();
        var saved = false;
        var navigated = false;
        var vm = new QuizViewModel(useCase, () => { Assert.True(saved); navigated = true; return Task.CompletedTask; },
            null, () => Task.CompletedTask, saveGluteFocusAsync: value =>
            {
                Assert.Equal(1, useCase.Commits);
                Assert.True(value);
                saved = true;
                return Task.FromResult(true);
            }) { GluteFocus = true };
        await CompleteAsync(vm);
        Assert.True(saved);
        Assert.True(navigated);
    }

    [Fact]
    public async Task Failed_focus_save_blocks_generation_and_retry_does_not_commit_quiz_twice()
    {
        var useCase = new Quiz();
        var navigated = false;
        var canSave = false;
        var vm = new QuizViewModel(useCase, () => { navigated = true; return Task.CompletedTask; }, null,
            () => Task.CompletedTask, saveGluteFocusAsync: _ => Task.FromResult(canSave)) { GluteFocus = true };
        await CompleteAsync(vm);
        Assert.False(navigated);
        Assert.Equal("Onboarding_Quiz_GluteFocusSaveFailed", vm.ErrorMessage);
        canSave = true;
        await vm.NextCommand.ExecuteAsync(null);
        Assert.True(navigated);
        Assert.Equal(1, useCase.Commits);
    }

    [Fact]
    public async Task Untouched_focus_choice_does_not_overwrite_saved_preferences()
    {
        var vm = new QuizViewModel(new Quiz(), () => Task.CompletedTask, null, () => Task.CompletedTask,
            saveGluteFocusAsync: _ => throw new InvalidOperationException("An untouched preference must be preserved."));
        await CompleteAsync(vm);
        Assert.Empty(vm.ErrorMessage);
    }

    private static async Task CompleteAsync(QuizViewModel vm)
    {
        await vm.LoadQuestionsCommand.ExecuteAsync(null);
        vm.SelectAnswerCommand.Execute(vm.CurrentAnswers[0]);
        await vm.NextCommand.ExecuteAsync(null);
        vm.SelectAnswerCommand.Execute(vm.CurrentAnswers[0]);
        vm.SelectAnswerCommand.Execute(vm.CurrentAnswers[3]);
        await vm.NextCommand.ExecuteAsync(null);
    }

    private sealed class Quiz : IOnboardingQuizUseCase
    {
        public int Commits { get; private set; }
        public Task<IReadOnlyList<QuizQuestionDto>> LoadQuestionsAsync(bool refresh = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<QuizQuestionDto>>([
                new(1, "Synthetic goal", 1, QuizQuestionType.Goal, [new(1, "Synthetic strength goal", 1, 2)]),
                new(2, "Synthetic weekdays", 2, QuizQuestionType.AvailableWeekdays,
                    Enumerable.Range(0, 7).Select(day => new QuizAnswerDto(10 + day, $"Synthetic day {day}", day + 1, day)).ToArray())]);

        public Task<QuizCommitResult> CommitAsync(IReadOnlyList<QuizSelectionDto> selections, CancellationToken cancellationToken = default)
        {
            Commits++;
            return Task.FromResult(new QuizCommitResult(true, false, null));
        }
    }
}
