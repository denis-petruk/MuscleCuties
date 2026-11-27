using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.BatchAndCopy;
using CoreDiaryCopyMode = MuscleCuties.Core.Models.Enums.Nutrition.DiaryCopyMode;
using CoreMealType = MuscleCuties.Core.Models.Enums.Nutrition.MealType;
using ContractMealType = MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts.MealType;
using DiaryCopyModeDto = MuscleCuties.UseCases.Features.Nutrition.BatchAndCopy.DiaryCopyModeDto;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class DiaryCopyPreviewTests
{
    private static readonly Guid Owner = Guid.Parse("21000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherOwner = Guid.Parse("21000000-0000-0000-0000-000000000002");
    private static readonly DateOnly SourceDate = new(2026, 10, 15);
    private static readonly DateOnly TargetDate = new(2026, 10, 20);
    private const string OriginalSourceRevision = "source-v1";
    private const string OriginalTargetRevision = "target-v1";
    private const string ChangedTargetRevision = "target-v2";
    private const string CopyKey = "previewed-copy";

    [Fact]
    public async Task PreviewMergeReadsOnlyAndCountsDuplicateFoodsAndLegacyGroupMembers()
    {
        var store = SeededStore();
        var useCase = new NutritionCopyPreviewUseCase(store);

        var preview = await useCase.PreviewAsync(Owner,
            new PreviewNutritionCopyRequest(SourceDate, TargetDate, ContractMealType.Breakfast, DiaryCopyModeDto.Merge));

        Assert.Equal(0, store.CommitCalls);
        Assert.Equal(2, store.ReadCalls);
        Assert.Equal(2, preview.SourceTotals.Count);
        Assert.Equal(1, preview.ExistingTargetTotals.Count);
        Assert.Equal(4, preview.ResultingTargetTotals.Count);
        Assert.Equal(700m, preview.ResultingTargetTotals.Calories);
        Assert.Equal("legacy-group", preview.SourceItems[0].GroupIdentity);
        Assert.Equal("legacy-group", preview.SourceItems[1].GroupIdentity);
        Assert.Equal(OriginalSourceRevision, preview.SourceRevision);
        Assert.Equal(OriginalTargetRevision, preview.TargetRevision);
    }

    [Fact]
    public void SelectedMealOverwriteKeepsUnrelatedMealAndWholeDayOverwriteRemovesIt()
    {
        var source = new[] { Food("source", CoreMealType.Breakfast, 120m) };
        var target = new[]
        {
            Food("breakfast", CoreMealType.Breakfast, 90m),
            Food("dinner", CoreMealType.Dinner, 300m)
        };

        var meal = DiaryCopyProjection.Create(source, target, CoreMealType.Breakfast, CoreDiaryCopyMode.Overwrite);
        var day = DiaryCopyProjection.Create(source, target, null, CoreDiaryCopyMode.Overwrite);

        Assert.Equal(2, meal.ResultingTargetTotals.Count);
        Assert.Equal(420m, meal.ResultingTargetTotals.Calories);
        Assert.Contains(meal.ResultingTargetItems, item => item.Identity == "dinner");
        Assert.Single(day.ResultingTargetItems);
        Assert.Equal(120m, day.ResultingTargetTotals.Calories);
    }

    [Fact]
    public void MalformedDuplicateIdentityCannotInflatePreview()
    {
        var repeated = Food("same-entry", CoreMealType.Breakfast, 120m);

        Assert.Throws<ArgumentException>(() => DiaryCopyProjection.Create(
            [repeated, repeated], [], null, CoreDiaryCopyMode.Merge));
    }

    [Fact]
    public async Task StaleTargetRejectsCommitWithoutWriteOrNotification()
    {
        var store = SeededStore();
        var notifier = new RecordingNotifier();
        var useCase = new NutritionCopyPreviewUseCase(store, notifier);
        var preview = await useCase.PreviewAsync(Owner,
            new PreviewNutritionCopyRequest(SourceDate, TargetDate, null, DiaryCopyModeDto.Overwrite));
        store.TargetRevision = ChangedTargetRevision;

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.CommitAsync(Owner,
            Commit(preview)));

        Assert.Equal(0, store.Writes);
        Assert.Equal(0, notifier.Calls);
    }

    [Fact]
    public async Task StaleSourceRejectsCommitWithoutWrite()
    {
        var store = SeededStore();
        var useCase = new NutritionCopyPreviewUseCase(store);
        var preview = await useCase.PreviewAsync(Owner,
            new PreviewNutritionCopyRequest(SourceDate, TargetDate, null, DiaryCopyModeDto.Merge));
        store.SourceRevision = "source-v2";

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.CommitAsync(Owner,
            Commit(preview)));

        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task PreviewRejectsAnotherOwnersSnapshotAndCommitRejectsMissingOwner()
    {
        var store = SeededStore();
        store.SourceOwner = OtherOwner;
        var useCase = new NutritionCopyPreviewUseCase(store);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.PreviewAsync(Owner,
            new PreviewNutritionCopyRequest(SourceDate, TargetDate, null, DiaryCopyModeDto.Merge)));
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.CommitAsync(Guid.Empty,
            new CommitPreviewedNutritionCopyRequest(CopyKey, SourceDate, TargetDate, null,
                DiaryCopyModeDto.Merge, OriginalSourceRevision, OriginalTargetRevision)));

        Assert.Equal(0, store.CommitCalls);
    }

    [Fact]
    public async Task CommitReplaysSameKeyWithoutSecondWriteOrNotification()
    {
        var store = SeededStore();
        var notifier = new RecordingNotifier();
        var useCase = new NutritionCopyPreviewUseCase(store, notifier);
        var preview = await useCase.PreviewAsync(Owner,
            new PreviewNutritionCopyRequest(SourceDate, TargetDate, null, DiaryCopyModeDto.Merge));
        var command = Commit(preview);

        var first = await useCase.CommitAsync(Owner, command);
        var replay = await useCase.CommitAsync(Owner, command);

        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(1, store.Writes);
        Assert.Equal(1, notifier.Calls);
    }

    [Fact]
    public async Task ReusedKeyWithDifferentModeAndOtherOwnerCannotWrite()
    {
        var store = SeededStore();
        var useCase = new NutritionCopyPreviewUseCase(store);
        var preview = await useCase.PreviewAsync(Owner,
            new PreviewNutritionCopyRequest(SourceDate, TargetDate, null, DiaryCopyModeDto.Merge));
        var command = Commit(preview);
        await useCase.CommitAsync(Owner, command);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.CommitAsync(Owner,
            command with { Mode = DiaryCopyModeDto.Overwrite }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.CommitAsync(OtherOwner,
            command));

        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public async Task EmptySourcePreviewIsReadOnlyAndShowsExistingTarget()
    {
        var store = SeededStore();
        store.SourceItems = [];
        var useCase = new NutritionCopyPreviewUseCase(store);

        var preview = await useCase.PreviewAsync(Owner,
            new PreviewNutritionCopyRequest(SourceDate, TargetDate, null, DiaryCopyModeDto.Merge));

        Assert.Empty(preview.SourceItems);
        Assert.Equal(preview.ExistingTargetTotals, preview.ResultingTargetTotals);
        Assert.Equal(0, store.Writes);
    }

    private static CommitPreviewedNutritionCopyRequest Commit(PreviewNutritionCopyResponse preview) =>
        new(CopyKey, preview.SourceDate, preview.TargetDate, preview.MealType, preview.Mode,
            preview.SourceRevision, preview.TargetRevision);

    private static DiaryCopyFood Food(string identity, CoreMealType mealType, decimal calories) =>
        new(identity, identity, mealType, calories, decimal.Zero, decimal.Zero, decimal.Zero);

    private static DiaryCopyFoodDto FoodDto(string identity, ContractMealType mealType, decimal calories,
        string? groupIdentity = null) =>
        new(identity, identity, mealType, calories, decimal.Zero, decimal.Zero, decimal.Zero, groupIdentity);

    private static RecordingStore SeededStore() => new()
    {
        SourceItems =
        [
            FoodDto("source-a", ContractMealType.Breakfast, 100m, "legacy-group"),
            FoodDto("source-b", ContractMealType.Breakfast, 100m, "legacy-group")
        ],
        TargetItems =
        [
            FoodDto("target-a", ContractMealType.Breakfast, 200m),
            FoodDto("target-dinner", ContractMealType.Dinner, 300m)
        ]
    };

    private sealed class RecordingStore : INutritionCopyPreviewStore
    {
        private readonly Dictionary<string, (CommitPreviewedNutritionCopyRequest Request,
            CopyNutritionDiaryResponse Response)> _replays = [];
        public Guid SourceOwner { get; set; } = Owner;
        public string SourceRevision { get; set; } = OriginalSourceRevision;
        public IReadOnlyList<DiaryCopyFoodDto> SourceItems { get; set; } = [];
        public IReadOnlyList<DiaryCopyFoodDto> TargetItems { get; set; } = [];
        public string TargetRevision { get; set; } = OriginalTargetRevision;
        public int ReadCalls { get; private set; }
        public int CommitCalls { get; private set; }
        public int Writes { get; private set; }

        public Task<DiaryCopySnapshotDto> ReadSnapshotAsync(Guid userId, DateOnly date,
            CancellationToken cancellationToken)
        {
            ReadCalls++;
            return Task.FromResult(date == SourceDate
                ? new DiaryCopySnapshotDto(SourceOwner, date, SourceRevision, SourceItems)
                : new DiaryCopySnapshotDto(userId, date, TargetRevision, TargetItems));
        }

        public Task<CopyNutritionDiaryResponse> CommitPreviewedCopyAsync(Guid userId,
            CommitPreviewedNutritionCopyRequest request, CancellationToken cancellationToken)
        {
            CommitCalls++;
            if (userId != Owner)
                throw new InvalidOperationException("Copy owner changed.");
            if (_replays.TryGetValue(request.IdempotencyKey, out var replay))
            {
                if (replay.Request != request)
                    throw new InvalidOperationException("The idempotency key was reused for another copy.");
                return Task.FromResult(replay.Response with { Replayed = true });
            }
            if (request.ExpectedSourceRevision != SourceRevision ||
                request.ExpectedTargetRevision != TargetRevision)
                throw new InvalidOperationException("Refresh stale diary preview.");
            var projection = DiaryCopyProjection.Create(
                SourceItems.Select(item => Food(item.Identity, ToCoreMealType(item.MealType), item.Calories)).ToArray(),
                TargetItems.Select(item => Food(item.Identity, ToCoreMealType(item.MealType), item.Calories)).ToArray(),
                request.MealType is { } mealType ? ToCoreMealType(mealType) : null,
                request.Mode == DiaryCopyModeDto.Merge ? CoreDiaryCopyMode.Merge : CoreDiaryCopyMode.Overwrite);
            Writes++;
            var response = new CopyNutritionDiaryResponse(Guid.NewGuid(), request.TargetDate,
                projection.SourceTotals.Count, [], false);
            _replays.Add(request.IdempotencyKey, (request, response));
            return Task.FromResult(response);
        }
    }

    private static CoreMealType ToCoreMealType(ContractMealType mealType) => mealType switch
    {
        ContractMealType.Breakfast => CoreMealType.Breakfast,
        ContractMealType.Lunch => CoreMealType.Lunch,
        ContractMealType.Dinner => CoreMealType.Dinner,
        ContractMealType.Snack => CoreMealType.Snack,
        _ => throw new ArgumentOutOfRangeException(nameof(mealType))
    };

    private sealed class RecordingNotifier : INutritionBatchAndCopyChangeNotifier
    {
        public int Calls { get; private set; }
        public void NotifyNutritionChanged(Guid userId, DateOnly date) => Calls++;
    }
}
