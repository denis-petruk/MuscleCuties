using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Barcode;
using MuscleCuties.UseCases.Features.Nutrition.Barcode.Contracts;

namespace MuscleCuties.Core.Tests;

public sealed class BarcodeLookupFoundationTests
{
    private const string SyntheticUpcA = "012345000065";
    private const string SyntheticEan13 = "0012345000065";
    private const string SyntheticUpcE = "01234565";
    private const int SyntheticFoodId = 41;
    private const float SyntheticCalories = 120f;
    private const float SyntheticProtein = 10f;
    private const float SyntheticCarbs = 15f;
    private const float SyntheticFats = 2f;
    private static readonly Guid OwnerA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OwnerB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Theory]
    [InlineData(SyntheticUpcA, BarcodeSymbologyDto.Auto)]
    [InlineData(SyntheticEan13, BarcodeSymbologyDto.Ean13)]
    [InlineData(SyntheticUpcE, BarcodeSymbologyDto.UpcE)]
    public async Task Valid_lookup_forwards_canonical_keys_owner_and_token_and_returns_plain_food_dto(
        string text, BarcodeSymbologyDto symbology)
    {
        using var cancellation = new CancellationTokenSource();
        var store = new RecordingStore { Candidate = Food(SyntheticUpcA) };

        var result = await new LookupBarcodeUseCase(store).ExecuteAsync(
            new BarcodeLookupRequest(text, symbology), OwnerA, cancellation.Token);

        Assert.True(result.IsValidBarcode);
        Assert.True(result.IsFound);
        Assert.Equal(SyntheticEan13, result.NormalizedBarcode);
        Assert.Equal(SyntheticFoodId, Assert.IsType<MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts.FoodCatalogItemDto>(result.FoodItem).Id);
        Assert.Equal(OwnerA, store.Owner);
        Assert.Equal(cancellation.Token, store.Token);
        Assert.Equal(1, store.Calls);
        var values = Assert.IsAssignableFrom<IReadOnlyList<string>>(store.Values);
        Assert.Equal(SyntheticEan13, values.First());
        Assert.Contains(SyntheticUpcA, values);
    }

    [Fact]
    public async Task Every_owner_is_forwarded_even_when_reusing_use_case()
    {
        var store = new RecordingStore();
        var useCase = new LookupBarcodeUseCase(store);
        var request = new BarcodeLookupRequest(SyntheticUpcA);

        await useCase.ExecuteAsync(request, OwnerA);
        Assert.Equal(OwnerA, store.Owner);
        await useCase.ExecuteAsync(request, OwnerB);
        Assert.Equal(OwnerB, store.Owner);
        await useCase.ExecuteAsync(request);
        Assert.Null(store.Owner);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("012345000066")]
    public async Task Invalid_request_never_accesses_store(string? text)
    {
        var store = new RecordingStore();
        var request = text is null ? null : new BarcodeLookupRequest(text);

        var result = await new LookupBarcodeUseCase(store).ExecuteAsync(request!);

        Assert.False(result.IsValidBarcode);
        Assert.False(result.IsFound);
        Assert.Null(result.FoodItem);
        Assert.Equal(0, store.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("X0012345000065X")]
    [InlineData("00123450000650")]
    [InlineData("001234500006")]
    public async Task Adapter_collision_or_missing_barcode_cannot_prefill_unrelated_food(string? storedValue)
    {
        var store = new RecordingStore { Candidate = Food(storedValue) };

        var result = await new LookupBarcodeUseCase(store).ExecuteAsync(new BarcodeLookupRequest(SyntheticUpcA));

        Assert.True(result.IsValidBarcode);
        Assert.False(result.IsFound);
        Assert.Null(result.FoodItem);
    }

    [Fact]
    public async Task Missing_food_is_a_valid_barcode_with_truthful_not_found_result()
    {
        var result = await new LookupBarcodeUseCase(new RecordingStore()).ExecuteAsync(new BarcodeLookupRequest(SyntheticUpcA));

        Assert.True(result.IsValidBarcode);
        Assert.False(result.IsFound);
        Assert.Equal(SyntheticEan13, result.NormalizedBarcode);
        Assert.Null(result.FoodItem);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Unknown_nutrition_stays_unknown_and_invalid_nutrition_is_not_displayed()
    {
        var store = new RecordingStore { Candidate = Food(SyntheticUpcA) with { Calories = null } };
        var useCase = new LookupBarcodeUseCase(store);
        var request = new BarcodeLookupRequest(SyntheticUpcA);

        var unknown = await useCase.ExecuteAsync(request);
        Assert.True(unknown.IsFound);
        Assert.Null(unknown.FoodItem!.Calories);
        store.Candidate = store.Candidate with { Calories = float.NaN };
        Assert.False((await useCase.ExecuteAsync(request)).IsFound);
    }

    [Fact]
    public async Task Offline_failure_is_not_reported_as_success_or_catalog_absence()
    {
        var offline = new HttpRequestException("Synthetic first-party backend unavailable.");
        var store = new RecordingStore { Failure = offline };

        var actual = await Assert.ThrowsAsync<HttpRequestException>(() =>
            new LookupBarcodeUseCase(store).ExecuteAsync(new BarcodeLookupRequest(SyntheticUpcA)));

        Assert.Same(offline, actual);
    }

    [Fact]
    public async Task Store_cancellation_propagates_without_a_food_result()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new LookupBarcodeUseCase(new RecordingStore()).ExecuteAsync(
                new BarcodeLookupRequest(SyntheticUpcA), OwnerA, cancellation.Token));
    }

    private static FoodCatalogCandidate Food(string? barcode) => new(
        SyntheticFoodId, "Synthetic catalog fixture", SyntheticCalories, SyntheticProtein, SyntheticCarbs,
        SyntheticFats, false, null, null, null, null, barcode, null, null, null, null);

    [Fact]
    public async Task Explicit_owner_cannot_fall_back_to_legacy_store()
    {
        var store = new LegacyStore();
        var useCase = new LookupBarcodeUseCase(store);
        var request = new BarcodeLookupRequest(SyntheticUpcA);

        await Assert.ThrowsAsync<NotSupportedException>(() => useCase.ExecuteAsync(request, OwnerA));
        await Assert.ThrowsAsync<NotSupportedException>(() => useCase.ExecuteAsync(request, OwnerB));
        await Assert.ThrowsAsync<NotSupportedException>(() => useCase.ExecuteAsync(request, Guid.Empty));
        Assert.False(store.Called);
    }

    [Fact]
    public async Task Null_owner_retains_explicit_legacy_session_contract_and_token()
    {
        var store = new LegacyStore();
        using var cancellation = new CancellationTokenSource();

        var result = await new LookupBarcodeUseCase(store).ExecuteAsync(
            new BarcodeLookupRequest(SyntheticUpcA), cancellation.Token);

        Assert.True(result.IsFound);
        Assert.True(store.Called);
        Assert.Equal(cancellation.Token, store.Token);
    }

    [Fact]
    public async Task Explicit_owner_cannot_fall_back_to_legacy_use_case()
    {
        var legacy = new LegacyUseCase();
        ILookupBarcodeUseCase useCase = legacy;

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            useCase.ExecuteAsync(new BarcodeLookupRequest(SyntheticUpcA), OwnerA));

        Assert.False(legacy.Called);
    }

    [Fact]
    public async Task Null_owner_preserves_legacy_use_case_contract()
    {
        var legacy = new LegacyUseCase();
        ILookupBarcodeUseCase useCase = legacy;
        using var cancellation = new CancellationTokenSource();

        await useCase.ExecuteAsync(new BarcodeLookupRequest(SyntheticUpcA), null, cancellation.Token);

        Assert.True(legacy.Called);
        Assert.Equal(cancellation.Token, legacy.Token);
    }

    private sealed class LegacyStore : IBarcodeFoodLookupStore
    {
        public bool Called { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<FoodCatalogCandidate?> FindFoodByBarcodeAsync(
            IReadOnlyList<string> exactValues, CancellationToken cancellationToken = default)
        {
            Called = true;
            Token = cancellationToken;
            return Task.FromResult<FoodCatalogCandidate?>(Food(SyntheticUpcA));
        }
    }

    private sealed class LegacyUseCase : ILookupBarcodeUseCase
    {
        public bool Called { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<BarcodeLookupResult> ExecuteAsync(
            BarcodeLookupRequest request, CancellationToken cancellationToken = default)
        {
            Called = true;
            Token = cancellationToken;
            return Task.FromResult(new BarcodeLookupResult(false, true, SyntheticEan13, null, null));
        }
    }

    private sealed class RecordingStore : IBarcodeFoodLookupStore
    {
        public FoodCatalogCandidate? Candidate { get; set; }
        public Exception? Failure { get; init; }
        public IReadOnlyList<string>? Values { get; private set; }
        public Guid? Owner { get; private set; }
        public CancellationToken Token { get; private set; }
        public int Calls { get; private set; }

        public Task<FoodCatalogCandidate?> FindFoodByBarcodeAsync(
            IReadOnlyList<string> exactValues, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Lookup must use the owner-aware overload.");

        public Task<FoodCatalogCandidate?> FindFoodByBarcodeAsync(
            IReadOnlyList<string> exactValues, Guid? ownerUserId, CancellationToken cancellationToken = default)
        {
            Values = exactValues.ToArray();
            Owner = ownerUserId;
            Token = cancellationToken;
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Failure is null
                ? Task.FromResult(Candidate)
                : Task.FromException<FoodCatalogCandidate?>(Failure);
        }
    }
}
