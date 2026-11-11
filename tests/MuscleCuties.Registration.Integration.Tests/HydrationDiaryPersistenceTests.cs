using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MuscleCuties.Core.Domain.Hydration;
using MuscleCuties.Infrastructure.PostgreSql.Hydration;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Nutrition.Diary;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Server.Hydration;
using MuscleCuties.UseCases.Features.Hydration;
using MuscleCuties.UseCases.Features.Hydration.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

[Collection(DisposableDatabaseCollection.Name)]
public sealed class HydrationDiaryPersistenceTests(DisposableDatabaseFixture fixture)
{
    private const string LegacyTotalConflict = "hydrationLegacyTotalConflict";

    [PostgresFact]
    public async Task RichIntakeMirrorsLegacyDiaryAndKeepsOneRowOnConcurrentFirstWrite()
    {
        if (!fixture.Available)
            return;

        var connection = await fixture.CreateCopyAsync("hydration_diary");
        try
        {
            var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options;
            await using var database = new PostgresDbContext(options);
            var account = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var date = DateOnly.FromDateTime(now);
            var legacyDate = date.AddDays(-1);
            var ct = CancellationToken.None;
            await SyntheticTenantSession.SetAsync(database, account);
            database.Users.Add(new UserRecord
            {
                Id = account,
                Email = $"water-{account:N}@example.invalid",
                Username = $"water_{account.ToString("N")[..10]}",
                CredentialType = "Password",
                PasswordHash = "synthetic-hash",
                CreatedAt = now,
                UpdatedAt = now
            });
            await database.SaveChangesAsync(ct);

            await using var intakeDatabase = new PostgresDbContext(options);
            await using var diaryDatabase = new PostgresDbContext(options);
            var intakeStore = new PostgresHydrationStore(intakeDatabase, TimeProvider.System);
            var intake = new ChangeWaterIntakeUseCase(intakeStore);
            var diary = new PostgresNutritionDiaryStore(diaryDatabase, TimeProvider.System);

            var newDayEntry = new ChangeWaterIntakeInputDto(
                account, date, null, Guid.NewGuid(), Guid.NewGuid(), WaterIntakeChangeKindDto.Add, 250);
            var waterTask = intake.ExecuteAsync(newDayEntry, ct);
            var foodTask = diary.SaveManualEntryAsync(account,
                new SaveNutritionDiaryEntry(null, date, MealType.Breakfast, "Synthetic food", 1, "g", 1, 1, 0, 0, 0, now), ct);
            await Task.WhenAll(waterTask, foodTask);

            Assert.Equal(250, (await waterTask).TotalMillilitres);
            var newDay = await diary.GetDayAsync(account, date, 10, null, ct);
            Assert.Equal(250, newDay.WaterIntakeMl);
            Assert.Single(newDay.Entries);
            Assert.Equal(1, await database.DailyNutritionLogs.CountAsync(row => row.UserId == account && row.LocalDate == date, ct));
            Assert.Equal(250, (await intake.ExecuteAsync(newDayEntry, ct)).TotalMillilitres);

            await diary.UpdateMetadataAsync(account, new UpdateNutritionDiaryMetadata(legacyDate, 400, "Synthetic notes"), ct);
            var legacyEntry = newDayEntry with { Date = legacyDate, OperationId = Guid.NewGuid(), EntryId = Guid.NewGuid() };
            var added = await intake.ExecuteAsync(legacyEntry, ct);
            Assert.Equal(400, added.LegacyMillilitres);
            Assert.Equal(650, added.TotalMillilitres);
            var corrected = await intake.ExecuteAsync(legacyEntry with
            {
                ExpectedVersion = added.Version,
                OperationId = Guid.NewGuid(),
                Kind = WaterIntakeChangeKindDto.Correct,
                Millilitres = 300
            }, ct);
            Assert.Equal(700, (await diary.GetDayAsync(account, legacyDate, 10, null, ct)).WaterIntakeMl);
            var undone = await intake.ExecuteAsync(legacyEntry with
            {
                ExpectedVersion = corrected.Version,
                OperationId = Guid.NewGuid(),
                Kind = WaterIntakeChangeKindDto.Undo,
                Millilitres = 0
            }, ct);
            Assert.Equal(400, undone.TotalMillilitres);
            var oldDay = await diary.GetDayAsync(account, legacyDate, 10, null, ct);
            Assert.Equal(400, oldDay.WaterIntakeMl);
            Assert.Equal("Synthetic notes", oldDay.Notes);

            var beforeDrift = await intakeStore.ReadAsync(account, legacyDate, ct);
            var nextEntry = legacyEntry with
            {
                ExpectedVersion = undone.Version,
                OperationId = Guid.NewGuid(),
                EntryId = Guid.NewGuid()
            };
            var proposedDay = beforeDrift.Day.Apply(new WaterIntakeChange(nextEntry.OperationId,
                nextEntry.EntryId, WaterIntakeChangeKind.Add, nextEntry.Millilitres));
            await diary.UpdateMetadataAsync(account, new UpdateNutritionDiaryMetadata(legacyDate, 325, "Updated notes"), ct);
            // Model the race where the rich client has loaded the ledger before the legacy update commits.
            var writeConflict = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                intakeStore.TryReplaceAsync(proposedDay, beforeDrift.Version, ct));
            Assert.Equal(LegacyTotalConflict, writeConflict.Message);
            await AssertIntakeConflictOverHttpAsync(connection, nextEntry);

            var legacyUpdate = await diary.GetDayAsync(account, legacyDate, 10, null, ct);
            Assert.Equal(325, legacyUpdate.WaterIntakeMl);
            Assert.Equal("Updated notes", legacyUpdate.Notes);

            // Restore the matching mirror in this disposable fixture to verify the rejected operations
            // did not change the original ledger, version or replay receipt.
            await diary.UpdateMetadataAsync(account, new UpdateNutritionDiaryMetadata(legacyDate, 400, "Updated notes"), ct);
            var stillUndone = await new GetWaterIntakeUseCase(intakeStore).ExecuteAsync(new(account, legacyDate), ct);
            Assert.Equal(undone.Version, stillUndone.Version);
            Assert.Equal(undone.TotalMillilitres, stillUndone.TotalMillilitres);
            Assert.True(Assert.Single(stillUndone.Entries).Undone);
        }
        finally
        {
            await fixture.DropAsync(connection);
        }
    }

    private static async Task AssertIntakeConflictOverHttpAsync(string connection, ChangeWaterIntakeInputDto request)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthorization();
        builder.Services.AddDbContext<PostgresDbContext>(options => options.UseNpgsql(connection));
        builder.Services.AddScoped<IWaterIntakeStore>(services =>
            new PostgresHydrationStore(services.GetRequiredService<PostgresDbContext>(), TimeProvider.System));
        builder.Services.AddScoped<GetWaterIntakeUseCase>();
        builder.Services.AddScoped<ChangeWaterIntakeUseCase>();
        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", request.AccountId.ToString("D"))], "Synthetic"));
            await next(context);
        });
        app.UseAuthorization();
        app.MapHydrationProgressAndFastingEndpoints();
        await app.StartAsync();
        using var client = app.GetTestClient();
        const string route = "/api/v1/hydration/intake";

        using var read = await client.GetAsync(FormattableString.Invariant($"{route}?date={request.Date:yyyy-MM-dd}"));
        await AssertConflictAsync(read);
        using var write = await client.PostAsJsonAsync(route, new
        {
            request.Date,
            request.ExpectedVersion,
            request.OperationId,
            request.EntryId,
            request.Kind,
            request.Millilitres
        });
        await AssertConflictAsync(write);
    }

    private static async Task AssertConflictAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var error = Assert.Single(body!);
        Assert.Equal("error", error.Key);
        Assert.Equal(LegacyTotalConflict, error.Value);
    }
}
