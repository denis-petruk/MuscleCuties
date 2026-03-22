using Microsoft.EntityFrameworkCore;
using MuscleCuties.Data;

namespace MuscleCuties.Repositories;

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
        await _db.Set<T>().ToListAsync();

    public async Task AddAsync(T entity)
    {
        await _db.Set<T>().AddAsync(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(T entity)
    {
        _db.Set<T>().Update(entity);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(T entity)
    {
        _db.Set<T>().Remove(entity);
        await _db.SaveChangesAsync();
    }
}