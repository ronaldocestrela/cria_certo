namespace CriaCerto.BuildingBlocks.Abstractions.Email;

public sealed record EmailMessage(
    IReadOnlyList<EmailRecipient> To,
    string Subject,
    string? HtmlBody = null,
    string? TextBody = null,
    string? FromEmail = null,
    string? FromName = null,
    string? ReplyTo = null,
    IReadOnlyDictionary<string, string>? Headers = null,
    IReadOnlyList<string>? Tags = null)
{
    public static EmailMessage Create(
        string toEmail,
        string subject,
        string htmlBody,
        string? textBody = null,
        string? toName = null,
        string? fromEmail = null,
        string? fromName = null)
    {
        return new EmailMessage(
            new[] { new EmailRecipient(toEmail, toName) },
            subject,
            HtmlBody: htmlBody,
            TextBody: textBody,
            FromEmail: fromEmail,
            FromName: fromName);
    }
}
