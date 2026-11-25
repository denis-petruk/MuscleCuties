using MuscleCuties.Core.Models.Entities.Quiz;

namespace MuscleCuties.Core.Services.Quiz;

/// <summary>Holds the quiz questions once loaded, so returning to the quiz does not refetch them.</summary>
public sealed class QuizQuestionCache
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private List<QuizQuestion>? _questions;

    /// <summary>The questions, loaded once and shared by every later caller.</summary>
    public async Task<IReadOnlyList<QuizQuestion>> GetOrLoadAsync(Func<Task<List<QuizQuestion>>> loadAsync)
    {
        if (_questions is not null)
            return _questions;

        await _semaphore.WaitAsync();
        try
        {
            _questions ??= await loadAsync();
            return _questions;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>Forgets the loaded questions, which is what a sign-out or an account change needs.</summary>
    public void Clear()
    {
        _questions = null;
    }
}
