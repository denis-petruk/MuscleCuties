using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Repositories.Users;

namespace MuscleCuties.Core.Services.Profile;

public interface IPhotoVaultService
{
    Task<IReadOnlyList<PhotoJourneyEntry>> GetPhotosAsync(int userId);

    Task<PhotoJourneyEntry?> AddPhotoAsync(int userId, string localImagePath, DateTime date, double? weightKg,
        string? notes, CyclePhase phase);

    Task<bool> DeletePhotoAsync(int userId, int entryId);
    Task<int> GetPhotoCountAsync(int userId);
    Task<PhotoJourneyEntry?> GetLatestPhotoAsync(int userId);
}

public sealed class PhotoVaultService : IPhotoVaultService
{
    private readonly AppDatabase _database;
    private readonly IPhotoVaultRepository _repository;

    public PhotoVaultService(IPhotoVaultRepository repository, AppDatabase database)
    {
        _repository = repository;
        _database = database;
    }

    public Task<IReadOnlyList<PhotoJourneyEntry>> GetPhotosAsync(int userId)
    {
        if (userId <= 0)
            return Task.FromResult<IReadOnlyList<PhotoJourneyEntry>>(Array.Empty<PhotoJourneyEntry>());

        return _repository.GetByUserIdAsync(userId);
    }

    public async Task<PhotoJourneyEntry?> AddPhotoAsync(
        int userId,
        string localImagePath,
        DateTime date,
        double? weightKg,
        string? notes,
        CyclePhase phase)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(localImagePath))
            return null;

        var entry = new PhotoJourneyEntry
        {
            UserId = userId,
            ImagePath = localImagePath.Trim(),
            Date = date,
            WeightKg = weightKg is > 0 ? weightKg : null,
            Notes = notes?.Trim() ?? string.Empty,
            Phase = phase,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(entry);
        return entry;
    }

    public async Task<bool> DeletePhotoAsync(int userId, int entryId)
    {
        if (userId <= 0 || entryId <= 0)
            return false;

        var entry = await _repository.GetByIdAsync(entryId);
        if (entry is null || entry.UserId != userId)
            return false;

        try
        {
            await ProfileMediaDeletion.DeleteIfUnreferencedAsync(_database, entry.ImagePath, entryId);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        await _repository.DeleteAsync(entryId);
        return true;
    }

    public async Task<int> GetPhotoCountAsync(int userId)
    {
        if (userId <= 0)
            return 0;

        var photos = await _repository.GetByUserIdAsync(userId);
        return photos.Count;
    }

    public async Task<PhotoJourneyEntry?> GetLatestPhotoAsync(int userId)
    {
        if (userId <= 0)
            return null;

        var photos = await _repository.GetByUserIdAsync(userId);
        return photos.FirstOrDefault();
    }
}
