using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Repositories.Users;

public interface IPhotoVaultRepository
{
    Task<IReadOnlyList<PhotoJourneyEntry>> GetByUserIdAsync(int userId);
    Task<PhotoJourneyEntry?> GetByIdAsync(int id);
    Task AddAsync(PhotoJourneyEntry entry);
    Task DeleteAsync(int id);
}

public class PhotoVaultRepository : IPhotoVaultRepository
{
    private readonly AppDatabase _database;

    public PhotoVaultRepository(AppDatabase database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<PhotoJourneyEntry>> GetByUserIdAsync(int userId)
    {
        return await _database.PhotoJourneyEntries
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.Date)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<PhotoJourneyEntry?> GetByIdAsync(int id)
    {
        return await _database.PhotoJourneyEntries
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task AddAsync(PhotoJourneyEntry entry)
    {
        await _database.PhotoJourneyEntries.AddAsync(entry);
        await _database.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entry = await _database.PhotoJourneyEntries.FindAsync(id);
        if (entry != null)
        {
            _database.PhotoJourneyEntries.Remove(entry);
            await _database.SaveChangesAsync();
        }
    }
}
