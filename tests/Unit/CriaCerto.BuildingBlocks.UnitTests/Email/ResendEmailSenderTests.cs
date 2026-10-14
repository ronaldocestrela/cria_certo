using System.Net;
using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Email;
using CriaCerto.BuildingBlocks.Infrastructure.Email;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CriaCerto.BuildingBlocks.UnitTests.Email;

public class ResendEmailSenderTests
{
    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? CapturedRequest { get; private set; }
        public string? CapturedRequestBody { get; private set; }
        private readonly HttpResponseMessage _responseToReturn;
        private readonly Exception? _exceptionToThrow;

        public FakeHttpMessageHandler(HttpResponseMessage responseToReturn)
        {
            _responseToReturn = responseToReturn;
        }

        public FakeHttpMessageHandler(Exception exceptionToThrow)
        {
            _exceptionToThrow = exceptionToThrow;
            _responseToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            if (request.Content != null)
            {
                CapturedRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            if (_exceptionToThrow != null)
            {
                throw _exceptionToThrow;
            }

            return _responseToReturn;
        }
    }

    [Fact]
    public async Task SendAsync_WhenApiKeyIsMissing_ShouldReturnApiKeyMissingError()
    {
        // Arrange
        var options = Options.Create(new ResendOptions { ApiKey = "" });
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var sender = new ResendEmailSender(httpClient, options, NullLogger<ResendEmailSender>.Instance);
        var message = EmailMessage.Create("produtor@fazenda.com.br", "Assunto", "<p>Corpo</p>");

        // Act
        var result = await sender.SendAsync(message);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Email.ApiKeyMissing");
    }

    [Fact]
    public async Task SendAsync_WhenRecipientIsEmpty_ShouldReturnInvalidRecipientError()
    {
        // Arrange
        var options = Options.Create(new ResendOptions { ApiKey = "re_test_key" });
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var sender = new ResendEmailSender(httpClient, options, NullLogger<ResendEmailSender>.Instance);
        var message = new EmailMessage(
            To: Array.Empty<EmailRecipient>(),
            Subject: "Assunto",
            HtmlBody: "<p>Corpo</p>");

        // Act
        var result = await sender.SendAsync(message);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Email.InvalidRecipient");
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@sem-usuario.com")]
    [InlineData("usuario@")]
    public async Task SendAsync_WhenRecipientEmailIsInvalid_ShouldReturnInvalidRecipientError(string invalidEmail)
    {
        // Arrange
        var options = Options.Create(new ResendOptions { ApiKey = "re_test_key" });
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var sender = new ResendEmailSender(httpClient, options, NullLogger<ResendEmailSender>.Instance);
        var message = EmailMessage.Create(invalidEmail, "Assunto", "<p>Corpo</p>");

        // Act
        var result = await sender.SendAsync(message);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Email.InvalidRecipient");
    }

    [Fact]
    public async Task SendAsync_WhenApiReturns200Ok_ShouldReturnSuccessWithMessageId()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new { id = "msg_resend_999" });
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        };
        var handler = new FakeHttpMessageHandler(response);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com") };
        var options = Options.Create(new ResendOptions
        {
            ApiKey = "re_live_valid_key",
            DefaultFromEmail = "noreply@criacerto.com.br",
            DefaultFromName = "CriaCerto Agro"
        });
        var sender = new ResendEmailSender(httpClient, options, NullLogger<ResendEmailSender>.Instance);
        var message = EmailMessage.Create("fazendeiro@pantanal.com.br", "Código de Acesso", "<p>123456</p>");

        // Act
        var result = await sender.SendAsync(message);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MessageId.Should().Be("msg_resend_999");
        handler.CapturedRequest.Should().NotBeNull();
        handler.CapturedRequest!.Headers.Authorization.Should().NotBeNull();
        handler.CapturedRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.CapturedRequest!.Headers.Authorization!.Parameter.Should().Be("re_live_valid_key");
        handler.CapturedRequestBody.Should().Contain("fazendeiro@pantanal.com.br");
        handler.CapturedRequestBody.Should().Contain("CriaCerto Agro <noreply@criacerto.com.br>");
    }

    [Fact]
    public async Task SendAsync_WhenApiReturns429TooManyRequests_ShouldReturnRateLimitedError()
    {
        // Arrange
        var response = new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = new StringContent("{\"message\":\"Rate limit exceeded\"}", System.Text.Encoding.UTF8, "application/json")
        };
        var handler = new FakeHttpMessageHandler(response);
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new ResendOptions { ApiKey = "re_valid_key" });
        var sender = new ResendEmailSender(httpClient, options, NullLogger<ResendEmailSender>.Instance);
        var message = EmailMessage.Create("contato@fazenda.com.br", "Assunto", "<p>Corpo</p>");

        // Act
        var result = await sender.SendAsync(message);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Email.RateLimited");
    }

    [Fact]
    public async Task SendAsync_WhenApiReturns401Unauthorized_ShouldReturnProviderError()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"name\":\"missing_api_key\",\"message\":\"Missing API key\"}", System.Text.Encoding.UTF8, "application/json")
        };
        var handler = new FakeHttpMessageHandler(response);
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new ResendOptions { ApiKey = "re_invalid_key" });
        var sender = new ResendEmailSender(httpClient, options, NullLogger<ResendEmailSender>.Instance);
        var message = EmailMessage.Create("contato@fazenda.com.br", "Assunto", "<p>Corpo</p>");

        // Act
        var result = await sender.SendAsync(message);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Email.ProviderError");
        result.Error.Message.Should().Contain("Missing API key");
    }

    [Fact]
    public async Task SendAsync_WhenHttpTimesOut_ShouldReturnNetworkFailureError()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(new HttpRequestException("Connection refused"));
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new ResendOptions { ApiKey = "re_valid_key" });
        var sender = new ResendEmailSender(httpClient, options, NullLogger<ResendEmailSender>.Instance);
        var message = EmailMessage.Create("contato@fazenda.com.br", "Assunto", "<p>Corpo</p>");

        // Act
        var result = await sender.SendAsync(message);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Email.NetworkFailure");
        result.Error.Message.Should().Contain("Connection refused");
    }

    [Fact]
    public async Task NullEmailSender_SendAsync_ShouldReturnSuccessWithMockId()
    {
        // Arrange
        var sender = new NullEmailSender(NullLogger<NullEmailSender>.Instance);
        var message = EmailMessage.Create("mock@fazenda.com.br", "Teste", "<p>Olá</p>");

        // Act
        var result = await sender.SendAsync(message);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MessageId.Should().StartWith("mock_");
    }
}
