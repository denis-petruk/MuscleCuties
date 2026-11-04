using System.Text.Json;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Server.Workout.Planning;

/// <summary>
/// Dedicated Lambda entry point for a first-party scheduler or queue. Configure the
/// function's resource policy so only that trusted dispatcher can invoke it; this
/// entry point is not exposed through the HTTP API.
/// </summary>
public sealed class WorkoutPendingRevisionLambda
{
    private static readonly Lazy<WebApplication> Application = new(() =>
        ApiApplication.Build(WebApplication.CreateBuilder()));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Stream> FunctionHandler(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var message = await JsonSerializer.DeserializeAsync<PendingWorkoutRevisionMessage>(input, JsonOptions)
            .ConfigureAwait(false);
        if (message is null || message.UserId == Guid.Empty)
            throw new ArgumentException("A valid account ID is required.", nameof(input));

        await using var scope = Application.Value.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<WorkoutPendingRevisionJobHandler>();
        var outcome = await handler.ExecuteAsync(message.UserId).ConfigureAwait(false);
        // Asynchronous Lambda and queue integrations do not consume return values.
        // Fail the invocation so the configured dispatcher retries or dead-letters it.
        if (outcome != WorkoutReconciliationOutcome.Completed)
            throw new InvalidOperationException($"Workout revision requires {outcome} handling.");
        var output = new MemoryStream();
        await JsonSerializer.SerializeAsync(output, new { status = outcome.ToString() }, JsonOptions).ConfigureAwait(false);
        output.Position = 0;
        return output;
    }

    public sealed record PendingWorkoutRevisionMessage(Guid UserId);
}
