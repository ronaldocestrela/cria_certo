namespace CriaCerto.Modules.Backoffice.Application.Domain.Services;

/// <summary>
/// Serviço de mascaramento determinístico e higienização de dados pessoais (PII)
/// para conformidade com a LGPD (Lei 13.709/2018) e minimização de acesso.
/// </summary>
public interface IPiiDataMasker
{
    string MaskCpf(string? cpf);
    string MaskCnpj(string? cnpj);
    string MaskDocument(string? document);
    string MaskEmail(string? email);
    string MaskPhone(string? phone);
    string MaskIpAddress(string? ipAddress);
    string MaskPersonName(string? name);
    string SanitizeJsonDetails(string? json);
}
