using MuscleCuties.Core.Models.Entities.Quiz;

namespace MuscleCuties.Core.Services.Quiz;

public sealed class QuizQuestionCache
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private List<QuizQuestion>? _questions;

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

    public void Clear() => _questions = null;
}
