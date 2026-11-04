using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.BatchAndCopy;
using DiaryMealType = MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts.MealType;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class DiaryCopyDateTests
{
    private static readonly DateOnly SourceDate = new(2026, 10, 18);
    private static readonly DateOnly AlternateSourceDate = new(2026, 10, 15);
    private static readonly DateOnly TargetDate = new(2026, 10, 20);
    private static readonly Guid OwnerId = Guid.Parse("11000000-0000-0000-0000-000000000001");
    private const string CopyCommandKey = "copy-selected-date";

    [Fact]
    public void DomainRequiresTwoSelectedDifferentDates()
    {
        Assert.Equal(SourceDate, DiaryCopyDates.Create(SourceDate, TargetDate).SourceDate);
        Assert.Throws<ArgumentException>(() => DiaryCopyDates.Create(default, TargetDate));
        Assert.Throws<ArgumentException>(() => DiaryCopyDates.Create(SourceDate, default));
        Assert.Throws<ArgumentException>(() => DiaryCopyDates.Create(TargetDate, TargetDate));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExplicitSourceDateReachesStoreForMealAndWholeDay(bool copyMeal)
    {
        var store = new RecordingStore();
        var notifier = new RecordingNotifier();
        var useCase = new NutritionBatchAndCopyUseCase(store, notifier);
        DiaryMealType? requestedMeal = copyMeal ? DiaryMealType.Breakfast : null;

        await useCase.CopyAsync(OwnerId,
            new CopyNutritionDiaryRequest(CopyCommandKey, AlternateSourceDate, TargetDate, requestedMeal));

        Assert.Equal(OwnerId, store.CapturedOwner);
        Assert.Equal(AlternateSourceDate, store.CapturedRequest?.SourceDate);
        Assert.Equal(TargetDate, store.CapturedRequest?.TargetDate);
        Assert.Equal(requestedMeal, store.CapturedRequest?.MealType);
        Assert.Equal((OwnerId, TargetDate), notifier.LastChange);
    }

    [Fact]
    public async Task SameDateAndMissingDatesNeverReachStore()
    {
        var store = new RecordingStore();
        var notifier = new RecordingNotifier();
        var useCase = new NutritionBatchAndCopyUseCase(store, notifier);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.CopyAsync(OwnerId,
            new CopyNutritionDiaryRequest(CopyCommandKey, TargetDate, TargetDate)));
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.CopyAsync(OwnerId,
            new CopyNutritionDiaryRequest(CopyCommandKey, default, TargetDate)));
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.CopyAsync(OwnerId,
            new CopyNutritionDiaryRequest(CopyCommandKey, SourceDate, default)));

        Assert.Equal(0, store.CopyCalls);
        Assert.Null(notifier.LastChange);
    }

    [Fact]
    public async Task MissingOwnerNeverReachesStore()
    {
        var store = new RecordingStore();
        var useCase = new NutritionBatchAndCopyUseCase(store);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.CopyAsync(Guid.Empty,
            new CopyNutritionDiaryRequest(CopyCommandKey, SourceDate, TargetDate)));

        Assert.Equal(0, store.CopyCalls);
    }

    private sealed class RecordingStore : INutritionBatchAndCopyStore
    {
        public Guid? CapturedOwner { get; private set; }
        public CopyNutritionDiaryRequest? CapturedRequest { get; private set; }
        public int CopyCalls { get; private set; }

        public Task<CopyNutritionDiaryResponse> CopyAsync(Guid userId, CopyNutritionDiaryRequest request,
            CancellationToken cancellationToken)
        {
            CapturedOwner = userId;
            CapturedRequest = request;
            CopyCalls++;
            return Task.FromResult(new CopyNutritionDiaryResponse(Guid.NewGuid(), request.TargetDate,
                DailyFoodEntry.MinimumBatchItems, ["synthetic-entry"], false));
        }

        public Task<BatchSaveNutritionDiaryResponse> BatchSaveAsync(Guid userId,
            BatchSaveNutritionDiaryRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<QuickAddNutritionDiaryResponse> QuickAddAsync(Guid userId,
            QuickAddNutritionDiaryRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingNotifier : INutritionBatchAndCopyChangeNotifier
    {
        public (Guid UserId, DateOnly Date)? LastChange { get; private set; }

        public void NotifyNutritionChanged(Guid userId, DateOnly date) => LastChange = (userId, date);
    }
}
