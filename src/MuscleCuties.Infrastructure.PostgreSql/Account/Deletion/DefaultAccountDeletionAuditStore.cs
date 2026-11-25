using System.Collections.Concurrent;
using MuscleCuties.UseCases.Features.Account.Deletion;

namespace MuscleCuties.Infrastructure.PostgreSql.Account.Deletion;

/// <summary>Records what a deletion did, without the account's data: only the outcome and reasons, never personal content.</summary>
public sealed class DefaultAccountDeletionAuditStore : IAccountDeletionAuditStore
{
    private readonly ConcurrentBag<AccountDeletionAuditEntry> _entries = [];
    private readonly string? _logFilePath;
    private readonly object _fileLock = new();

    /// <summary>Takes an optional log file; without one the audit is kept in memory, which is what development uses.</summary>
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

    /// <summary>Records one account deletion.</summary>
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

    /// <summary>The deletion audits recorded for the account.</summary>
    public Task<IReadOnlyList<AccountDeletionAuditEntry>> GetDeletionAuditsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AccountDeletionAuditEntry> list = _entries.ToList();
        return Task.FromResult(list);
    }
}
