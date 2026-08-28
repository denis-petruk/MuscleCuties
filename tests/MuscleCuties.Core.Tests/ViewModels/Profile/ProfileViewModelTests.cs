using NSubstitute;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Repositories.Common;
using MuscleCuties.Core.Repositories.Cycle;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Quiz;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Repositories.Workout;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Progress;
using MuscleCuties.Core.Services.Quiz;
using MuscleCuties.Core.ViewModels.Auth;
using MuscleCuties.Core.ViewModels.Cycle;
using MuscleCuties.Core.ViewModels.Dashboard;
using MuscleCuties.Core.ViewModels.Nutrition;
using MuscleCuties.Core.ViewModels.Profile;
using MuscleCuties.Core.ViewModels.Quiz;
using MuscleCuties.Core.ViewModels.Workout;

namespace MuscleCuties.Core.Tests.ViewModels.Profile;

public class ProfileViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICycleService _cycleService = Substitute.For<ICycleService>();
    private readonly IProgressSummaryService _progressSummaryService = Substitute.For<IProgressSummaryService>();
    private bool _navigatedToLogin;

    private ProfileViewModel CreateViewModel() =>
        new(
            _authService,
            _userRepository,
            _cycleService,
            _progressSummaryService,
            () => _navigatedToLogin = true);

    [Fact]
    public async Task LoadData_SetsNameEmailGoal()
    {
        var user = new User { Id = 1, Email = "user@test.com", PasswordHash = "hash" };
        var profile = new UserProfile
        {
            Id = 1,
            UserId = 1,
            Name = "Jane",
            Goal = UserGoal.FatLoss
        };

        _authService.GetCurrentUserIdAsync().Returns(1);
        _userRepository.GetByIdAsync(1).Returns(user);
        _userRepository.GetProfileAsync(1).Returns(profile);
        _progressSummaryService.GetSummaryAsync(1, Arg.Any<DateTime>())
            .Returns(new ProgressSummary(5, 3, 7));
        _cycleService.GetPredictionAsync(1).Returns(new CyclePrediction
        {
            CurrentPhase = CyclePhase.Luteal,
            PredictedCycleLength = 28
        });
        _cycleService.GetCycleHistoryAsync(1).Returns(Array.Empty<MuscleCuties.Core.Models.Entities.Cycle.CycleLog>());

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal("Jane", vm.Name);
        Assert.Equal("user@test.com", vm.Email);
        Assert.Equal(UserGoal.FatLoss, vm.Goal);
        Assert.Equal(5, vm.SessionCount);
        Assert.Equal(3, vm.WorkoutStreakDays);
        Assert.Equal(7, vm.NutritionStreakDays);
    }

    [Fact]
    public async Task Logout_CallsLogoutAsync_AndNavigates()
    {
        var vm = CreateViewModel();
        await vm.LogoutCommand.ExecuteAsync(null);

        await _authService.Received(1).LogoutAsync();
        Assert.True(_navigatedToLogin);
    }
}
