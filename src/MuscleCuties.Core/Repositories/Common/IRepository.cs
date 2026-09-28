using System.Diagnostics.CodeAnalysis;

namespace MuscleCuties.Core.Repositories.Common;

public interface IRepository<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}
