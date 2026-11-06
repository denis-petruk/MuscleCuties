using System.Net;

namespace MuscleCuties.Infrastructure.ApiClient;

/// <summary>
/// A planning request whose outcome the screen can act on. It stays an
/// <see cref="HttpRequestException"/> so existing transport handling keeps working, but it preserves
/// the backend status, error category and server request reference that a plain connection error
/// throws away. PG01 needs those to tell an offline device, an expired session, a limited proposal and
/// a server fault apart instead of showing one connection message for all of them.
/// </summary>
public sealed class PlanningRequestFailedException : HttpRequestException
{
    public PlanningRequestFailedException(
        string operation,
        BackendApiStatus status,
        HttpStatusCode? responseStatusCode,
        string? errorCode,
        string? requestId)
        : base($"Cloud {operation} did not complete ({status}).", null, responseStatusCode)
    {
        Operation = operation;
        Status = status;
        ErrorCode = errorCode;
        RequestId = requestId;
    }

    /// <summary>The planning step that failed, e.g. "create strength plan proposal".</summary>
    public string Operation { get; }

    public BackendApiStatus Status { get; }

    /// <summary>The backend's stable error category, when it sent one.</summary>
    public string? ErrorCode { get; }

    /// <summary>The server reference to quote in a support conversation, when it sent one.</summary>
    public string? RequestId { get; }
}
