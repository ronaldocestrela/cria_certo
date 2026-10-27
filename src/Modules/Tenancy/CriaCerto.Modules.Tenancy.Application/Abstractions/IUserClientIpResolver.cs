namespace CriaCerto.Modules.Tenancy.Application.Abstractions;

public interface IUserClientIpResolver
{
    /// <summary>
    /// Resolve o endereço IP do cliente a partir do contexto HTTP ou cabeçalhos de rede.
    /// </summary>
    string? ResolveClientIp();
}
