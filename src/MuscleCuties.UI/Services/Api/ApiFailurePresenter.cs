using System.Globalization;
using System.Net;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.Services.Api;

/// <summary>
/// The action a person can take next after a failed request. The screen uses it to decide whether to
/// offer retry, ask for a sign-in, tell the person to wait, or point them at support.
/// </summary>
public enum ApiFailureAction
{
    None = 0,
    Retry = 1,
    WaitAndRetry = 2,
    SignInAgain = 3,
    Offline = 4,
    ContactSupport = 5
}

/// <summary>
/// One failure, already reduced to a localized message key, the next action and the server reference.
/// The reference is only displayed when the server actually sent one, so a support conversation can
/// name the exact request.
/// </summary>
public sealed class ApiFailureNotice
{
    public ApiFailureNotice(string messageKey, ApiFailureAction action, string? requestId)
    {
        MessageKey = messageKey;
        Action = action;
        RequestId = requestId;
        MessageText = UiTextCatalog.Get(messageKey);
        ReferenceText = string.IsNullOrWhiteSpace(requestId)
            ? string.Empty
            : string.Format(
                CultureInfo.CurrentCulture,
                UiTextCatalog.Get(ApiFailurePresenter.ReferenceFormatKey),
                requestId);
    }

    public string MessageKey { get; }

    public string MessageText { get; }

    public ApiFailureAction Action { get; }

    public string? RequestId { get; }

    public string ReferenceText { get; }

    public bool IsRetryable =>
        Action is ApiFailureAction.Retry or ApiFailureAction.WaitAndRetry or ApiFailureAction.Offline;
}

/// <summary>
/// Maps transport outcomes onto distinct user actions. Thin screens that only caught
/// <see cref="HttpRequestException"/> used to show one "check your connection" line for a rate limit,
/// an outage, an expired session and a server fault alike; the status and the server reference are now
/// preserved end to end so each case gets its own instruction.
/// </summary>
public static class ApiFailurePresenter
{
    public const string ReferenceFormatKey = "Ui_Common_Error_ReferenceFormat";

    private const string OfflineKey = "Ui_Common_Error_Offline";
    private const string TimeoutKey = "Ui_Common_Error_Timeout";
    private const string RateLimitedKey = "Ui_Common_Error_RateLimited";
    private const string ServiceUnavailableKey = "Ui_Common_Error_ServiceUnavailable";
    private const string ServerKey = "Ui_Common_Error_Server";
    private const string SessionExpiredKey = "Ui_Common_Error_SessionExpired";
    private const string SupportRequiredKey = "Ui_Common_Error_SupportRequired";

    /// <summary>Any status above this boundary is a server-side fault, not client input.</summary>
    private const int MinimumServerErrorStatusCode = 500;

    private const string DatabaseUnavailableCode = "databaseUnavailable";
    private const string ServiceUnavailableCode = "serviceUnavailable";
    private const string RequestTimeoutCode = "requestTimeout";
    private const string DatabaseMissingTableCode = "databaseMissingTable";
    private const string DatabaseMissingColumnCode = "databaseMissingColumn";
    private const string DatabasePermissionDeniedCode = "databasePermissionDenied";

    public static ApiFailureNotice FromResponse<TResponse>(
        BackendApiResponse<TResponse> response,
        string fallbackKey) =>
        response.Status switch
        {
            BackendApiStatus.Unauthorized =>
                new ApiFailureNotice(SessionExpiredKey, ApiFailureAction.SignInAgain, response.RequestId),
            BackendApiStatus.OfflineRejected =>
                new ApiFailureNotice(OfflineKey, ApiFailureAction.Offline, null),
            BackendApiStatus.ServerError =>
                FromStatusCode(
                    response.ResponseStatusCode,
                    response.ErrorCode,
                    response.RequestId,
                    fallbackKey),
            _ => new ApiFailureNotice(fallbackKey, ApiFailureAction.None, response.RequestId)
        };

    public static ApiFailureNotice FromException(Exception exception, string fallbackKey)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is TaskCanceledException)
            return new ApiFailureNotice(TimeoutKey, ApiFailureAction.Retry, null);

        if (exception is HttpRequestException { StatusCode: { } status })
            return FromStatusCode(status, null, null, fallbackKey);

        return new ApiFailureNotice(fallbackKey, ApiFailureAction.Retry, null);
    }

    private static ApiFailureNotice FromStatusCode(
        HttpStatusCode? status,
        string? errorCode,
        string? requestId,
        string fallbackKey)
    {
        if (errorCode is DatabaseMissingTableCode or DatabaseMissingColumnCode or DatabasePermissionDeniedCode)
            return new ApiFailureNotice(SupportRequiredKey, ApiFailureAction.ContactSupport, requestId);

        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return new ApiFailureNotice(SessionExpiredKey, ApiFailureAction.SignInAgain, requestId);

        if (status is HttpStatusCode.TooManyRequests)
            return new ApiFailureNotice(RateLimitedKey, ApiFailureAction.WaitAndRetry, requestId);

        if (status is HttpStatusCode.ServiceUnavailable ||
            errorCode is DatabaseUnavailableCode or ServiceUnavailableCode)
            return new ApiFailureNotice(ServiceUnavailableKey, ApiFailureAction.Retry, requestId);

        if (errorCode == RequestTimeoutCode)
            return new ApiFailureNotice(TimeoutKey, ApiFailureAction.Retry, requestId);

        if (status is { } known && (int)known >= MinimumServerErrorStatusCode)
            return new ApiFailureNotice(ServerKey, ApiFailureAction.Retry, requestId);

        return new ApiFailureNotice(fallbackKey, ApiFailureAction.None, requestId);
    }
}
