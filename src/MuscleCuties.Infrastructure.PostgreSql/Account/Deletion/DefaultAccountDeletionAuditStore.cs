using System.Collections.Concurrent;
using System.Text.Json;
using MuscleCuties.UseCases.Features.Account.Deletion;

namespace MuscleCuties.Infrastructure.PostgreSql.Account.Deletion;

public sealed class DefaultAccountDeletionAuditStore : IAccountDeletionAuditStore
{
    private readonly ConcurrentBag<AccountDeletionAuditEntry> _entries = [];
    private readonly string? _logFilePath;
    private readonly object _fileLock = new();

    public DefaultAccountDeletionAuditStore(string? logFilePath = null)
    {
        _logFilePath = logFilePath;
        if (!string.IsNullOrWhiteSpace(_logFilePath) && File.Exists(_logFilePath))
        {
            try
            {
                var json = File.ReadAllText(_logFilePath);
                var loaded = JsonSerializer.Deserialize<List<AccountDeletionAuditEntry>>(json);
                if (loaded != null)
                {
                    foreach (var entry in loaded)
                    {
                        _entries.Add(entry);
                    }
                }
            }
            catch
            {
                // Fall back to clean bag if file is corrupt or unreadable
            }
        }
    }

    public Task RecordDeletionAuditAsync(
        AccountDeletionAuditEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _entries.Add(entry);

        if (!string.IsNullOrWhiteSpace(_logFilePath))
        {
            lock (_fileLock)
            {
                var directory = Path.GetDirectoryName(_logFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var list = _entries.ToList();
                var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_logFilePath, json);
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AccountDeletionAuditEntry>> GetDeletionAuditsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AccountDeletionAuditEntry> list = _entries.ToList();
        return Task.FromResult(list);
    }
}
