using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.BuildingBlocks.Abstractions.Email;

public interface IEmailSender
{
    Task<Result<EmailSendResult>> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
