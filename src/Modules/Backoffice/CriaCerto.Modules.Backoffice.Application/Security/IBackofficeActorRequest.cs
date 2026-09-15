namespace CriaCerto.Modules.Backoffice.Application.Security;

public interface IBackofficeActorRequest
{
    Guid ActorId { get; }
    string ActorEmail { get; }
    string? ActorRole { get; }
}
