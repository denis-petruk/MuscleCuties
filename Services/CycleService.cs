// Services/CycleService.cs
using MuscleCuties.Models;
using MuscleCuties.Models.Enums;
using MuscleCuties.Repositories;

namespace MuscleCuties.Services;

public class CycleService : ICycleService
{
    private readonly ICycleRepository _cycleRepository;
    private readonly IUserRepository _userRepository;

    public CycleService(ICycleRepository cycleRepository, IUserRepository userRepository)
    {
        _cycleRepository = cycleRepository;
        _userRepository = userRepository;
    }

    public async Task<CyclePhase> GetCurrentPhaseAsync(int userId)
    {
        var cycle = await _cycleRepository.GetLatestCycleAsync(userId);
        if (cycle == null) return CyclePhase.Follicular;

        var profile = await _userRepository.GetProfileAsync(userId);
        var cycleLength = profile?.CycleLength ?? 28;

        var day = CalculateCycleDay(cycle.CycleStartDate);
        return CalculatePhase(day, cycleLength);
    }

    public async Task<CycleLog?> GetCurrentCycleAsync(int userId) =>
        await _cycleRepository.GetLatestCycleAsync(userId);

    public async Task StartNewCycleAsync(int userId)
    {
        var profile = await _userRepository.GetProfileAsync(userId);

        var cycle = new CycleLog
        {
            UserId = userId,
            CycleStartDate = DateTime.UtcNow,
            CycleLength = profile?.CycleLength ?? 28,
            PeriodLength = 5
        };

        await _cycleRepository.AddAsync(cycle);
    }

    public async Task EndCurrentCycleAsync(int userId)
    {
        var cycle = await _cycleRepository.GetLatestCycleAsync(userId);
        if (cycle == null) return;

        cycle.CycleEndDate = DateTime.UtcNow;
        await _cycleRepository.UpdateAsync(cycle);
    }

    public int CalculateCycleDay(DateTime cycleStartDate)
    {
        var day = (DateTime.UtcNow - cycleStartDate).Days + 1;
        return day < 1 ? 1 : day;
    }

    public CyclePhase CalculatePhase(int cycleDay, int cycleLength)
    {
        if (cycleDay <= 5) return CyclePhase.Menstrual;

        var ovulationDay = cycleLength - 14;
        if (cycleDay <= ovulationDay - 2) return CyclePhase.Follicular;
        if (cycleDay <= ovulationDay + 2) return CyclePhase.Ovulatory;

        return CyclePhase.Luteal;
    }
}
