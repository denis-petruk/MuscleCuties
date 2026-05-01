using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;

namespace MuscleCuties.Core.Services;

public class CycleService : ICycleService
{
    private readonly ICycleRepository _cycleRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICyclePhaseCalculator _calculator;

    public CycleService(
        ICycleRepository cycleRepository,
        IUserRepository userRepository,
        ICyclePhaseCalculator calculator)
    {
        _cycleRepository = cycleRepository;
        _userRepository = userRepository;
        _calculator = calculator;
    }

    public async Task<CyclePhase> GetCurrentPhaseAsync(int userId)
    {
        var cycle = await _cycleRepository.GetLatestCycleAsync(userId);
        if (cycle == null) return CyclePhase.Follicular;

        var profile = await _userRepository.GetProfileAsync(userId);
        var cycleLength = profile?.CycleLength > 0 ? profile.CycleLength : 28;

        var day = (int)(DateTime.UtcNow - cycle.StartDate).TotalDays + 1;
        day = day < 1 ? 1 : day;
        return _calculator.CalculatePhase(day, cycleLength);
    }

    public async Task<CycleLog?> GetCurrentCycleAsync(int userId) =>
        await _cycleRepository.GetLatestCycleAsync(userId);

    public async Task StartNewCycleAsync(int userId)
    {
        var cycle = new CycleLog
        {
            UserId = userId,
            StartDate = DateTime.UtcNow,
            CycleLength = 0,
            CreatedAt = DateTime.UtcNow
        };
        await _cycleRepository.AddAsync(cycle);
    }

    public async Task EndCurrentCycleAsync(int userId)
    {
        var cycle = await _cycleRepository.GetLatestCycleAsync(userId);
        if (cycle == null) return;

        cycle.EndDate = DateTime.UtcNow;
        cycle.CycleLength = (int)(cycle.EndDate.Value - cycle.StartDate).TotalDays;
        await _cycleRepository.UpdateAsync(cycle);
    }
}