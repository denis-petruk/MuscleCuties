using MuscleCuties.Core.Models.Entities.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

public partial class FoodSyncService
{
    private async Task<FoodSyncLog> StartLogAsync()
    {
        var log = new FoodSyncLog
        {
            StartedAt = DateTime.UtcNow,
            ItemsUpserted = 0,
            ItemsFailed = 0,
            Status = "Running"
        };

        await _foodSyncRepository.AddSyncLogAsync(log);
        return log;
    }

    private async Task CompleteLogAsync(
        FoodSyncLog log,
        string status,
        IEnumerable<string> errors,
        Exception? exception = null)
    {
        var allErrors = exception is null
            ? errors.ToList()
            : errors.Concat([exception.Message]).ToList();

        log.CompletedAt = DateTime.UtcNow;
        log.Status = status;
        log.ErrorDetails = allErrors.Count == 0 ? null : string.Join(Environment.NewLine, allErrors);
        await _foodSyncRepository.UpdateSyncLogAsync(log);
    }

    private static string BuildStatus(FoodSyncLog log, List<string> errors)
    {
        if ((log.ItemsFailed > 0 || errors.Count > 0) && log.ItemsUpserted > 0)
            return "Partial";

        if (log.ItemsFailed > 0 || errors.Count > 0)
            return "Failed";

        return "Success";
    }
}
