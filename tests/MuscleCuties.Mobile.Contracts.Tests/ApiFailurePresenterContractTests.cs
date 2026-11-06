using System.Net;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UI.Services.Api;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

public class ApiFailurePresenterContractTests
{
    private const string FallbackMessageKey = "Ui_Common_Error_Fallback";

    [Fact]
    public void FromResponse_Unauthorized_ReturnsSessionExpiredAndSignInAgain()
    {
        var response = new BackendApiResponse<string>(
            BackendApiStatus.Unauthorized,
            Data: null,
            ErrorCode: "unauthorized",
            RequestId: "req-auth-1");

        var notice = ApiFailurePresenter.FromResponse(response, FallbackMessageKey);

        Assert.Equal(ApiFailureAction.SignInAgain, notice.Action);
        Assert.Equal("Ui_Common_Error_SessionExpired", notice.MessageKey);
        Assert.False(notice.IsRetryable);
        Assert.Equal("req-auth-1", notice.RequestId);
        Assert.Contains("req-auth-1", notice.ReferenceText);
    }

    [Fact]
    public void FromResponse_OfflineRejected_ReturnsOfflineAction()
    {
        var response = new BackendApiResponse<string>(
            BackendApiStatus.OfflineRejected,
            Data: null,
            ErrorCode: "networkOffline");

        var notice = ApiFailurePresenter.FromResponse(response, FallbackMessageKey);

        Assert.Equal(ApiFailureAction.Offline, notice.Action);
        Assert.Equal("Ui_Common_Error_Offline", notice.MessageKey);
        Assert.True(notice.IsRetryable);
        Assert.Null(notice.RequestId);
        Assert.Empty(notice.ReferenceText);
    }

    [Fact]
    public void FromResponse_RateLimited_ReturnsWaitAndRetry()
    {
        var response = new BackendApiResponse<string>(
            BackendApiStatus.ServerError,
            Data: null,
            ErrorCode: "rateLimited",
            ResponseStatusCode: HttpStatusCode.TooManyRequests,
            RequestId: "req-rate-limit-429");

        var notice = ApiFailurePresenter.FromResponse(response, FallbackMessageKey);

        Assert.Equal(ApiFailureAction.WaitAndRetry, notice.Action);
        Assert.Equal("Ui_Common_Error_RateLimited", notice.MessageKey);
        Assert.True(notice.IsRetryable);
        Assert.Equal("req-rate-limit-429", notice.RequestId);
    }

    [Fact]
    public void FromResponse_Timeout_ReturnsRetry()
    {
        var response = new BackendApiResponse<string>(
            BackendApiStatus.Timeout,
            Data: null,
            ErrorCode: "requestTimeout",
            RequestId: "req-timeout-408");

        var notice = ApiFailurePresenter.FromResponse(response, FallbackMessageKey);

        Assert.Equal(ApiFailureAction.Retry, notice.Action);
        Assert.Equal("Ui_Common_Error_Timeout", notice.MessageKey);
        Assert.True(notice.IsRetryable);
        Assert.Equal("req-timeout-408", notice.RequestId);
    }

    [Fact]
    public void FromResponse_Conflict_ReturnsRetryWithFallbackKey()
    {
        var response = new BackendApiResponse<string>(
            BackendApiStatus.Conflict,
            Data: null,
            ErrorCode: "concurrencyConflict",
            RequestId: "req-conflict-409");

        var notice = ApiFailurePresenter.FromResponse(response, FallbackMessageKey);

        Assert.Equal(ApiFailureAction.Retry, notice.Action);
        Assert.Equal(FallbackMessageKey, notice.MessageKey);
        Assert.True(notice.IsRetryable);
        Assert.Equal("req-conflict-409", notice.RequestId);
    }

    [Fact]
    public void FromResponse_ServerError_500_ReturnsServerNoticeAndRetry()
    {
        var response = new BackendApiResponse<string>(
            BackendApiStatus.ServerError,
            Data: null,
            ErrorCode: "internalError",
            ResponseStatusCode: HttpStatusCode.InternalServerError,
            RequestId: "req-server-500");

        var notice = ApiFailurePresenter.FromResponse(response, FallbackMessageKey);

        Assert.Equal(ApiFailureAction.Retry, notice.Action);
        Assert.Equal("Ui_Common_Error_Server", notice.MessageKey);
        Assert.True(notice.IsRetryable);
        Assert.Equal("req-server-500", notice.RequestId);
    }

    [Fact]
    public void FromResponse_ServiceUnavailable_ReturnsServiceUnavailableNotice()
    {
        var response = new BackendApiResponse<string>(
            BackendApiStatus.ServerError,
            Data: null,
            ErrorCode: "databaseUnavailable",
            ResponseStatusCode: HttpStatusCode.ServiceUnavailable,
            RequestId: "req-service-503");

        var notice = ApiFailurePresenter.FromResponse(response, FallbackMessageKey);

        Assert.Equal(ApiFailureAction.Retry, notice.Action);
        Assert.Equal("Ui_Common_Error_ServiceUnavailable", notice.MessageKey);
        Assert.True(notice.IsRetryable);
        Assert.Equal("req-service-503", notice.RequestId);
    }

    [Theory]
    [InlineData("databaseMissingTable")]
    [InlineData("databaseMissingColumn")]
    [InlineData("databasePermissionDenied")]
    public void FromResponse_DatabaseSchemaMissing_ReturnsContactSupportAction(string errorCode)
    {
        var response = new BackendApiResponse<string>(
            BackendApiStatus.ServerError,
            Data: null,
            ErrorCode: errorCode,
            ResponseStatusCode: HttpStatusCode.InternalServerError,
            RequestId: "req-schema-fault");

        var notice = ApiFailurePresenter.FromResponse(response, FallbackMessageKey);

        Assert.Equal(ApiFailureAction.ContactSupport, notice.Action);
        Assert.Equal("Ui_Common_Error_SupportRequired", notice.MessageKey);
        Assert.False(notice.IsRetryable);
        Assert.Equal("req-schema-fault", notice.RequestId);
    }

    [Fact]
    public void FromException_TaskCanceledException_ReturnsTimeoutAndRetry()
    {
        var exception = new TaskCanceledException("Operation timed out.");
        var notice = ApiFailurePresenter.FromException(exception, FallbackMessageKey);

        Assert.Equal(ApiFailureAction.Retry, notice.Action);
        Assert.Equal("Ui_Common_Error_Timeout", notice.MessageKey);
        Assert.True(notice.IsRetryable);
        Assert.Null(notice.RequestId);
    }

    [Fact]
    public void FromException_HttpRequestException_429_ReturnsRateLimitedAndWaitAndRetry()
    {
        var exception = new HttpRequestException("Rate limited", null, HttpStatusCode.TooManyRequests);
        var notice = ApiFailurePresenter.FromException(exception, FallbackMessageKey);

        Assert.Equal(ApiFailureAction.WaitAndRetry, notice.Action);
        Assert.Equal("Ui_Common_Error_RateLimited", notice.MessageKey);
        Assert.True(notice.IsRetryable);
    }

    [Fact]
    public void FromResponse_GeneratorFailure_ReturnsContactSupportAction()
    {
        var response = new BackendApiResponse<string>(
            BackendApiStatus.ServerError,
            Data: null,
            ErrorCode: "generatorFailure",
            ResponseStatusCode: HttpStatusCode.InternalServerError,
            RequestId: "req-gen-fail-500");

        var notice = ApiFailurePresenter.FromResponse(response, FallbackMessageKey);

        Assert.Equal(ApiFailureAction.ContactSupport, notice.Action);
        Assert.False(notice.IsRetryable);
        Assert.Equal("req-gen-fail-500", notice.RequestId);
    }

    [Theory]
    [InlineData("planInfeasible")]
    [InlineData("planningProfileInvalid")]
    [InlineData("safetyAnswersRequired")]
    public void FromResponse_PlanInfeasibleOrInputRequired_ReturnsActionNone(string errorCode)
    {
        var response = new BackendApiResponse<string>(
            BackendApiStatus.ServerError,
            Data: null,
            ErrorCode: errorCode,
            ResponseStatusCode: HttpStatusCode.UnprocessableEntity,
            RequestId: "req-infeasible");

        var notice = ApiFailurePresenter.FromResponse(response, FallbackMessageKey);

        Assert.Equal(ApiFailureAction.None, notice.Action);
        Assert.False(notice.IsRetryable);
        Assert.Equal("req-infeasible", notice.RequestId);
    }

    [Fact]
    public void FromException_SocketException_ReturnsOfflineAction()
    {
        var exception = new System.Net.Sockets.SocketException(10061); // Connection refused
        var notice = ApiFailurePresenter.FromException(exception, FallbackMessageKey);

        Assert.Equal(ApiFailureAction.Offline, notice.Action);
        Assert.Equal("Ui_Common_Error_Offline", notice.MessageKey);
        Assert.True(notice.IsRetryable);
    }

    [Fact]
    public void FromException_AuthenticationException_ReturnsSignInAgainAction()
    {
        var exception = new System.Security.Authentication.AuthenticationException("Invalid credentials.");
        var notice = ApiFailurePresenter.FromException(exception, FallbackMessageKey);

        Assert.Equal(ApiFailureAction.SignInAgain, notice.Action);
        Assert.Equal("Ui_Common_Error_SessionExpired", notice.MessageKey);
        Assert.False(notice.IsRetryable);
    }

    [Fact]
    public void FromException_GeneratorInvalidOperation_ReturnsActionNone()
    {
        var exception = new InvalidOperationException("Workout generator failed to find safe candidates.");
        var notice = ApiFailurePresenter.FromException(exception, FallbackMessageKey);

        Assert.Equal(ApiFailureAction.None, notice.Action);
        Assert.False(notice.IsRetryable);
    }

    [Fact]
    public void SanitizeRequestId_PreventsCredentialAndPiiLeakage()
    {
        // Valid trace correlation identifier preserved
        var validNotice = new ApiFailureNotice(FallbackMessageKey, ApiFailureAction.Retry, "028417007003-trace-12345");
        Assert.Equal("028417007003-trace-12345", validNotice.RequestId);
        Assert.Contains("028417007003-trace-12345", validNotice.ReferenceText);

        // Sensitive email address stripped
        var piiNotice = new ApiFailureNotice(FallbackMessageKey, ApiFailureAction.Retry, "denis@musclecuties.test");
        Assert.Null(piiNotice.RequestId);
        Assert.Empty(piiNotice.ReferenceText);

        // Sensitive Bearer token stripped
        var tokenNotice = new ApiFailureNotice(FallbackMessageKey, ApiFailureAction.Retry, "Bearer eyJhbGciOi...");
        Assert.Null(tokenNotice.RequestId);
        Assert.Empty(tokenNotice.ReferenceText);

        // Excessive length stripped
        var oversizedNotice = new ApiFailureNotice(FallbackMessageKey, ApiFailureAction.Retry, new string('x', 200));
        Assert.Null(oversizedNotice.RequestId);
        Assert.Empty(oversizedNotice.ReferenceText);
    }
}
