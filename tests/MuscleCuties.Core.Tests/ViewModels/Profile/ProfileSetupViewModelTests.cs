using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Quiz;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.ViewModels.Profile;
using NSubstitute;

namespace MuscleCuties.Core.Tests.ViewModels.Profile;

public class ProfileSetupViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly IQuizService _quizService = Substitute.For<IQuizService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private bool _navigatedToQuiz;

    private ProfileSetupViewModel CreateViewModel()
    {
        _quizService.GetOnboardingQuestionsAsync().Returns([]);
        return new ProfileSetupViewModel(
            _authService,
            _userRepository,
            _quizService,
            new QuizQuestionCache(),
            () =>
            {
                _navigatedToQuiz = true;
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task LoadData_UpdatesSelectionsWithoutRebuildingActivityGroups()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _userRepository.GetProfileAsync(1).Returns(new UserProfile
        {
            UserId = 1,
            PreferredWorkoutActivityTypes = WorkoutActivityPreferences.Serialize(
                [WorkoutActivityType.HighVolumeStrength, WorkoutActivityType.Running],
                StrengthTrainingStyle.ExpressHard)
        });
        var vm = CreateViewModel();
        var groups = vm.GroupedWorkoutActivityOptions;
        var options = vm.WorkoutActivityOptions;
        var strengthOptions = vm.StrengthTrainingStyleOptions;

        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Same(groups, vm.GroupedWorkoutActivityOptions);
        Assert.Same(options, vm.WorkoutActivityOptions);
        Assert.Same(strengthOptions, vm.StrengthTrainingStyleOptions);
        var running = Assert.Single(options, option => option.ActivityType == WorkoutActivityType.Running);
        Assert.True(running.IsSelected);
        Assert.Equal(StrengthTrainingStyle.ExpressHard, vm.SelectedStrengthTrainingStyle);
        Assert.True(Assert.Single(strengthOptions, option => option.Style == StrengthTrainingStyle.ExpressHard).IsSelected);

        vm.ToggleWorkoutActivityCommand.Execute(running);

        Assert.False(running.IsSelected);
        Assert.Same(groups, vm.GroupedWorkoutActivityOptions);
    }

    [Fact]
    public async Task Save_NewProfile_AddsSnapshotAndNavigatesToQuiz()
    {
        var user = new User { Id = 1, Email = "jane@test.com", PasswordHash = "hash" };
        _authService.GetCurrentUserIdAsync().Returns(1);
        _userRepository.GetProfileAsync(1).Returns((UserProfile?)null);
        _userRepository.GetByIdAsync(1).Returns(user);

        var vm = CreateViewModel();
        vm.Name = "Jane";
        vm.Height = 165f;
        vm.Weight = 60f;
        vm.WorkoutDaysPerWeek = 4;
        vm.CycleLength = 28;
        vm.SetProfileImage("/tmp/profile_avatar.jpg");

        await vm.SaveCommand.ExecuteAsync(null);

        await _userRepository.Received(1).AddProfileAsync(Arg.Is<UserProfile>(p =>
            p.UserId == 1 &&
            p.Name == "Jane" &&
            p.ProfileImagePath == "/tmp/profile_avatar.jpg"));
        await _userRepository.Received(1).AddSnapshotAsync(Arg.Is<UserProfileSnapshot>(s =>
            s.UserId == 1 &&
            s.SnapshotReason == "InitialProfileSetup"));
        await _userRepository.Received(1).UpdateAsync(Arg.Is<User>(u =>
            u.Id == 1 &&
            !u.IsOnboardingComplete));
        Assert.True(_navigatedToQuiz);
    }

    [Fact]
    public async Task Save_ExistingProfile_UpdatesWithoutAddingDuplicate()
    {
        var profile = new UserProfile
        {
            Id = 10,
            UserId = 1,
            Name = string.Empty,
            Goal = UserGoal.Strength,
            WorkoutDaysPerWeek = 4,
            CycleLength = 0
        };
        var user = new User { Id = 1, Email = "jane@test.com", PasswordHash = "hash" };
        _authService.GetCurrentUserIdAsync().Returns(1);
        _userRepository.GetProfileAsync(1).Returns(profile);
        _userRepository.GetByIdAsync(1).Returns(user);

        var vm = CreateViewModel();
        vm.Name = "Jane";
        vm.Height = 165f;
        vm.Weight = 60f;
        vm.CycleLength = 28;

        await vm.SaveCommand.ExecuteAsync(null);

        await _userRepository.DidNotReceive().AddProfileAsync(Arg.Any<UserProfile>());
        await _userRepository.Received(1).UpdateProfileAsync(Arg.Is<UserProfile>(p =>
            p.Id == 10 &&
            p.Name == "Jane" &&
            p.WorkoutDaysPerWeek == 4 &&
            p.CycleLength == 28));
        await _userRepository.Received(1).AddSnapshotAsync(Arg.Is<UserProfileSnapshot>(s =>
            s.UserId == 1 &&
            s.SnapshotReason == "ProfileSetup"));
        Assert.True(_navigatedToQuiz);
    }

    [Fact]
    public async Task Save_ExistingProfile_PreservesQuizDerivedProfileFields()
    {
        var profile = new UserProfile
        {
            Id = 20,
            UserId = 1,
            Name = string.Empty,
            Goal = UserGoal.Strength,
            TrainingExperienceLevel = TrainingExperienceLevel.Advanced,
            WorkoutDaysPerWeek = 5,
            CycleLength = 31,
            DietaryTags = DietaryTag.Vegan.ToString()
        };
        var user = new User { Id = 1, Email = "jane2@test.com", PasswordHash = "hash" };
        _authService.GetCurrentUserIdAsync().Returns(1);
        _userRepository.GetProfileAsync(1).Returns(profile);
        _userRepository.GetByIdAsync(1).Returns(user);

        var vm = CreateViewModel();
        vm.Name = "Jane";
        vm.CycleLength = 28;

        await vm.SaveCommand.ExecuteAsync(null);

        await _userRepository.Received(1).UpdateProfileAsync(Arg.Is<UserProfile>(p =>
            p.Id == 20 &&
            p.Goal == UserGoal.Strength &&
            p.TrainingExperienceLevel == TrainingExperienceLevel.Advanced &&
            p.WorkoutDaysPerWeek == 5 &&
            p.CycleLength == 31 &&
            p.DietaryTags == "Vegan"));
    }

    [Fact]
    public async Task Save_WhenCalled_SetsBusy()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _userRepository.GetProfileAsync(1).Returns((UserProfile?)null);

        var vm = CreateViewModel();
        vm.Name = "Jane";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(vm.IsBusy);
    }
}
