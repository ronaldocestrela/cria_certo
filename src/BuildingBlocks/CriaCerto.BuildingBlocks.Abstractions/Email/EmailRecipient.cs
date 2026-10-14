namespace CriaCerto.BuildingBlocks.Abstractions.Email;

public sealed record EmailRecipient(string Email, string? Name = null);
