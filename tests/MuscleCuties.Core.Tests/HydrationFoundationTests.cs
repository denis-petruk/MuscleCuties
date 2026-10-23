using MuscleCuties.Core.Domain.Hydration;
using MuscleCuties.UseCases.Features.Hydration;
using MuscleCuties.UseCases.Features.Hydration.Contracts;

namespace MuscleCuties.Core.Tests;

public sealed class HydrationFoundationTests
{
    private const int CupMillilitres = 250;
    private const int BottleMillilitres = 750;
    private const double FractionalMillilitres = 1.5;
    private const double CupLitres = 0.25;
    private const int MorningHour = 9;
    private const int EveningHour = 22;
    private const int WakeHour = 7;
    private const int AfternoonHour = 15;
    private const int MiddayHour = 12;
    private const int NegativeVolume = -1;
    private const string InitialToken = "synthetic-initial-version";
    private const string NextToken = "synthetic-next-version";
    private static readonly Guid Account = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherAccount = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CupId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid BottleId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly TimeOnly Morning = new(MorningHour, 0);
    private static readonly TimeOnly QuietStart = new(EveningHour, 0);
    private static readonly TimeOnly QuietEnd = new(WakeHour, 0);

    [Fact]
    public void PortionRejectsInvalidOrUnrepresentableVolumes()
    {
        double[] invalid = [0, NegativeVolume, FractionalMillilitres, double.NaN, double.PositiveInfinity,
            double.NegativeInfinity, double.MaxValue, int.MaxValue, WaterPortion.MaximumMillilitres + 1];
        foreach (var value in invalid)
            Assert.Throws<ArgumentOutOfRangeException>(() => WaterPortion.FromMillilitres(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => WaterPortion.FromLitres(double.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => WaterPortion.FromLitres(double.NaN));
        Assert.Equal(CupMillilitres, WaterPortion.FromLitres(CupLitres).Millilitres);
        Assert.Equal(WaterPortion.MinimumMillilitres, WaterPortion.FromMillilitres(WaterPortion.MinimumMillilitres).Millilitres);
        Assert.Equal(WaterPortion.MaximumMillilitres, WaterPortion.FromMillilitres(WaterPortion.MaximumMillilitres).Millilitres);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PresetRejectsBlankLabels(string? label) =>
        Assert.Throws<ArgumentException>(() => WaterPreset.Create(CupId, label!, CupMillilitres));

    [Fact]
    public void CatalogRejectsInvalidIdentityDuplicateLabelsAndExcessiveBounds()
    {
        Assert.Throws<ArgumentException>(() => WaterPreset.Create(Guid.Empty, "Cup", CupMillilitres));
        Assert.Throws<ArgumentException>(() => WaterPreset.Create(CupId, new string('a', WaterPreset.MaximumLabelLength + 1), CupMillilitres));
        Assert.Throws<ArgumentException>(() => WaterPresetCatalog.Restore(Guid.Empty, []));
        Assert.Throws<ArgumentException>(() => Catalog().Add(WaterPreset.Create(BottleId, " cup ", BottleMillilitres)));
        Assert.Throws<ArgumentException>(() => Catalog().Add(WaterPreset.Create(CupId, "Bottle", BottleMillilitres)));
        Assert.Throws<ArgumentException>(() => WaterPresetCatalog.Restore(Account, [null!]));
        var full = WaterPresetCatalog.Restore(Account, Enumerable.Range(0, WaterPresetCatalog.MaximumPresetCount)
            .Select(index => WaterPreset.Create(Guid.NewGuid(), $"Container {index}", CupMillilitres)));
        Assert.Throws<ArgumentException>(() => full.Add(WaterPreset.Create(CupId, "Extra", CupMillilitres)));
    }

    [Fact]
    public void UpdateReorderDeletePreserveIdsAndOriginalSnapshot()
    {
        var original = Catalog().Add(WaterPreset.Create(BottleId, "Bottle", BottleMillilitres));
        var updated = original.Update(CupId, " Travel cup ", BottleMillilitres).Reorder([BottleId, CupId]);
        Assert.Equal(new[] { BottleId, CupId }, updated.Presets.Select(p => p.Id));
        Assert.Equal("Travel cup", updated.Presets[^1].Label);
        Assert.Equal(BottleMillilitres, updated.Presets[^1].Portion.Millilitres);
        Assert.Equal("Cup", original.Presets[0].Label);
        Assert.Equal(CupId, Assert.Single(updated.Delete(BottleId).Presets).Id);
        Assert.Throws<ArgumentException>(() => original.Update(CupId, "bottle", CupMillilitres));
        Assert.Throws<ArgumentException>(() => original.Reorder([CupId, CupId]));
        Assert.Throws<ArgumentException>(() => original.Reorder([CupId]));
        Assert.Throws<ArgumentException>(() => original.Reorder([CupId, Guid.NewGuid()]));
        Assert.Throws<KeyNotFoundException>(() => original.Delete(Guid.NewGuid()));
        Assert.Throws<KeyNotFoundException>(() => original.Update(Guid.NewGuid(), "Missing", CupMillilitres));
    }

    [Fact]
    public async Task UseCasesCreateUpdateReorderDeleteAndListPlainDtos()
    {
        var store = new MemoryPresetStore();
        var first = await new CreateWaterPresetUseCase(store).ExecuteAsync(new(Account, "Cup", CupMillilitres, Guid.NewGuid(), InitialToken));
        var id = Assert.Single(first.Presets).Id;
        Assert.NotEqual(Guid.Empty, id);
        var second = await new CreateWaterPresetUseCase(store).ExecuteAsync(new(Account, "Bottle", BottleMillilitres, Guid.NewGuid(), first.Version));
        var bottleId = second.Presets[^1].Id;
        var updated = await new UpdateWaterPresetUseCase(store).ExecuteAsync(new(Account, id, "Travel cup", BottleMillilitres, Guid.NewGuid(), second.Version));
        var reordered = await new ReorderWaterPresetsUseCase(store).ExecuteAsync(new(Account, [bottleId, id], Guid.NewGuid(), updated.Version));
        Assert.Equal(new[] { bottleId, id }, reordered.Presets.Select(p => p.Id));
        await new DeleteWaterPresetUseCase(store).ExecuteAsync(new(Account, bottleId, Guid.NewGuid(), reordered.Version));
        var listed = await new GetWaterPresetsUseCase(store).ExecuteAsync(new(Account));
        Assert.Equal(id, Assert.Single(listed.Presets).Id);
        Assert.Equal("Travel cup", listed.Presets.Single().Label);
        Assert.Empty((await new GetWaterPresetsUseCase(store).ExecuteAsync(new(OtherAccount))).Presets);
        Assert.Equal(NextToken, store.LastExpectedToken);
    }

    [Fact]
    public async Task InvalidEditConflictAndFailedWriteDoNotReturnFabricatedSuccess()
    {
        var store = new MemoryPresetStore();
        var useCase = new CreateWaterPresetUseCase(store);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => useCase.ExecuteAsync(new(Account, "Cup", double.NaN, Guid.NewGuid(), InitialToken)));
        Assert.False(store.WriteAttempted);
        store.Conflict = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(new(Account, "Cup", CupMillilitres, Guid.NewGuid(), InitialToken)));
        Assert.Empty(store.Catalogs);
        store.Conflict = false;
        store.FailWrite = true;
        await Assert.ThrowsAsync<IOException>(() => useCase.ExecuteAsync(new(Account, "Cup", CupMillilitres, Guid.NewGuid(), InitialToken)));
        Assert.Empty(store.Catalogs);
    }

    [Fact]
    public async Task CancellationAndWrongAccountPreventWritesOrDataLeak()
    {
        var store = new MemoryPresetStore();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CreateWaterPresetUseCase(store)
            .ExecuteAsync(new(Account, "Cup", CupMillilitres, Guid.NewGuid(), InitialToken), cancellation.Token));
        Assert.False(store.ReadAttempted);
        store.ReturnOtherAccount = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => new GetWaterPresetsUseCase(store).ExecuteAsync(new(Account)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CreateWaterPresetUseCase(store).ExecuteAsync(new(Account, "Cup", CupMillilitres, Guid.NewGuid(), InitialToken)));
        Assert.False(store.WriteAttempted);
    }

    [Theory]
    [InlineData(WaterReminderPermissionDto.Unknown, false)]
    [InlineData(WaterReminderPermissionDto.Denied, false)]
    [InlineData(WaterReminderPermissionDto.Granted, true)]
    public async Task ReminderPreviewRequiresOptInAndGrantedPermission(WaterReminderPermissionDto permission, bool eligible)
    {
        var useCase = new PreviewWaterReminderUseCase();
        var disabled = await useCase.ExecuteAsync(new(false, null, null, null, permission));
        Assert.False(disabled.OptedIn);
        Assert.False(disabled.CanRequestScheduling);
        Assert.Empty(disabled.LocalTimes);
        var enabled = await useCase.ExecuteAsync(new(true, [Morning], QuietStart, QuietEnd, permission));
        Assert.True(enabled.OptedIn);
        Assert.Equal(eligible, enabled.CanRequestScheduling);
        Assert.Equal(permission, enabled.Permission);
    }

    [Fact]
    public void ReminderRejectsMissingQuietHoursDuplicateTimesAndQuietWindowTimes()
    {
        Assert.Throws<ArgumentException>(() => FixedWaterReminderSettings.Enable([Morning], null, null));
        Assert.Throws<ArgumentNullException>(() => FixedWaterReminderSettings.Enable(null, QuietStart, QuietEnd));
        Assert.Throws<ArgumentException>(() => FixedWaterReminderSettings.Enable([], QuietStart, QuietEnd));
        Assert.Throws<ArgumentException>(() => FixedWaterReminderSettings.Enable([Morning, Morning], QuietStart, QuietEnd));
        Assert.Throws<ArgumentException>(() => FixedWaterReminderSettings.Enable([QuietStart], QuietStart, QuietEnd));
        Assert.Throws<ArgumentException>(() => FixedWaterReminderSettings.Enable([TimeOnly.MinValue], QuietStart, QuietEnd));
        Assert.Throws<ArgumentException>(() => FixedWaterReminderSettings.Enable([Morning], QuietStart, QuietStart));
        Assert.Throws<ArgumentException>(() => FixedWaterReminderSettings.Enable([new(MiddayHour, 0)],
            new(MorningHour, 0), new(AfternoonHour, 0)));
        Assert.Single(FixedWaterReminderSettings.Enable([QuietEnd], QuietStart, QuietEnd).LocalTimes);
        var tooMany = Enumerable.Range(0, FixedWaterReminderSettings.MaximumReminderTimes + 1)
            .Select(minute => new TimeOnly(MiddayHour, minute));
        Assert.Throws<ArgumentException>(() => FixedWaterReminderSettings.Enable(tooMany, QuietStart, QuietEnd));
    }

    [Fact]
    public async Task ReminderNormalizesUnrecognizedPermissionAndObservesCancellation()
    {
        var useCase = new PreviewWaterReminderUseCase();
        var input = new PreviewWaterReminderInputDto(true, [Morning], QuietStart, QuietEnd,
            (WaterReminderPermissionDto)int.MaxValue);
        var output = await useCase.ExecuteAsync(input);
        Assert.Equal(WaterReminderPermissionDto.Unknown, output.Permission);
        Assert.False(output.CanRequestScheduling);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => useCase.ExecuteAsync(input, cancellation.Token));
    }

    private static WaterPresetCatalog Catalog() => WaterPresetCatalog.Restore(Account, [WaterPreset.Create(CupId, "Cup", CupMillilitres)]);

    private sealed class MemoryPresetStore : IWaterPresetStore
    {
        public Dictionary<Guid, StoredWaterPresetCatalog> Catalogs { get; } = [];
        public bool ReadAttempted { get; private set; }
        public bool WriteAttempted { get; private set; }
        public bool Conflict { get; set; }
        public bool FailWrite { get; set; }
        public bool ReturnOtherAccount { get; set; }
        public string? LastExpectedToken { get; private set; }

        public Task<StoredWaterPresetCatalog> ReadAsync(Guid accountId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadAttempted = true;
            return Task.FromResult(ReturnOtherAccount
                ? new StoredWaterPresetCatalog(WaterPresetState.Restore(WaterPresetCatalog.Restore(OtherAccount, []), []), InitialToken)
                : Catalogs.GetValueOrDefault(accountId) ?? new StoredWaterPresetCatalog(WaterPresetState.Restore(WaterPresetCatalog.Restore(accountId, []), []), InitialToken));
        }

        public Task<StoredWaterPresetCatalog?> TryReplaceAsync(WaterPresetState state, string? expectedConcurrencyToken, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteAttempted = true;
            LastExpectedToken = expectedConcurrencyToken;
            if (FailWrite) throw new IOException("Synthetic persistence failure.");
            if (Conflict) return Task.FromResult<StoredWaterPresetCatalog?>(null);
            var committed = new StoredWaterPresetCatalog(state, NextToken);
            Catalogs[state.Catalog.AccountId] = committed;
            return Task.FromResult<StoredWaterPresetCatalog?>(committed);
        }
    }
}
