using System.Net;
using MuscleCuties.Infrastructure.ApiClient;

namespace MuscleCuties.Infrastructure.Cloud;

/// <summary>
/// A cloud store response that never became data.
/// <para>
/// The backend status, error code, HTTP status and request reference are preserved so a screen can
/// tell an ended session, a rate limit, an outage and a rejected request apart. Previously only the
/// message survived, so every one of those outcomes reached the person as the same connection line
/// with a retry that could not succeed.
/// </para>
/// <para>
/// It derives from <see cref="InvalidOperationException"/> deliberately: callers that already treat a
/// failed cloud response as an invalid operation keep their current behavior, while the presenter can
/// classify the typed failure first.
/// </para>
/// </summary>
public sealed class CloudRequestFailedException : InvalidOperationException
{
    public CloudRequestFailedException(
        string operation,
        BackendApiStatus status,
        string? errorCode,
        HttpStatusCode? responseStatusCode,
        string? requestId,
        string message)
        : base(message)
    {
        Operation = operation;
        Status = status;
        ErrorCode = errorCode;
        ResponseStatusCode = responseStatusCode;
        RequestId = requestId;
    }

    /// <summary>The store operation that failed, for example <c>dashboard overview</c>.</summary>
    public string Operation { get; }

    public BackendApiStatus Status { get; }

    public string? ErrorCode { get; }

    /// <summary>The status the server answered with, when the request reached it.</summary>
    public HttpStatusCode? ResponseStatusCode { get; }

    /// <summary>The server's bounded request reference, when it sent one.</summary>
    public string? RequestId { get; }
}
