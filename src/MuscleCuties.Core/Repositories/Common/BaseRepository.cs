using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;

namespace MuscleCuties.Core.Repositories.Common;

public abstract class BaseRepository<T> : IRepository<T> where T : class
{
    protected readonly AppDatabase _db;

    protected BaseRepository(AppDatabase db)
    {
        _db = db;
    }

    public async Task<T?> GetByIdAsync(int id) =>
        await _db.Set<T>().FindAsync(id);

    public async Task<List<T>> GetAllAsync() =>
        await _db.Set<T>()
            .AsNoTracking()
            .ToListAsync();

    public async Task AddAsync(T entity)
    {
        await _db.Set<T>().AddAsync(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(T entity)
    {
        DetachTrackedLocal(entity);
        _db.Set<T>().Update(entity);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(T entity)
    {
        _db.Set<T>().Remove(entity);
        await _db.SaveChangesAsync();
    }

    protected void DetachTrackedLocal<TEntity>(TEntity entity) where TEntity : class
    {
        var entry = _db.Entry(entity);
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey?.Properties.Count != 1)
            return;

        var keyName = primaryKey.Properties[0].Name;
        var keyValue = entry.Property(keyName).CurrentValue;
        if (keyValue is null)
            return;

        var localEntity = _db.Set<TEntity>()
            .Local
            .FirstOrDefault(tracked =>
                !ReferenceEquals(tracked, entity) &&
                Equals(_db.Entry(tracked).Property(keyName).CurrentValue, keyValue));

        if (localEntity is not null)
            _db.Entry(localEntity).State = EntityState.Detached;
    }
}
