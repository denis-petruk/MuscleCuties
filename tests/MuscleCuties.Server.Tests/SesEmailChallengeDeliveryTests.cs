using Amazon.Runtime;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MuscleCuties.Infrastructure.PostgreSql.Account.EmailChallenge;
using Xunit;

namespace MuscleCuties.Server.Tests;

public sealed class SesEmailChallengeDeliveryTests
{
    private const string ValidFromAddress = "MuscleCuties <noreply@musclecuties.app>";
    private const string ValidEmail = "athlete@musclecuties.app";
    private const string ValidCode = "749201";
    private const int RegistrationPurpose = 1;
    private const int PasswordRecoveryPurpose = 2;

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SendAsync_WhenFromAddressMissing_ReturnsFalse(string? fromAddress)
    {
        var transport = new CapturingSesTransport();
        var delivery = new SesEmailChallengeDelivery(transport, fromAddress);

        var result = await delivery.SendAsync(ValidEmail, ValidCode, RegistrationPurpose, CancellationToken.None);

        Assert.False(result);
        Assert.Null(transport.CapturedRequest);
    }

    [Theory]
    [InlineData("", ValidCode)]
    [InlineData("not-an-email", "123456")]
    [InlineData("two@at@example.com", "123456")]
    [InlineData(ValidEmail, "12345")]
    [InlineData(ValidEmail, "1234567")]
    [InlineData(ValidEmail, "12a456")]
    [InlineData(ValidEmail, "")]
    public async Task SendAsync_WhenEmailOrCodeInvalid_ReturnsFalse(string email, string code)
    {
        var transport = new CapturingSesTransport();
        var delivery = new SesEmailChallengeDelivery(transport, ValidFromAddress);

        var result = await delivery.SendAsync(email, code, RegistrationPurpose, CancellationToken.None);

        Assert.False(result);
        Assert.Null(transport.CapturedRequest);
    }

    [Fact]
    public async Task SendAsync_WhenUnknownPurpose_ReturnsFalse()
    {
        var transport = new CapturingSesTransport();
        var delivery = new SesEmailChallengeDelivery(transport, ValidFromAddress);

        var result = await delivery.SendAsync(ValidEmail, ValidCode, 999, CancellationToken.None);

        Assert.False(result);
        Assert.Null(transport.CapturedRequest);
    }

    [Fact]
    public async Task SendAsync_WhenRegistrationPurpose_SendsExpectedSesRequest()
    {
        var transport = new CapturingSesTransport();
        var delivery = new SesEmailChallengeDelivery(
            transport,
            "MuscleCuties Team <noreply@musclecuties.app>",
            configurationSetName: "musclecuties-email-challenges");

        var result = await delivery.SendAsync(ValidEmail, ValidCode, RegistrationPurpose, CancellationToken.None);

        Assert.True(result);
        var request = Assert.IsType<SendEmailRequest>(transport.CapturedRequest);
        Assert.Equal("MuscleCuties Team <noreply@musclecuties.app>", request.FromEmailAddress);
        Assert.Equal("musclecuties-email-challenges", request.ConfigurationSetName);
        Assert.Equal(ValidEmail, Assert.Single(request.Destination.ToAddresses));

        var simple = Assert.IsType<Message>(request.Content.Simple);
        Assert.DoesNotContain(ValidCode, simple.Subject.Data);
        Assert.Equal("UTF-8", simple.Subject.Charset);
        Assert.Contains(ValidCode, simple.Body.Text.Data);
        Assert.Contains(ValidCode, simple.Body.Html.Data);
        Assert.Contains("Confirm Your Email", simple.Body.Html.Data);
        Assert.Equal("UTF-8", simple.Body.Html.Charset);
    }

    [Fact]
    public async Task SendAsync_WhenPasswordRecoveryPurpose_SendsExpectedSesRequest()
    {
        var transport = new CapturingSesTransport();
        var delivery = new SesEmailChallengeDelivery(
            transport,
            ValidFromAddress,
            configurationSetName: "musclecuties-email-challenges");

        var result = await delivery.SendAsync(ValidEmail, ValidCode, PasswordRecoveryPurpose, CancellationToken.None);

        Assert.True(result);
        var request = Assert.IsType<SendEmailRequest>(transport.CapturedRequest);
        Assert.Equal("musclecuties-email-challenges", request.ConfigurationSetName);

        var simple = Assert.IsType<Message>(request.Content.Simple);
        Assert.Contains("Password Reset", simple.Subject.Data);
        Assert.DoesNotContain(ValidCode, simple.Subject.Data);
        Assert.Contains(ValidCode, simple.Body.Text.Data);
        Assert.Contains(ValidCode, simple.Body.Html.Data);
        Assert.Contains("Reset Your Password", simple.Body.Html.Data);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SendAsync_WhenConfigurationSetIsNotConfigured_OmitsIt(string? configurationSetName)
    {
        // An environment may omit the configuration set entirely; the send must still go out and must
        // not carry an empty configuration set name, which SES would reject as unknown.
        var transport = new CapturingSesTransport();
        var delivery = new SesEmailChallengeDelivery(transport, ValidFromAddress, configurationSetName);

        var result = await delivery.SendAsync(ValidEmail, ValidCode, RegistrationPurpose, CancellationToken.None);

        Assert.True(result);
        Assert.Null(Assert.IsType<SendEmailRequest>(transport.CapturedRequest).ConfigurationSetName);
    }

    [Fact]
    public async Task SendAsync_WhenSesRejectsTheMessage_ReturnsFalse()
    {
        var transport = new ThrowingSesTransport(
            new AmazonSimpleEmailServiceV2Exception("Email address is not verified."));
        var delivery = new SesEmailChallengeDelivery(transport, ValidFromAddress);

        var result = await delivery.SendAsync(ValidEmail, ValidCode, RegistrationPurpose, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task SendAsync_WhenNetworkThrows_ReturnsFalseSafely()
    {
        var transport = new ThrowingSesTransport(new HttpRequestException("Connection refused"));
        var delivery = new SesEmailChallengeDelivery(transport, ValidFromAddress);

        var result = await delivery.SendAsync(ValidEmail, ValidCode, RegistrationPurpose, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task SendAsync_WhenCredentialsAreUnavailable_ReturnsFalseSafely()
    {
        // A missing or unreachable credential source throws AmazonClientException, not
        // AmazonServiceException; registration must degrade to "delivery unavailable" rather than 500.
        var transport = new ThrowingSesTransport(new AmazonClientException("Unable to find credentials"));
        var delivery = new SesEmailChallengeDelivery(transport, ValidFromAddress);

        var result = await delivery.SendAsync(ValidEmail, ValidCode, RegistrationPurpose, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task SendAsync_WhenSendFails_LogsTheFailureWithoutRecipientOrCode()
    {
        var logger = new CapturingLogger<SesEmailChallengeDelivery>();
        var transport = new ThrowingSesTransport(new HttpRequestException("Connection refused"));
        var delivery = new SesEmailChallengeDelivery(transport, ValidFromAddress, logger: logger);

        var result = await delivery.SendAsync(ValidEmail, ValidCode, RegistrationPurpose, CancellationToken.None);

        Assert.False(result);
        var entry = Assert.Single(logger.Entries);
        Assert.Contains("HttpRequestException", entry, StringComparison.Ordinal);
        Assert.DoesNotContain(ValidEmail, entry, StringComparison.Ordinal);
        Assert.DoesNotContain(ValidCode, entry, StringComparison.Ordinal);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => new NoopScope();

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(formatter(state, exception));

        private sealed class NoopScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class CapturingSesTransport : ISesEmailTransport
    {
        public SendEmailRequest? CapturedRequest { get; private set; }

        public Task SendAsync(SendEmailRequest request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingSesTransport(Exception exceptionToThrow) : ISesEmailTransport
    {
        public Task SendAsync(SendEmailRequest request, CancellationToken cancellationToken) =>
            throw exceptionToThrow;
    }
}
