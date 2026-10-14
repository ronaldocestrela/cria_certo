using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Email;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriaCerto.BuildingBlocks.Infrastructure.Email;

public sealed class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendEmailSender> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ResendEmailSender(
        HttpClient httpClient,
        IOptions<ResendOptions> options,
        ILogger<ResendEmailSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<EmailSendResult>> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("[ResendEmailSender] Resend API key is missing or not configured.");
            return Result.Failure<EmailSendResult>(EmailErrors.ApiKeyMissing);
        }

        if (message.To == null || message.To.Count == 0)
        {
            return Result.Failure<EmailSendResult>(EmailErrors.InvalidRecipient("A lista de destinatários está vazia."));
        }

        foreach (var recipient in message.To)
        {
            if (string.IsNullOrWhiteSpace(recipient.Email) || !IsValidEmail(recipient.Email))
            {
                return Result.Failure<EmailSendResult>(EmailErrors.InvalidRecipient($"O e-mail '{recipient.Email}' possui formato inválido."));
            }
        }

        var fromEmail = !string.IsNullOrWhiteSpace(message.FromEmail) ? message.FromEmail : _options.DefaultFromEmail;
        var fromName = !string.IsNullOrWhiteSpace(message.FromName) ? message.FromName : _options.DefaultFromName;
        var formattedFrom = !string.IsNullOrWhiteSpace(fromName) ? $"{fromName} <{fromEmail}>" : fromEmail;

        var payload = new ResendEmailPayload
        {
            From = formattedFrom,
            To = message.To.Select(r => !string.IsNullOrWhiteSpace(r.Name) ? $"{r.Name} <{r.Email}>" : r.Email).ToList(),
            Subject = message.Subject,
            Html = message.HtmlBody,
            Text = message.TextBody,
            ReplyTo = message.ReplyTo,
            Headers = message.Headers != null ? new Dictionary<string, string>(message.Headers) : null,
            Tags = message.Tags?.Select(t => new ResendEmailTag { Name = "tag", Value = t }).ToList()
        };

        var endpoint = _httpClient.BaseAddress != null
            ? "emails"
            : $"{_options.BaseUrl.TrimEnd('/')}/emails";

        var requestJson = JsonSerializer.Serialize(payload, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("[ResendEmailSender] Rate limit hit on Resend API (HTTP 429).");
                return Result.Failure<EmailSendResult>(EmailErrors.RateLimited);
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errorMessage = responseBody;
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<ResendErrorResponse>(responseBody, JsonOptions);
                    if (!string.IsNullOrWhiteSpace(errorResponse?.Message))
                    {
                        errorMessage = errorResponse.Message;
                    }
                }
                catch
                {
                    // Fallback to raw responseBody
                }

                _logger.LogError("[ResendEmailSender] Failed to send email via Resend API (HTTP {StatusCode}): {Error}",
                    response.StatusCode, errorMessage);

                return Result.Failure<EmailSendResult>(EmailErrors.ProviderError(errorMessage));
            }

            var successResponse = JsonSerializer.Deserialize<ResendSuccessResponse>(responseBody, JsonOptions);
            var messageId = successResponse?.Id ?? Guid.NewGuid().ToString();

            _logger.LogInformation("[ResendEmailSender] Email sent successfully via Resend. MessageId: {MessageId}", messageId);
            return Result.Success(new EmailSendResult(messageId));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<EmailSendResult>(EmailErrors.NetworkFailure("Operação de envio cancelada."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ResendEmailSender] Unexpected network error while communicating with Resend.");
            return Result.Failure<EmailSendResult>(EmailErrors.NetworkFailure(ex.Message));
        }
    }

    private static bool IsValidEmail(string email)
    {
        var trimmed = email.Trim();
        var atIndex = trimmed.IndexOf('@');
        return atIndex > 0 && atIndex < trimmed.Length - 1 && trimmed.IndexOf('@', atIndex + 1) == -1;
    }
}
