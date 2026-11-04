using MuscleCuties.Core.Domain.Hydration;
using MuscleCuties.UseCases.Features.Hydration;
using MuscleCuties.UseCases.Features.Hydration.Contracts;

namespace MuscleCuties.Core.Tests;

public sealed class HydrationPersistenceContractTests
{
    private const int FixtureYear = 2026;
    private const int FixtureMonth = 10;
    private const int FixtureDay = 20;
    private const int MorningHour = 9;
    private const int QuietStartHour = 22;
    private const int QuietEndHour = 7;
    private static readonly Guid Account = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherAccount = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateOnly Date = new(FixtureYear, FixtureMonth, FixtureDay);
    private static readonly TimeOnly Morning = new(MorningHour, 0);
    private static readonly TimeOnly QuietStart = new(QuietStartHour, 0);
    private static readonly TimeOnly QuietEnd = new(QuietEndHour, 0);

    [Fact]
    public async Task DefaultInitializationIsAccountScopedStableAndDoesNotResurrectDeletedPresets()
    {
        var store = new PresetStore();
        var initialize = new InitializeWaterPresetsUseCase(store);
        var first = await initialize.ExecuteAsync(new(Account));
        var repeated = await initialize.ExecuteAsync(new(Account));
        Assert.Equal(first.Presets, repeated.Presets);
        Assert.Equal(new[] { DefaultWaterPresets.SmallMillilitres, DefaultWaterPresets.MediumMillilitres,
            DefaultWaterPresets.LargeMillilitres }, first.Presets.Select(p => p.Millilitres));
        var current = await new UpdateWaterPresetUseCase(store).ExecuteAsync(new(Account, first.Presets[0].Id, "My bottle", DefaultWaterPresets.LargeMillilitres, Guid.NewGuid(), first.Version));
        Assert.Equal("My bottle", (await initialize.ExecuteAsync(new(Account))).Presets[0].Label);
        foreach (var preset in first.Presets)
            current = await new DeleteWaterPresetUseCase(store).ExecuteAsync(new(Account, preset.Id, Guid.NewGuid(), current.Version));
        Assert.Empty((await initialize.ExecuteAsync(new(Account))).Presets);
        Assert.NotEmpty((await initialize.ExecuteAsync(new(OtherAccount))).Presets);
        Assert.Empty((await initialize.ExecuteAsync(new(Account))).Presets);
    }

    [Fact]
    public async Task ExistingIntentionallyEmptyCatalogIsNotTreatedAsNeverSeeded()
    {
        var store = new PresetStore();
        var created = await new CreateWaterPresetUseCase(store).ExecuteAsync(new(Account, "Custom", DefaultWaterPresets.SmallMillilitres, Guid.NewGuid(), null));
        await new DeleteWaterPresetUseCase(store).ExecuteAsync(new(Account, created.Presets[0].Id, Guid.NewGuid(), created.Version));
        var initialize = new InitializeWaterPresetsUseCase(store);
        Assert.Empty((await initialize.ExecuteAsync(new(Account))).Presets);
        Assert.NotEmpty((await initialize.ExecuteAsync(new(OtherAccount))).Presets);
    }

    [Fact]
    public async Task PresetClientVersionAndDurableOperationIdentityHandleRetryAndCompare()
    {
        var store = new PresetStore { LoseNextResponse = true };
        var create = new CreateWaterPresetUseCase(store);
        var input = new CreateWaterPresetInputDto(Account, "Custom", DefaultWaterPresets.SmallMillilitres, Guid.NewGuid(), null);
        await Assert.ThrowsAsync<IOException>(() => create.ExecuteAsync(input));
        var retried = await create.ExecuteAsync(input);
        Assert.Equal(input.OperationId, Assert.Single(retried.Presets).Id);
        Assert.Equal(1, store.Writes);
        await Assert.ThrowsAsync<InvalidOperationException>(() => create.ExecuteAsync(input with { Millilitres = DefaultWaterPresets.LargeMillilitres }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => create.ExecuteAsync(input with { OperationId = Guid.NewGuid(), Label = "Second" }));
        var delete = new DeleteWaterPresetInputDto(Account, input.OperationId, Guid.NewGuid(), retried.Version);
        var deleted = await new DeleteWaterPresetUseCase(store).ExecuteAsync(delete);
        var repeated = await new DeleteWaterPresetUseCase(store).ExecuteAsync(delete);
        Assert.Empty(repeated.Presets);
        Assert.Equal(deleted.Version, repeated.Version);
    }

    [Fact]
    public async Task IntakeLostResponseRetryDoesNotDoubleCountAndSeparatesAccountAndDate()
    {
        var store = new IntakeStore { LoseNextResponse = true };
        var useCase = new ChangeWaterIntakeUseCase(store);
        var input = Add();
        await Assert.ThrowsAsync<IOException>(() => useCase.ExecuteAsync(input));
        var retried = await useCase.ExecuteAsync(input);
        Assert.Equal(DefaultWaterPresets.SmallMillilitres, retried.TotalMillilitres);
        Assert.Single(retried.Entries);
        Assert.Equal(1, store.Writes);
        var get = new GetWaterIntakeUseCase(store);
        Assert.Equal(0, (await get.ExecuteAsync(new(OtherAccount, Date))).TotalMillilitres);
        Assert.Equal(0, (await get.ExecuteAsync(new(Account, Date.AddDays(1)))).TotalMillilitres);
    }

    [Fact]
    public async Task IntakeCorrectionUndoRejectsStaleOrConflictingWrites()
    {
        var store = new IntakeStore();
        var useCase = new ChangeWaterIntakeUseCase(store);
        var add = Add();
        var saved = await useCase.ExecuteAsync(add);
        var correction = add with { OperationId = Guid.NewGuid(), Kind = WaterIntakeChangeKindDto.Correct,
            Millilitres = DefaultWaterPresets.LargeMillilitres, ExpectedVersion = saved.Version };
        var corrected = await useCase.ExecuteAsync(correction);
        Assert.Equal(DefaultWaterPresets.LargeMillilitres, corrected.TotalMillilitres);
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(correction with { OperationId = Guid.NewGuid() }));
        var undo = correction with { OperationId = Guid.NewGuid(), Kind = WaterIntakeChangeKindDto.Undo,
            Millilitres = 0, ExpectedVersion = corrected.Version };
        store.Conflict = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(undo));
        Assert.Equal(DefaultWaterPresets.LargeMillilitres, (await new GetWaterIntakeUseCase(store).ExecuteAsync(new(Account, Date))).TotalMillilitres);
        store.Conflict = false;
        var undone = await useCase.ExecuteAsync(undo);
        Assert.Equal(0, undone.TotalMillilitres);
        var retried = await useCase.ExecuteAsync(undo);
        Assert.Equal(undone.TotalMillilitres, retried.TotalMillilitres);
        Assert.Equal(undone.Version, retried.Version);
        Assert.Equal(undone.Entries, retried.Entries);
    }

    [Fact]
    public async Task IntakeRejectsCrossScopeResponsesAndCancellationBeforeWriting()
    {
        var store = new IntakeStore { WrongDate = true };
        var useCase = new ChangeWaterIntakeUseCase(store);
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(Add()));
        store.WrongDate = false;
        store.WrongAccount = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(Add()));
        store.WrongAccount = false;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => useCase.ExecuteAsync(Add(), cancellation.Token));
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task ReminderConfigurationIsDisabledInitiallyAccountScopedAndVersioned()
    {
        var store = new ReminderStore();
        var get = new GetWaterReminderConfigurationUseCase(store);
        var save = new SaveWaterReminderConfigurationUseCase(store);
        Assert.False((await get.ExecuteAsync(new(Account))).OptedIn);
        var enabled = await save.ExecuteAsync(Enable());
        Assert.True(enabled.OptedIn);
        Assert.Equal(new[] { Morning }, enabled.LocalTimes);
        Assert.False((await get.ExecuteAsync(new(OtherAccount))).OptedIn);
        await Assert.ThrowsAsync<InvalidOperationException>(() => save.ExecuteAsync(Enable()));
        var disabled = await save.ExecuteAsync(Enable() with { ExpectedVersion = enabled.Version, OptedIn = false });
        Assert.False(disabled.OptedIn);
        Assert.Empty(disabled.LocalTimes);
        Assert.Null(disabled.QuietStart);
    }

    [Fact]
    public async Task ReminderConfigurationRejectsInvalidQuietHoursConflictsAndFailedWrites()
    {
        var store = new ReminderStore();
        var save = new SaveWaterReminderConfigurationUseCase(store);
        await Assert.ThrowsAsync<ArgumentException>(() => save.ExecuteAsync(Enable() with { QuietStart = null }));
        Assert.Empty(store.Configurations);
        store.Conflict = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => save.ExecuteAsync(Enable()));
        Assert.Empty(store.Configurations);
        store.Conflict = false;
        store.FailWrite = true;
        await Assert.ThrowsAsync<IOException>(() => save.ExecuteAsync(Enable()));
        Assert.Empty(store.Configurations);
        store.FailWrite = false;
        store.WrongAccount = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => save.ExecuteAsync(Enable()));
        Assert.Empty(store.Configurations);
    }

    [Fact]
    public async Task ReminderLostResponseRetryAndOldEnableReplayCannotResurrectAnOptOut()
    {
        var store = new ReminderStore { LoseNextResponse = true };
        var save = new SaveWaterReminderConfigurationUseCase(store);
        var enable = Enable();
        await Assert.ThrowsAsync<IOException>(() => save.ExecuteAsync(enable));
        var retried = await save.ExecuteAsync(enable);
        Assert.True(retried.OptedIn);
        Assert.Equal(1, store.Writes);
        var disable = enable with { OperationId = Guid.NewGuid(), ExpectedVersion = retried.Version, OptedIn = false };
        var disabled = await save.ExecuteAsync(disable);
        var oldEnableRetried = await save.ExecuteAsync(enable);
        Assert.False(oldEnableRetried.OptedIn);
        Assert.Equal(disabled.Version, oldEnableRetried.Version);
        await Assert.ThrowsAsync<InvalidOperationException>(() => save.ExecuteAsync(enable with { OptedIn = false }));
    }

    private static ChangeWaterIntakeInputDto Add() => new(Account, Date, null, Guid.NewGuid(), Guid.NewGuid(),
        WaterIntakeChangeKindDto.Add, DefaultWaterPresets.SmallMillilitres);
    private static SaveWaterReminderConfigurationInputDto Enable() => new(Account, null, true, [Morning], QuietStart, QuietEnd, Guid.NewGuid());

    private sealed class PresetStore : IWaterPresetInitializationStore
    {
        private readonly HashSet<Guid> _initialized = [];
        private readonly Dictionary<Guid, StoredWaterPresetCatalog> _catalogs = [];
        public bool LoseNextResponse { get; set; }
        public int Writes { get; private set; }
        public Task<bool> TryInitializeAsync(WaterPresetCatalog defaults, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_initialized.Add(defaults.AccountId)) return Task.FromResult(false);
            if (_catalogs.ContainsKey(defaults.AccountId)) return Task.FromResult(false);
            _catalogs[defaults.AccountId] = new(WaterPresetState.Restore(defaults, []), Guid.NewGuid().ToString());
            return Task.FromResult(true);
        }
        public Task<StoredWaterPresetCatalog> ReadAsync(Guid accountId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_catalogs.GetValueOrDefault(accountId) ?? new(WaterPresetState.Restore(WaterPresetCatalog.Restore(accountId, []), []), null));
        }
        public Task<StoredWaterPresetCatalog?> TryReplaceAsync(WaterPresetState state, string? expectedConcurrencyToken, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_catalogs.GetValueOrDefault(state.Catalog.AccountId)?.ConcurrencyToken != expectedConcurrencyToken)
                return Task.FromResult<StoredWaterPresetCatalog?>(null);
            var committed = new StoredWaterPresetCatalog(state, Guid.NewGuid().ToString());
            _catalogs[state.Catalog.AccountId] = committed;
            Writes++;
            if (LoseNextResponse)
            {
                LoseNextResponse = false;
                throw new IOException("Synthetic response lost after commit.");
            }
            return Task.FromResult<StoredWaterPresetCatalog?>(committed);
        }
    }

    private sealed class IntakeStore : IWaterIntakeStore
    {
        private readonly Dictionary<(Guid, DateOnly), StoredWaterIntakeDay> _days = [];
        public bool LoseNextResponse { get; set; }
        public bool Conflict { get; set; }
        public bool WrongDate { get; set; }
        public bool WrongAccount { get; set; }
        public int Writes { get; private set; }
        public Task<StoredWaterIntakeDay> ReadAsync(Guid accountId, DateOnly date, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_days.GetValueOrDefault((accountId, date)) ?? new(
                WaterIntakeDay.Restore(WrongAccount ? OtherAccount : accountId, WrongDate ? date.AddDays(1) : date, 0, [], []), null));
        }
        public Task<StoredWaterIntakeDay?> TryReplaceAsync(WaterIntakeDay day, string? expectedVersion, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Conflict || _days.GetValueOrDefault((day.AccountId, day.Date))?.Version != expectedVersion)
                return Task.FromResult<StoredWaterIntakeDay?>(null);
            var stored = new StoredWaterIntakeDay(day, Guid.NewGuid().ToString());
            _days[(day.AccountId, day.Date)] = stored;
            Writes++;
            if (LoseNextResponse)
            {
                LoseNextResponse = false;
                throw new IOException("Synthetic response lost after commit.");
            }
            return Task.FromResult<StoredWaterIntakeDay?>(stored);
        }
    }

    private sealed class ReminderStore : IWaterReminderConfigurationStore
    {
        public Dictionary<Guid, StoredWaterReminderConfiguration> Configurations { get; } = [];
        public bool Conflict { get; set; }
        public bool FailWrite { get; set; }
        public bool WrongAccount { get; set; }
        public bool LoseNextResponse { get; set; }
        public int Writes { get; private set; }
        public Task<StoredWaterReminderConfiguration> ReadAsync(Guid accountId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Configurations.GetValueOrDefault(accountId) ?? new(
                WaterReminderConfiguration.Create(WrongAccount ? OtherAccount : accountId, false, null, null, null), null));
        }
        public Task<StoredWaterReminderConfiguration?> TryReplaceAsync(WaterReminderConfiguration configuration, string? expectedVersion, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FailWrite) throw new IOException("Synthetic persistence failure.");
            if (Conflict || Configurations.GetValueOrDefault(configuration.AccountId)?.Version != expectedVersion)
                return Task.FromResult<StoredWaterReminderConfiguration?>(null);
            var committed = new StoredWaterReminderConfiguration(configuration, Guid.NewGuid().ToString());
            Configurations[configuration.AccountId] = committed;
            Writes++;
            if (LoseNextResponse)
            {
                LoseNextResponse = false;
                throw new IOException("Synthetic response lost after commit.");
            }
            return Task.FromResult<StoredWaterReminderConfiguration?>(committed);
        }
    }
}
