using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Services.Auth;

public sealed record OnboardingCommitResult(
    bool Succeeded,
    User? User,
    string? ErrorMessage = null);

