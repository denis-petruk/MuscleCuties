using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.BatchAndCopy;
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
            new PreviewNutritionCopyRequest(SourceDate, TargetDate, MealType.Breakfast, DiaryCopyMode.Merge));

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
        var source = new[] { Food("source", MealType.Breakfast, 120m) };
        var target = new[]
        {
            Food("breakfast", MealType.Breakfast, 90m),
            Food("dinner", MealType.Dinner, 300m)
        };

        var meal = DiaryCopyProjection.Create(source, target, MealType.Breakfast, DiaryCopyMode.Overwrite);
        var day = DiaryCopyProjection.Create(source, target, null, DiaryCopyMode.Overwrite);

        Assert.Equal(2, meal.ResultingTargetTotals.Count);
        Assert.Equal(420m, meal.ResultingTargetTotals.Calories);
        Assert.Contains(meal.ResultingTargetItems, item => item.Identity == "dinner");
        Assert.Single(day.ResultingTargetItems);
        Assert.Equal(120m, day.ResultingTargetTotals.Calories);
    }

    [Fact]
    public void MalformedDuplicateIdentityCannotInflatePreview()
    {
        var repeated = Food("same-entry", MealType.Breakfast, 120m);

        Assert.Throws<ArgumentException>(() => DiaryCopyProjection.Create(
            [repeated, repeated], [], null, DiaryCopyMode.Merge));
    }

    [Fact]
    public async Task StaleTargetRejectsCommitWithoutWriteOrNotification()
    {
        var store = SeededStore();
        var notifier = new RecordingNotifier();
        var useCase = new NutritionCopyPreviewUseCase(store, notifier);
        var preview = await useCase.PreviewAsync(Owner,
            new PreviewNutritionCopyRequest(SourceDate, TargetDate, null, DiaryCopyMode.Overwrite));
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
            new PreviewNutritionCopyRequest(SourceDate, TargetDate, null, DiaryCopyMode.Merge));
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
            new PreviewNutritionCopyRequest(SourceDate, TargetDate, null, DiaryCopyMode.Merge)));
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.CommitAsync(Guid.Empty,
            new CommitPreviewedNutritionCopyRequest(CopyKey, SourceDate, TargetDate, null,
                DiaryCopyMode.Merge, OriginalSourceRevision, OriginalTargetRevision)));

        Assert.Equal(0, store.CommitCalls);
    }

    [Fact]
    public async Task CommitReplaysSameKeyWithoutSecondWriteOrNotification()
    {
        var store = SeededStore();
        var notifier = new RecordingNotifier();
        var useCase = new NutritionCopyPreviewUseCase(store, notifier);
        var preview = await useCase.PreviewAsync(Owner,
            new PreviewNutritionCopyRequest(SourceDate, TargetDate, null, DiaryCopyMode.Merge));
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
            new PreviewNutritionCopyRequest(SourceDate, TargetDate, null, DiaryCopyMode.Merge));
        var command = Commit(preview);
        await useCase.CommitAsync(Owner, command);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.CommitAsync(Owner,
            command with { Mode = DiaryCopyMode.Overwrite }));
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
            new PreviewNutritionCopyRequest(SourceDate, TargetDate, null, DiaryCopyMode.Merge));

        Assert.Empty(preview.SourceItems);
        Assert.Equal(preview.ExistingTargetTotals, preview.ResultingTargetTotals);
        Assert.Equal(0, store.Writes);
    }

    private static CommitPreviewedNutritionCopyRequest Commit(PreviewNutritionCopyResponse preview) =>
        new(CopyKey, preview.SourceDate, preview.TargetDate, preview.MealType, preview.Mode,
            preview.SourceRevision, preview.TargetRevision);

    private static DiaryCopyFood Food(string identity, MealType mealType, decimal calories) =>
        new(identity, identity, mealType, calories, decimal.Zero, decimal.Zero, decimal.Zero);

    private static DiaryCopyFoodDto FoodDto(string identity, MealType mealType, decimal calories,
        string? groupIdentity = null) =>
        new(identity, identity, mealType, calories, decimal.Zero, decimal.Zero, decimal.Zero, groupIdentity);

    private static RecordingStore SeededStore() => new()
    {
        SourceItems =
        [
            FoodDto("source-a", MealType.Breakfast, 100m, "legacy-group"),
            FoodDto("source-b", MealType.Breakfast, 100m, "legacy-group")
        ],
        TargetItems =
        [
            FoodDto("target-a", MealType.Breakfast, 200m),
            FoodDto("target-dinner", MealType.Dinner, 300m)
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
                SourceItems.Select(item => Food(item.Identity, item.MealType, item.Calories)).ToArray(),
                TargetItems.Select(item => Food(item.Identity, item.MealType, item.Calories)).ToArray(),
                request.MealType,
                request.Mode);
            Writes++;
            var response = new CopyNutritionDiaryResponse(Guid.NewGuid(), request.TargetDate,
                projection.SourceTotals.Count, [], false);
            _replays.Add(request.IdempotencyKey, (request, response));
            return Task.FromResult(response);
        }
    }

    private sealed class RecordingNotifier : INutritionBatchAndCopyChangeNotifier
    {
        public int Calls { get; private set; }
        public void NotifyNutritionChanged(Guid userId, DateOnly date) => Calls++;
    }
}
