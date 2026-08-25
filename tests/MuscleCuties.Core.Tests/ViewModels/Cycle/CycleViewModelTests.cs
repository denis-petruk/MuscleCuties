using NSubstitute;
using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Quiz;
using MuscleCuties.Core.ViewModels.Auth;
using MuscleCuties.Core.ViewModels.Cycle;
using MuscleCuties.Core.ViewModels.Dashboard;
using MuscleCuties.Core.ViewModels.Nutrition;
using MuscleCuties.Core.ViewModels.Profile;
using MuscleCuties.Core.ViewModels.Quiz;
using MuscleCuties.Core.ViewModels.Workout;

namespace MuscleCuties.Core.Tests.ViewModels.Cycle;

public class CycleViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly ICycleService _cycleService = Substitute.For<ICycleService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();

    private CycleViewModel CreateViewModel() =>
        new(_authService, _cycleService, _userRepository);

    private void SetupCurrentUser(int userId = 1, DateTime? createdAt = null)
    {
        _authService.GetCurrentUserIdAsync().Returns(userId);
        _userRepository.GetByIdAsync(userId).Returns(new User
        {
            Id = userId,
            Email = $"cycle-{userId}@test.com",
            PasswordHash = "hash",
            CreatedAt = createdAt ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
        });
    }

    [Fact]
    public async Task LoadData_WithActiveCycle_SetsCycleDay()
    {
        var prediction = new CyclePrediction
        {
            HasActiveCycle = true,
            CurrentDay = 11,
            PredictedCycleLength = 28,
            CurrentPhase = CyclePhase.Follicular,
            DaysUntilPeriod = 17
        };

        SetupCurrentUser();
        _cycleService.GetPredictionAsync(1).Returns(prediction);
        _cycleService.GetRecentPhaseLogsAsync(1, Arg.Any<int>()).Returns(Array.Empty<CyclePhaseLog>());

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(11, vm.CycleDay);
        Assert.Equal(28, vm.CycleLength);
        Assert.Equal(CyclePhase.Follicular, vm.CurrentPhase);
    }

    [Fact]
    public async Task LoadData_WithNoCycle_CycleDayIsZero()
    {
        SetupCurrentUser();
        _cycleService.GetPredictionAsync(1).Returns(new CyclePrediction
        {
            HasActiveCycle = false,
            CurrentDay = 0,
            PredictedCycleLength = 28,
            CurrentPhase = CyclePhase.Follicular,
            DaysUntilPeriod = 0
        });
        _cycleService.GetRecentPhaseLogsAsync(1, Arg.Any<int>()).Returns(Array.Empty<CyclePhaseLog>());

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.CycleDay);
        Assert.Equal(28, vm.CycleLength);
    }

    [Fact]
    public async Task LoadData_UsesCurrentPhaseForHighlightedCalendarDay()
    {
        SetupCurrentUser();
        _cycleService.GetPredictionAsync(1).Returns(new CyclePrediction
        {
            HasActiveCycle = true,
            CurrentDay = 14,
            CurrentCycleStartDate = DateTime.Today.AddDays(-13),
            PredictedCycleLength = 28,
            CurrentPhase = CyclePhase.Luteal,
            DaysUntilPeriod = 14
        });
        _cycleService.GetRecentPhaseLogsAsync(1, Arg.Any<int>()).Returns([
            new CyclePhaseLog
            {
                UserId = 1,
                Phase = CyclePhase.Luteal,
                LoggedAt = DateTime.Today
            }
        ]);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        var highlightedDay = vm.CalendarDays.Single(day => day.Date == DateTime.Today);
        Assert.Equal(DateTime.Today.Day, highlightedDay.Day);
        Assert.Equal(14, highlightedDay.CycleDay);
        Assert.Equal(CyclePhase.Luteal, highlightedDay.Phase);
        Assert.Equal(2, highlightedDay.StrokeThickness);
    }

    [Fact]
    public async Task LoadData_UsesTodayPhaseLogForCurrentCalendarDay()
    {
        SetupCurrentUser();
        _cycleService.GetPredictionAsync(1).Returns(new CyclePrediction
        {
            HasActiveCycle = true,
            CurrentDay = 11,
            CurrentCycleStartDate = DateTime.Today.AddDays(-10),
            PredictedCycleLength = 28,
            CurrentPhase = CyclePhase.Follicular,
            DaysUntilPeriod = 17
        });
        _cycleService.GetRecentPhaseLogsAsync(1, Arg.Any<int>()).Returns([
            new CyclePhaseLog
            {
                UserId = 1,
                Phase = CyclePhase.Luteal,
                LoggedAt = DateTime.Today,
                CreatedAt = DateTime.UtcNow
            }
        ]);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        var today = vm.CalendarDays.Single(day => day.Date == DateTime.Today);
        Assert.Equal(CyclePhase.Luteal, today.Phase);
    }


    [Fact]
    public async Task LoadData_ProjectsFutureCalendarDaysFromLoggedShift()
    {
        SetupCurrentUser();
        _cycleService.GetPredictionAsync(1).Returns(new CyclePrediction
        {
            HasActiveCycle = true,
            CurrentCycleStartDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
            CurrentDay = DateTime.Today.Day,
            PredictedCycleLength = 28,
            CurrentPhase = CyclePhase.Luteal,
            DaysUntilPeriod = 14
        });
        var loggedAt = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 10);
        _cycleService.GetRecentPhaseLogsAsync(1, Arg.Any<int>()).Returns([
            new CyclePhaseLog
            {
                UserId = 1,
                Phase = CyclePhase.Luteal,
                LoggedAt = loggedAt
            }
        ]);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        var nextDay = vm.CalendarDays.Single(day => day.Date == loggedAt.AddDays(1));
        Assert.Equal(CyclePhase.Luteal, nextDay.Phase);
    }

    [Fact]
    public async Task LoadData_FutureProjectedCalendarDaysAreMarkedAsPredictions()
    {
        SetupCurrentUser();
        _cycleService.GetPredictionAsync(1).Returns(new CyclePrediction
        {
            HasActiveCycle = true,
            CurrentCycleStartDate = DateTime.Today.AddDays(-9),
            CurrentDay = 10,
            PredictedCycleLength = 28,
            CurrentPhase = CyclePhase.Follicular,
            DaysUntilPeriod = 18
        });
        _cycleService.GetRecentPhaseLogsAsync(1, Arg.Any<int>()).Returns(Array.Empty<CyclePhaseLog>());

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        var futureDay = vm.CalendarDays.First(day => day.Date > DateTime.Today);
        var today = vm.CalendarDays.Single(day => day.Date == DateTime.Today);

        Assert.True(futureDay.IsPredictedFuture);
        Assert.True(futureDay.StrokeThickness > 0);
        Assert.False(today.IsPredictedFuture);
    }

    [Fact]
    public async Task LoadData_FutureLoggedShiftIsNotMarkedAsPrediction()
    {
        SetupCurrentUser();
        var futureLogDate = DateTime.Today.AddDays(1);
        _cycleService.GetPredictionAsync(1).Returns(new CyclePrediction
        {
            HasActiveCycle = true,
            CurrentCycleStartDate = DateTime.Today.AddDays(-9),
            CurrentDay = 10,
            PredictedCycleLength = 28,
            CurrentPhase = CyclePhase.Follicular,
            DaysUntilPeriod = 18
        });
        _cycleService.GetRecentPhaseLogsAsync(1, Arg.Any<int>()).Returns([
            new CyclePhaseLog
            {
                UserId = 1,
                Phase = CyclePhase.Ovulatory,
                LoggedAt = futureLogDate,
                CreatedAt = DateTime.Today
            }
        ]);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        var loggedFutureDay = vm.CalendarDays.Single(day => day.Date == futureLogDate.Date);

        Assert.False(loggedFutureDay.IsPredictedFuture);
        Assert.True(loggedFutureDay.HasPhaseShiftLog);
    }

    [Fact]
    public async Task LoadData_DatesBeforeAccountCreationStayNeutral()
    {
        var accountCreatedAt = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 10);
        SetupCurrentUser(createdAt: accountCreatedAt);
        _cycleService.GetPredictionAsync(1).Returns(new CyclePrediction
        {
            HasActiveCycle = true,
            CurrentCycleStartDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
            CurrentDay = DateTime.Today.Day,
            PredictedCycleLength = 28,
            CurrentPhase = CyclePhase.Follicular,
            DaysUntilPeriod = 14
        });
        _cycleService.GetRecentPhaseLogsAsync(1, Arg.Any<int>()).Returns(Array.Empty<CyclePhaseLog>());

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        var beforeAccount = vm.CalendarDays.Single(day => day.Date == accountCreatedAt.AddDays(-1));
        var accountDay = vm.CalendarDays.Single(day => day.Date == accountCreatedAt);

        Assert.True(beforeAccount.IsNeutral);
        Assert.False(accountDay.IsNeutral);
    }

    [Fact]
    public async Task OpenCalendarDayAndSave_WritesSelectedPhaseForThatDate()
    {
        SetupCurrentUser();
        _cycleService.GetPredictionAsync(1).Returns(new CyclePrediction
        {
            HasActiveCycle = true,
            CurrentCycleStartDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
            CurrentDay = DateTime.Today.Day,
            PredictedCycleLength = 28,
            CurrentPhase = CyclePhase.Follicular,
            DaysUntilPeriod = 14
        });
        _cycleService.GetRecentPhaseLogsAsync(1, Arg.Any<int>()).Returns(Array.Empty<CyclePhaseLog>());

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        var calendarDay = vm.CalendarDays.Single(day => day.Date == DateTime.Today);
        vm.OpenCalendarDayCommand.Execute(calendarDay);
        vm.SelectPhaseOptionCommand.Execute(vm.PhaseEditOptions.Single(option => option.Phase == CyclePhase.Luteal));
        await vm.SaveDatePhaseCommand.ExecuteAsync(null);

        await _cycleService.Received(1).SetPhaseForDateAsync(
            1,
            CyclePhase.Luteal,
            Arg.Is<DateTime>(date => date.Date == DateTime.Today),
            "Calendar phase edit");
        Assert.False(vm.IsDatePhaseModalVisible);
    }

    [Fact]
    public async Task SaveDatePhase_WhenSkippingExpectedPhase_ShowsFriendlyWarning()
    {
        SetupCurrentUser();
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var previousShiftDate = monthStart.AddDays(4);
        var targetDate = previousShiftDate.AddDays(1);

        _cycleService.GetPredictionAsync(1).Returns(new CyclePrediction
        {
            HasActiveCycle = true,
            CurrentCycleStartDate = monthStart,
            CurrentDay = DateTime.Today.Day,
            PredictedCycleLength = 28,
            CurrentPhase = CyclePhase.Follicular,
            DaysUntilPeriod = 14
        });
        _cycleService.GetRecentPhaseLogsAsync(1, Arg.Any<int>()).Returns([
            new CyclePhaseLog
            {
                UserId = 1,
                Phase = CyclePhase.Menstrual,
                LoggedAt = previousShiftDate,
                CreatedAt = previousShiftDate
            }
        ]);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        var calendarDay = vm.CalendarDays.Single(day => day.Date == targetDate);
        vm.OpenCalendarDayCommand.Execute(calendarDay);
        vm.SelectPhaseOptionCommand.Execute(vm.PhaseEditOptions.Single(option => option.Phase == CyclePhase.Ovulatory));
        await vm.SaveDatePhaseCommand.ExecuteAsync(null);

        Assert.True(vm.HasPhaseJumpWarning);
        Assert.Contains("rhythm", vm.PhaseJumpWarningTitle);
        await _cycleService.DidNotReceive().SetPhaseForDateAsync(
            Arg.Any<int>(),
            Arg.Any<CyclePhase>(),
            Arg.Any<DateTime>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task UseSuggestedPhase_LogsExpectedPhaseAfterOrderWarning()
    {
        SetupCurrentUser();
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var previousShiftDate = monthStart.AddDays(4);
        var targetDate = previousShiftDate.AddDays(1);

        _cycleService.GetPredictionAsync(1).Returns(new CyclePrediction
        {
            HasActiveCycle = true,
            CurrentCycleStartDate = monthStart,
            CurrentDay = DateTime.Today.Day,
            PredictedCycleLength = 28,
            CurrentPhase = CyclePhase.Follicular,
            DaysUntilPeriod = 14
        });
        _cycleService.GetRecentPhaseLogsAsync(1, Arg.Any<int>()).Returns([
            new CyclePhaseLog
            {
                UserId = 1,
                Phase = CyclePhase.Menstrual,
                LoggedAt = previousShiftDate,
                CreatedAt = previousShiftDate
            }
        ]);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        var calendarDay = vm.CalendarDays.Single(day => day.Date == targetDate);
        vm.OpenCalendarDayCommand.Execute(calendarDay);
        vm.SelectPhaseOptionCommand.Execute(vm.PhaseEditOptions.Single(option => option.Phase == CyclePhase.Ovulatory));
        await vm.SaveDatePhaseCommand.ExecuteAsync(null);
        vm.UseSuggestedPhaseCommand.Execute(null);
        await vm.SaveDatePhaseCommand.ExecuteAsync(null);

        await _cycleService.Received(1).SetPhaseForDateAsync(
            1,
            CyclePhase.Follicular,
            Arg.Is<DateTime>(date => date.Date == targetDate.Date),
            "Calendar phase edit");
    }

    [Fact]
    public async Task SaveDatePhase_WhenServiceRejectsOrder_ShowsModalWarning()
    {
        SetupCurrentUser();
        _cycleService.GetPredictionAsync(1).Returns(new CyclePrediction
        {
            HasActiveCycle = true,
            CurrentCycleStartDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
            CurrentDay = DateTime.Today.Day,
            PredictedCycleLength = 28,
            CurrentPhase = CyclePhase.Follicular,
            DaysUntilPeriod = 14
        });
        _cycleService.GetRecentPhaseLogsAsync(1, Arg.Any<int>()).Returns(Array.Empty<CyclePhaseLog>());
        var targetDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _cycleService.SetPhaseForDateAsync(
                1,
                CyclePhase.Ovulatory,
                Arg.Is<DateTime>(date => date.Date == targetDate.Date),
                "Calendar phase edit")
            .Returns(Task.FromException(new CyclePhaseOrderException(
                "This phase would break the cycle order. Log Follicular before Ovulatory.",
                CyclePhase.Follicular)));

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        var calendarDay = vm.CalendarDays.Single(day => day.Date == targetDate);
        vm.OpenCalendarDayCommand.Execute(calendarDay);
        vm.SelectPhaseOptionCommand.Execute(vm.PhaseEditOptions.Single(option => option.Phase == CyclePhase.Ovulatory));
        await vm.SaveDatePhaseCommand.ExecuteAsync(null);

        Assert.True(vm.IsDatePhaseModalVisible);
        Assert.True(vm.HasPhaseJumpWarning);
        Assert.True(vm.HasSuggestedPhase);
        Assert.Contains("Follicular", vm.PhaseJumpWarningText);
    }

    [Fact]
    public async Task SaveDatePhase_WhenBreakingOrderBeforeNextLog_ShowsFriendlyWarning()
    {
        SetupCurrentUser();
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var targetDate = monthStart.AddDays(6);
        var nextShiftDate = targetDate.AddDays(1);

        _cycleService.GetPredictionAsync(1).Returns(new CyclePrediction
        {
            HasActiveCycle = true,
            CurrentCycleStartDate = monthStart,
            CurrentDay = DateTime.Today.Day,
            PredictedCycleLength = 28,
            CurrentPhase = CyclePhase.Follicular,
            DaysUntilPeriod = 14
        });
        _cycleService.GetRecentPhaseLogsAsync(1, Arg.Any<int>()).Returns([
            new CyclePhaseLog
            {
                UserId = 1,
                Phase = CyclePhase.Luteal,
                LoggedAt = nextShiftDate,
                CreatedAt = nextShiftDate
            }
        ]);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        var calendarDay = vm.CalendarDays.Single(day => day.Date == targetDate);
        vm.OpenCalendarDayCommand.Execute(calendarDay);
        vm.SelectPhaseOptionCommand.Execute(vm.PhaseEditOptions.Single(option => option.Phase == CyclePhase.Follicular));
        await vm.SaveDatePhaseCommand.ExecuteAsync(null);

        Assert.True(vm.HasPhaseJumpWarning);
        Assert.Contains("missing ovulatory", vm.PhaseJumpWarningText);
        await _cycleService.DidNotReceive().SetPhaseForDateAsync(
            Arg.Any<int>(),
            Arg.Any<CyclePhase>(),
            Arg.Any<DateTime>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task ReviewEarlierPhaseRecords_ClosesModalAndGuidesBackfill()
    {
        SetupCurrentUser();
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var previousShiftDate = monthStart.AddDays(4);
        var targetDate = previousShiftDate.AddDays(1);

        _cycleService.GetPredictionAsync(1).Returns(new CyclePrediction
        {
            HasActiveCycle = true,
            CurrentCycleStartDate = monthStart,
            CurrentDay = DateTime.Today.Day,
            PredictedCycleLength = 28,
            CurrentPhase = CyclePhase.Follicular,
            DaysUntilPeriod = 14
        });
        _cycleService.GetRecentPhaseLogsAsync(1, Arg.Any<int>()).Returns([
            new CyclePhaseLog
            {
                UserId = 1,
                Phase = CyclePhase.Menstrual,
                LoggedAt = previousShiftDate,
                CreatedAt = previousShiftDate
            }
        ]);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        var calendarDay = vm.CalendarDays.Single(day => day.Date == targetDate);
        vm.OpenCalendarDayCommand.Execute(calendarDay);
        vm.SelectPhaseOptionCommand.Execute(vm.PhaseEditOptions.Single(option => option.Phase == CyclePhase.Ovulatory));
        await vm.SaveDatePhaseCommand.ExecuteAsync(null);

        vm.ReviewEarlierPhaseRecordsCommand.Execute(null);

        Assert.False(vm.IsDatePhaseModalVisible);
        Assert.False(vm.HasPhaseJumpWarning);
        Assert.Contains("missed shift", vm.CalendarEditHintText);
    }

    [Fact]
    public async Task AdvancePhaseCommand_LogsNextPhase()
    {
        SetupCurrentUser();
        _cycleService.GetPredictionAsync(1).Returns(
            new CyclePrediction
            {
                HasActiveCycle = true,
                CurrentDay = 14,
                PredictedCycleLength = 28,
                CurrentPhase = CyclePhase.Ovulatory,
                DaysUntilPeriod = 14
            },
            new CyclePrediction
            {
                HasActiveCycle = true,
                CurrentDay = 14,
                PredictedCycleLength = 28,
                CurrentPhase = CyclePhase.Luteal,
                DaysUntilPeriod = 14
            });
        _cycleService.GetRecentPhaseLogsAsync(1, Arg.Any<int>()).Returns(Array.Empty<CyclePhaseLog>());

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        await vm.AdvancePhaseCommand.ExecuteAsync(null);

        await _cycleService.Received(1).SetPhaseForDateAsync(
            1,
            CyclePhase.Luteal,
            Arg.Is<DateTime>(date => date.Date == DateTime.Today),
            "Manual phase advance");
    }

    [Fact]
    public async Task AdvancePhaseCommand_WhenCycleOrderIsRejected_ShowsWarningPopup()
    {
        SetupCurrentUser();
        _cycleService.GetPredictionAsync(1).Returns(
            new CyclePrediction
            {
                HasActiveCycle = true,
                CurrentDay = 13,
                PredictedCycleLength = 28,
                CurrentPhase = CyclePhase.Follicular,
                DaysUntilPeriod = 15
            },
            new CyclePrediction
            {
                HasActiveCycle = true,
                CurrentDay = 14,
                PredictedCycleLength = 28,
                CurrentPhase = CyclePhase.Follicular,
                DaysUntilPeriod = 14
            });
        _cycleService.GetRecentPhaseLogsAsync(1, Arg.Any<int>()).Returns(Array.Empty<CyclePhaseLog>());
        _cycleService.SetPhaseForDateAsync(
                1,
                CyclePhase.Ovulatory,
                Arg.Is<DateTime>(date => date.Date == DateTime.Today),
                "Manual phase advance")
            .Returns(Task.FromException(new CyclePhaseOrderException(
                "This phase would break the cycle order. Yesterday was Menstrual, so log Follicular before Ovulatory.",
                CyclePhase.Follicular)));
        _cycleService.SetPhaseForDateAsync(
                1,
                CyclePhase.Follicular,
                Arg.Is<DateTime>(date => date.Date == DateTime.Today),
                "Manual phase correction")
            .Returns(Task.CompletedTask);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        await vm.AdvancePhaseCommand.ExecuteAsync(null);

        Assert.True(vm.IsCycleWarningPopupVisible);
        Assert.True(vm.HasCycleWarningSuggestedPhase);
        Assert.Equal("Use Follicular", vm.CycleWarningSuggestedActionText);
        Assert.Contains("Follicular", vm.CycleWarningText);

        await vm.UseCycleWarningSuggestedPhaseCommand.ExecuteAsync(null);

        Assert.False(vm.IsCycleWarningPopupVisible);
        await _cycleService.Received(1).SetPhaseForDateAsync(
            1,
            CyclePhase.Follicular,
            Arg.Is<DateTime>(date => date.Date == DateTime.Today),
            "Manual phase correction");
    }

    [Fact]
    public async Task UseCycleWarningSuggestedPhase_WhenStillRejected_ShowsRepairMessage()
    {
        SetupCurrentUser();
        _cycleService.GetPredictionAsync(1).Returns(new CyclePrediction
        {
            HasActiveCycle = true,
            CurrentDay = 13,
            PredictedCycleLength = 28,
            CurrentPhase = CyclePhase.Follicular,
            DaysUntilPeriod = 15
        });
        _cycleService.GetRecentPhaseLogsAsync(1, Arg.Any<int>()).Returns(Array.Empty<CyclePhaseLog>());
        _cycleService.SetPhaseForDateAsync(
                1,
                CyclePhase.Ovulatory,
                Arg.Is<DateTime>(date => date.Date == DateTime.Today),
                "Manual phase advance")
            .Returns(Task.FromException(new CyclePhaseOrderException(
                "This phase would break the cycle order. Yesterday was Menstrual, so log Follicular before Ovulatory.",
                CyclePhase.Follicular)));
        _cycleService.SetPhaseForDateAsync(
                1,
                CyclePhase.Follicular,
                Arg.Is<DateTime>(date => date.Date == DateTime.Today),
                "Manual phase correction")
            .Returns(Task.FromException(new CyclePhaseOrderException(
                "This phase would break the cycle order. Log Ovulatory before Luteal.",
                CyclePhase.Ovulatory)));

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);
        await vm.AdvancePhaseCommand.ExecuteAsync(null);

        await vm.UseCycleWarningSuggestedPhaseCommand.ExecuteAsync(null);

        Assert.True(vm.IsCycleWarningPopupVisible);
        Assert.False(vm.HasCycleWarningSuggestedPhase);
        Assert.Equal("Fix the missed shift first", vm.CycleWarningTitle);
        Assert.Contains("Forgot to log shift", vm.CycleWarningText);
    }
}
