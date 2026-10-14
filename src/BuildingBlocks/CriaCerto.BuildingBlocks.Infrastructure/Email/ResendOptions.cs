namespace CriaCerto.BuildingBlocks.Infrastructure.Email;

public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    public string ApiKey { get; set; } = string.Empty;
    public string DefaultFromEmail { get; set; } = "noreply@criacerto.com.br";
    public string DefaultFromName { get; set; } = "CriaCerto";
    public string BaseUrl { get; set; } = "https://api.resend.com";
    public bool EnableMockInDevelopment { get; set; } = false;
}
