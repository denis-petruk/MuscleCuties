using MuscleCuties.Core.Domain.Progress;
using MuscleCuties.UseCases.Features.Progress.BodyMeasurements;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class BodyMeasurementUseCaseTests
{
    private const double InitialWaistCentimeters = 72.25;
    private const double CorrectedWaistCentimeters = 73.5;
    private static readonly DateOnly Today = new(2026, 10, 20);
    private static readonly DateOnly Yesterday = Today.AddDays(-1);

    [Fact]
    public async Task CreatePassesAuthenticatedOwnerAndReturnsPlainDto()
    {
        var owner = Guid.NewGuid();
        var command = Guid.NewGuid();
        var id = Guid.NewGuid();
        var store = new FakeStore();
        var useCase = new CreateBodyMeasurementUseCase(new FakeSession(owner), store);

        var output = await useCase.ExecuteAsync(Create(id, command));

        Assert.Equal(owner, store.LastOwnerId);
        Assert.Equal(command, store.LastCommandId);
        Assert.Equal(id, output.Id);
        Assert.Equal(BodyMeasurementCodes.Waist, output.Site);
        Assert.Equal(BodyMeasurementCodes.Centimeters, output.EnteredUnit);
        Assert.Equal((decimal)InitialWaistCentimeters, output.ValueCentimeters);
        Assert.Equal(BodyMeasurement.InitialVersion, output.Version);
    }

    [Fact]
    public async Task InvalidSideAndFutureDateNeverReachStore()
    {
        var store = new FakeStore();
        var useCase = new CreateBodyMeasurementUseCase(new FakeSession(Guid.NewGuid()), store);
        var valid = Create(Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(valid with
        {
            Site = BodyMeasurementCodes.Calf,
            Side = BodyMeasurementCodes.NoSide
        }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => useCase.ExecuteAsync(valid with
        {
            LocalDate = Today.AddDays(1)
        }));
        Assert.Equal(0, store.WriteCount);
    }

    [Fact]
    public async Task UnauthenticatedSessionAndCancelledRequestDoNotTouchStore()
    {
        var store = new FakeStore();
        var unauthenticated = new CreateBodyMeasurementUseCase(new FakeSession(Guid.Empty), store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            unauthenticated.ExecuteAsync(Create(Guid.NewGuid(), Guid.NewGuid())));

        var authenticated = new CreateBodyMeasurementUseCase(new FakeSession(Guid.NewGuid()), store);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            authenticated.ExecuteAsync(Create(Guid.NewGuid(), Guid.NewGuid()), cancellation.Token));
        Assert.Equal(0, store.WriteCount);
    }

    [Fact]
    public async Task CorrectUsesOwnerFilteredLoadAndReturnsUpdatedVersion()
    {
        var owner = Guid.NewGuid();
        var otherOwner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var store = new FakeStore();
        await new CreateBodyMeasurementUseCase(new FakeSession(owner), store)
            .ExecuteAsync(Create(id, Guid.NewGuid()));
        var correction = new CorrectBodyMeasurementInputDto(id, Guid.NewGuid(),
            BodyMeasurement.InitialVersion, BodyMeasurementCodes.Waist, BodyMeasurementCodes.NoSide,
            Yesterday, CorrectedWaistCentimeters, BodyMeasurementCodes.Centimeters);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new CorrectBodyMeasurementUseCase(new FakeSession(otherOwner), store).ExecuteAsync(correction));
        var output = await new CorrectBodyMeasurementUseCase(new FakeSession(owner), store)
            .ExecuteAsync(correction);

        Assert.Equal(owner, store.LastOwnerId);
        Assert.Equal((decimal)CorrectedWaistCentimeters, output.ValueCentimeters);
        Assert.Equal(BodyMeasurement.InitialVersion + 1, output.Version);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CorrectBodyMeasurementUseCase(new FakeSession(owner), store).ExecuteAsync(correction));
    }

    [Fact]
    public async Task HistoryIsEmptyForAnotherOwnerAndForwardsCancellation()
    {
        var owner = Guid.NewGuid();
        var otherOwner = Guid.NewGuid();
        var store = new FakeStore();
        await new CreateBodyMeasurementUseCase(new FakeSession(owner), store)
            .ExecuteAsync(Create(Guid.NewGuid(), Guid.NewGuid()));
        var useCase = new ReadBodyMeasurementHistoryUseCase(new FakeSession(otherOwner), store);
        using var cancellation = new CancellationTokenSource();

        var history = await useCase.ExecuteAsync(new BodyMeasurementHistoryInputDto(Yesterday, Today),
            cancellation.Token);

        Assert.Empty(history.Readings);
        Assert.Equal(otherOwner, store.LastOwnerId);
        Assert.Equal(cancellation.Token, store.LastCancellationToken);
    }

    [Fact]
    public async Task DeleteRequiresCurrentVersionAndHidesReadingFromHistory()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var store = new FakeStore();
        await new CreateBodyMeasurementUseCase(new FakeSession(owner), store)
            .ExecuteAsync(Create(id, Guid.NewGuid()));
        var delete = new DeleteBodyMeasurementUseCase(new FakeSession(owner), store);

        await Assert.ThrowsAsync<InvalidOperationException>(() => delete.ExecuteAsync(
            new DeleteBodyMeasurementInputDto(id, Guid.NewGuid(), BodyMeasurement.InitialVersion - 1)));
        await delete.ExecuteAsync(new DeleteBodyMeasurementInputDto(id, Guid.NewGuid(),
            BodyMeasurement.InitialVersion));
        var history = await new ReadBodyMeasurementHistoryUseCase(new FakeSession(owner), store)
            .ExecuteAsync(new BodyMeasurementHistoryInputDto(Yesterday, Today));

        Assert.Empty(history.Readings);
    }

    [Fact]
    public async Task CorrectRetryReturnsPriorAcknowledgementBeforeStaleVersionCheck()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var store = new FakeStore();
        await new CreateBodyMeasurementUseCase(new FakeSession(owner), store)
            .ExecuteAsync(Create(id, Guid.NewGuid()));
        var correction = new CorrectBodyMeasurementInputDto(id, Guid.NewGuid(),
            BodyMeasurement.InitialVersion, BodyMeasurementCodes.Waist, BodyMeasurementCodes.NoSide,
            Yesterday, CorrectedWaistCentimeters, BodyMeasurementCodes.Centimeters);
        var useCase = new CorrectBodyMeasurementUseCase(new FakeSession(owner), store);
        var first = await useCase.ExecuteAsync(correction);
        var writesAfterFirst = store.WriteCount;
        store.PriorCorrection = first;

        var retried = await useCase.ExecuteAsync(correction);

        Assert.Equal(first, retried);
        Assert.Equal(writesAfterFirst, store.WriteCount);
    }

    private static CreateBodyMeasurementInputDto Create(Guid id, Guid commandId) => new(
        id, commandId, BodyMeasurementCodes.Waist, BodyMeasurementCodes.NoSide,
        Yesterday, InitialWaistCentimeters, BodyMeasurementCodes.Centimeters);

    private sealed record FakeSession(Guid CurrentUserId) : IBodyMeasurementSession
    {
        public DateOnly Today => BodyMeasurementUseCaseTests.Today;
    }

    private sealed class FakeStore : IBodyMeasurementStore
    {
        private readonly Dictionary<Guid, (Guid Owner, BodyMeasurement Measurement)> _readings = [];
        public Guid LastOwnerId { get; private set; }
        public Guid LastCommandId { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }
        public int WriteCount { get; private set; }
        public BodyMeasurementOutputDto? PriorCorrection { get; set; }

        public Task<BodyMeasurementOutputDto?> TryReplayCreateAsync(Guid ownerId,
            CreateBodyMeasurementInputDto input, CancellationToken cancellationToken) => Task.FromResult<BodyMeasurementOutputDto?>(null);

        public Task<BodyMeasurementOutputDto?> TryReplayCorrectionAsync(Guid ownerId,
            CorrectBodyMeasurementInputDto input, CancellationToken cancellationToken) =>
            Task.FromResult(PriorCorrection);

        public Task<bool> WasDeletionAppliedAsync(Guid ownerId,
            DeleteBodyMeasurementInputDto input, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<BodyMeasurement> CreateAsync(Guid ownerId, Guid commandId,
            BodyMeasurement measurement, CancellationToken cancellationToken)
        {
            Record(ownerId, commandId, cancellationToken);
            if (_readings.Values.Any(row => row.Owner == ownerId &&
                                             row.Measurement.OccupiesSameDailySlotAs(measurement)))
                throw new InvalidOperationException("Duplicate daily slot.");
            _readings.Add(measurement.Id, (ownerId, measurement));
            WriteCount++;
            return Task.FromResult(Clone(measurement));
        }

        public Task<BodyMeasurement?> LoadAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
        {
            LastOwnerId = ownerId;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(_readings.TryGetValue(id, out var row) && row.Owner == ownerId
                ? Clone(row.Measurement)
                : null);
        }

        public Task<BodyMeasurement> CorrectAsync(Guid ownerId, Guid commandId,
            BodyMeasurement measurement, int expectedVersion, CancellationToken cancellationToken)
        {
            Record(ownerId, commandId, cancellationToken);
            if (!_readings.TryGetValue(measurement.Id, out var row) || row.Owner != ownerId ||
                row.Measurement.Version != expectedVersion)
                throw new InvalidOperationException("Concurrent correction.");
            _readings[measurement.Id] = (ownerId, Clone(measurement));
            WriteCount++;
            return Task.FromResult(Clone(measurement));
        }

        public Task DeleteAsync(Guid ownerId, Guid commandId, BodyMeasurement measurement,
            int expectedVersion, CancellationToken cancellationToken)
        {
            Record(ownerId, commandId, cancellationToken);
            if (!_readings.TryGetValue(measurement.Id, out var row) || row.Owner != ownerId ||
                row.Measurement.Version != expectedVersion)
                throw new InvalidOperationException("Concurrent deletion.");
            _readings[measurement.Id] = (ownerId, Clone(measurement));
            WriteCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<BodyMeasurement>> LoadHistoryAsync(Guid ownerId, DateOnly from,
            DateOnly through, CancellationToken cancellationToken)
        {
            LastOwnerId = ownerId;
            LastCancellationToken = cancellationToken;
            IReadOnlyList<BodyMeasurement> result = _readings.Values
                .Where(row => row.Owner == ownerId && !row.Measurement.IsDeleted &&
                              row.Measurement.LocalDate >= from &&
                              row.Measurement.LocalDate <= through)
                .OrderBy(row => row.Measurement.LocalDate)
                .ThenBy(row => row.Measurement.Site)
                .ThenBy(row => row.Measurement.Side)
                .ThenBy(row => row.Measurement.Id)
                .Select(row => Clone(row.Measurement))
                .ToArray();
            return Task.FromResult(result);
        }

        private void Record(Guid ownerId, Guid commandId, CancellationToken cancellationToken)
        {
            LastOwnerId = ownerId;
            LastCommandId = commandId;
            LastCancellationToken = cancellationToken;
        }

        private static BodyMeasurement Clone(BodyMeasurement measurement) => BodyMeasurement.Restore(
            measurement.Id, measurement.Site, measurement.Side, measurement.LocalDate,
            measurement.EnteredValue, measurement.EnteredUnit, measurement.Version,
            measurement.IsDeleted, Today);
    }
}
