using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MuscleCuties.Infrastructure.PostgreSql.Account.EmailChallenge;
using Xunit;

namespace MuscleCuties.Server.Tests;

public sealed class ResendEmailChallengeDeliveryTests
{
    private const string ValidApiKey = "re_test_key_123456789";
    private const string ValidEmail = "athlete@musclecuties.app";
    private const string ValidCode = "749201";
    private const int RegistrationPurpose = 1;
    private const int PasswordRecoveryPurpose = 2;

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SendAsync_WhenApiKeyMissing_ReturnsFalse(string? apiKey)
    {
        var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        var delivery = new ResendEmailChallengeDelivery(client, apiKey);

        var result = await delivery.SendAsync(ValidEmail, ValidCode, RegistrationPurpose, CancellationToken.None);

        Assert.False(result);
        Assert.Null(handler.CapturedRequest);
    }

    [Theory]
    [InlineData("", ValidCode)]
    [InlineData("not-an-email", "123")]
    [InlineData(ValidEmail, "12345")]
    [InlineData(ValidEmail, "1234567")]
    [InlineData(ValidEmail, "")]
    public async Task SendAsync_WhenEmailOrCodeInvalid_ReturnsFalse(string email, string code)
    {
        var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        var delivery = new ResendEmailChallengeDelivery(client, ValidApiKey);

        var result = await delivery.SendAsync(email, code, RegistrationPurpose, CancellationToken.None);

        Assert.False(result);
        Assert.Null(handler.CapturedRequest);
    }

    [Fact]
    public async Task SendAsync_WhenUnknownPurpose_ReturnsFalse()
    {
        var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        var delivery = new ResendEmailChallengeDelivery(client, ValidApiKey);

        var result = await delivery.SendAsync(ValidEmail, ValidCode, 999, CancellationToken.None);

        Assert.False(result);
        Assert.Null(handler.CapturedRequest);
    }

    [Fact]
    public async Task SendAsync_WhenRegistrationPurpose_SendsExpectedResendPayload()
    {
        var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, "{\"id\":\"resend_msg_123\"}");
        using var client = new HttpClient(handler);
        var delivery = new ResendEmailChallengeDelivery(
            client,
            ValidApiKey,
            fromEmail: "MuscleCuties Team <noreply@musclecuties.com>",
            apiEndpoint: "https://api.resend.com/emails");

        var result = await delivery.SendAsync(ValidEmail, ValidCode, RegistrationPurpose, CancellationToken.None);

        Assert.True(result);
        Assert.NotNull(handler.CapturedRequest);
        Assert.Equal(HttpMethod.Post, handler.CapturedRequest!.Method);
        Assert.Equal(new Uri("https://api.resend.com/emails"), handler.CapturedRequest.RequestUri);
        Assert.Equal("Bearer", handler.CapturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal(ValidApiKey, handler.CapturedRequest.Headers.Authorization?.Parameter);

        var json = handler.CapturedBody;
        Assert.NotNull(json);
        using var doc = JsonDocument.Parse(json!);
        var root = doc.RootElement;
        Assert.Equal("MuscleCuties Team <noreply@musclecuties.com>", root.GetProperty("from").GetString());
        Assert.Equal(ValidEmail, root.GetProperty("to")[0].GetString());
        Assert.DoesNotContain(ValidCode, root.GetProperty("subject").GetString()!);
        Assert.Contains(ValidCode, root.GetProperty("text").GetString()!);
        Assert.Contains(ValidCode, root.GetProperty("html").GetString()!);
        Assert.Contains("Confirm Your Email", root.GetProperty("html").GetString()!);
    }

    [Fact]
    public async Task SendAsync_WhenPasswordRecoveryPurpose_SendsExpectedRecoveryPayload()
    {
        var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, "{\"id\":\"resend_msg_456\"}");
        using var client = new HttpClient(handler);
        var delivery = new ResendEmailChallengeDelivery(
            client,
            ValidApiKey,
            fromEmail: "MuscleCuties Security <security@musclecuties.com>",
            apiEndpoint: "https://api.resend.com/emails");

        var result = await delivery.SendAsync(ValidEmail, ValidCode, PasswordRecoveryPurpose, CancellationToken.None);

        Assert.True(result);
        Assert.NotNull(handler.CapturedRequest);

        var json = handler.CapturedBody;
        Assert.NotNull(json);
        using var doc = JsonDocument.Parse(json!);
        var root = doc.RootElement;
        Assert.Contains("Password Reset", root.GetProperty("subject").GetString()!);
        Assert.DoesNotContain(ValidCode, root.GetProperty("subject").GetString()!);
        Assert.Contains(ValidCode, root.GetProperty("text").GetString()!);
        Assert.Contains(ValidCode, root.GetProperty("html").GetString()!);
        Assert.Contains("Reset Your Password", root.GetProperty("html").GetString()!);
    }

    [Fact]
    public async Task SendAsync_WhenResendApiReturnsError_ReturnsFalse()
    {
        var handler = new CapturingHttpMessageHandler(
            HttpStatusCode.BadRequest,
            "{\"statusCode\":400,\"message\":\"Domain not verified\",\"name\":\"validation_error\"}");
        using var client = new HttpClient(handler);
        var delivery = new ResendEmailChallengeDelivery(client, ValidApiKey);

        var result = await delivery.SendAsync(ValidEmail, ValidCode, RegistrationPurpose, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task SendAsync_WhenEndpointIsNotHttps_DoesNotSendCredentialsOrCode()
    {
        var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        var delivery = new ResendEmailChallengeDelivery(
            client, ValidApiKey, apiEndpoint: "http://api.resend.com/emails");

        var result = await delivery.SendAsync(ValidEmail, ValidCode, RegistrationPurpose, CancellationToken.None);

        Assert.False(result);
        Assert.Null(handler.CapturedRequest);
    }

    [Fact]
    public async Task SendAsync_WhenEndpointIsNotResend_DoesNotSendCredentialsOrCode()
    {
        var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        var delivery = new ResendEmailChallengeDelivery(
            client, ValidApiKey, apiEndpoint: "https://untrusted.example/emails");

        var result = await delivery.SendAsync(ValidEmail, ValidCode, RegistrationPurpose, CancellationToken.None);

        Assert.False(result);
        Assert.Null(handler.CapturedRequest);
    }

    [Fact]
    public async Task SendAsync_WhenNetworkThrows_ReturnsFalseSafely()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("Connection refused"));
        using var client = new HttpClient(handler);
        var delivery = new ResendEmailChallengeDelivery(client, ValidApiKey);

        var result = await delivery.SendAsync(ValidEmail, ValidCode, RegistrationPurpose, CancellationToken.None);

        Assert.False(result);
    }

    private sealed class CapturingHttpMessageHandler(HttpStatusCode responseStatus, string responseContent = "") : HttpMessageHandler
    {
        public HttpRequestMessage? CapturedRequest { get; private set; }
        public string? CapturedBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            if (request.Content != null)
            {
                CapturedBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(responseStatus)
            {
                Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class ThrowingHttpMessageHandler(Exception exceptionToThrow) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw exceptionToThrow;
        }
    }
}
