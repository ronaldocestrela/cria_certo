using CriaCerto.BuildingBlocks.Abstractions.Email;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using Microsoft.Extensions.Logging;

namespace CriaCerto.BuildingBlocks.Infrastructure.Email;

public sealed class NullEmailSender : IEmailSender
{
    private readonly ILogger<NullEmailSender> _logger;

    public NullEmailSender(ILogger<NullEmailSender> logger)
    {
        _logger = logger;
    }

    public Task<Result<EmailSendResult>> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var recipients = string.Join(", ", message.To.Select(r => r.Email));
        var mockId = $"mock_{Guid.NewGuid():N}";

        _logger.LogInformation("[NullEmailSender] Mock email sent to [{Recipients}]. Subject: '{Subject}'. MockMessageId: {MessageId}",
            recipients, message.Subject, mockId);

        return Task.FromResult(Result.Success(new EmailSendResult(mockId)));
    }
}
