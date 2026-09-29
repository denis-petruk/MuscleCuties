using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Repositories.Workout.Planning;
using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.Core.Tests;

public class InjuryLogRegressionTests
{
    [Fact]
    public void New_Injury_Requires_An_Explicit_Status_Choice()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var viewModel = new InjuryLogViewModel(
            services.GetRequiredService<IServiceScopeFactory>(), () => Task.CompletedTask);

        viewModel.ToggleAddFormCommand.Execute(null);
        viewModel.SelectSiteCommand.Execute(viewModel.AvailableSites.First());
        Assert.False(viewModel.CanSaveInjury);
        Assert.DoesNotContain(viewModel.AvailableStatuses, option => option.IsSelected);

        var recovering = viewModel.AvailableStatuses.Single(option => option.Status == InjuryStatus.Recovering);
        viewModel.SelectStatusCommand.Execute(recovering);

        Assert.True(viewModel.CanSaveInjury);
        Assert.Equal(InjuryStatus.Recovering, viewModel.SelectedStatus);
        Assert.Single(viewModel.AvailableStatuses, option => option.IsSelected);
        Assert.True(recovering.IsSelected);
    }

    [Fact]
    public void Add_After_Editing_Starts_With_A_Blank_Form()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var viewModel = new InjuryLogViewModel(
            services.GetRequiredService<IServiceScopeFactory>(), () => Task.CompletedTask);

        viewModel.EditInjuryCommand.Execute(new InjuryLogItem
        {
            Id = 42,
            SiteFlag = InjuryFlag.Ankle,
            Status = InjuryStatus.Acute,
            Pain = 3,
            Since = DateOnly.FromDateTime(DateTime.Today.AddDays(-7)),
        });

        viewModel.ToggleAddFormCommand.Execute(null);

        Assert.True(viewModel.ShowAddForm);
        Assert.False(viewModel.IsEditing);
        Assert.Null(viewModel.EditingInjuryId);
        Assert.Equal(InjuryFlag.None, viewModel.SelectedSiteFlag);
        Assert.Null(viewModel.SelectedStatus);
        Assert.DoesNotContain(viewModel.AvailableStatuses, option => option.IsSelected);
        Assert.Equal(0, viewModel.NewPainLevel);
        Assert.Equal(DateTime.Today, viewModel.SinceDateValue);
    }

    [Fact]
    public void ResetForm_Clears_Stale_Edit_State_On_Modal_Reopen()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var viewModel = new InjuryLogViewModel(
            services.GetRequiredService<IServiceScopeFactory>(), () => Task.CompletedTask);

        viewModel.EditInjuryCommand.Execute(new InjuryLogItem
        {
            Id = 42,
            SiteFlag = InjuryFlag.Knee,
            Status = InjuryStatus.Recovering,
            Pain = 4,
            Since = DateOnly.FromDateTime(DateTime.Today.AddDays(-14)),
        });

        Assert.True(viewModel.ShowAddForm);
        Assert.Equal(42, viewModel.EditingInjuryId);

        viewModel.ResetForm();

        Assert.False(viewModel.ShowAddForm);
        Assert.Null(viewModel.EditingInjuryId);
        Assert.Equal(InjuryFlag.None, viewModel.SelectedSiteFlag);
        Assert.Null(viewModel.SelectedStatus);
        Assert.DoesNotContain(viewModel.AvailableStatuses, option => option.IsSelected);
        Assert.Equal(0, viewModel.NewPainLevel);
        Assert.Equal(DateTime.Today, viewModel.SinceDateValue);
    }

    [Fact]
    public async Task Cleared_Injury_Remains_In_History_But_Not_Active_List()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDatabase>().UseSqlite(connection).Options;
        await using var database = new AppDatabase(options);
        await database.Database.EnsureCreatedAsync();
        var repository = new WorkoutInjuryRepository(database);
        var injury = new WorkoutInjuryLog
        {
            UserId = 7,
            SiteFlag = (int)InjuryFlag.Ankle,
            Status = InjuryStatus.Acute.ToString(),
            Since = DateOnly.FromDateTime(DateTime.Today.AddDays(-7)),
            Date = DateOnly.FromDateTime(DateTime.Today),
            Pain = 3,
        };

        await repository.SaveAsync(injury);
        injury.Status = InjuryStatus.Cleared.ToString();
        await repository.SaveAsync(injury);

        Assert.Empty(await repository.GetActiveAsync(7));
        var history = await repository.GetAllAsync(7);
        Assert.Single(history);
        Assert.Equal(InjuryStatus.Cleared.ToString(), history[0].Status);

        injury.Status = "cleared";
        await repository.SaveAsync(injury);
        Assert.Empty(await repository.GetActiveAsync(7));
    }

    [Fact]
    public async Task Injury_Changes_Are_Scoped_To_The_Owning_User()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDatabase>().UseSqlite(connection).Options;
        await using var database = new AppDatabase(options);
        await database.Database.EnsureCreatedAsync();
        var repository = new WorkoutInjuryRepository(database);
        var injury = new WorkoutInjuryLog
        {
            UserId = 7,
            SiteFlag = (int)InjuryFlag.Ankle,
            Status = InjuryStatus.Acute.ToString(),
            Since = DateOnly.FromDateTime(DateTime.Today),
            Date = DateOnly.FromDateTime(DateTime.Today),
        };
        await repository.SaveAsync(injury);

        await repository.DeleteAsync(8, injury.Id);
        Assert.Single(await repository.GetActiveAsync(7));

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveAsync(new WorkoutInjuryLog
        {
            Id = injury.Id,
            UserId = 8,
            SiteFlag = (int)InjuryFlag.Knee,
            Status = InjuryStatus.Cleared.ToString(),
            Since = injury.Since,
            Date = injury.Date,
        }));
        Assert.Single(await repository.GetActiveAsync(7));
    }
}
