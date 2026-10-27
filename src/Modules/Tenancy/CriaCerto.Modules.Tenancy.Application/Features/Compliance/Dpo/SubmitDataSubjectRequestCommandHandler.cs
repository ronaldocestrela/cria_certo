using System.Security.Cryptography;
using CriaCerto.BuildingBlocks.Abstractions.Email;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Features.Compliance.Dpo.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CriaCerto.Modules.Tenancy.Application.Features.Compliance.Dpo;

public class SubmitDataSubjectRequestCommandHandler : IRequestHandler<SubmitDataSubjectRequestCommand, Result<DataSubjectRequestReceiptDto>>
{
    private const string DpoOfficialEmail = "dpo@criacerto.com.br";
    private const int LegalResponseBusinessDays = 15;

    private readonly IEmailSender _emailSender;
    private readonly ITenancyAuditSink _auditSink;
    private readonly ILogger<SubmitDataSubjectRequestCommandHandler> _logger;

    public SubmitDataSubjectRequestCommandHandler(
        IEmailSender emailSender,
        ITenancyAuditSink auditSink,
        ILogger<SubmitDataSubjectRequestCommandHandler> logger)
    {
        _emailSender = emailSender;
        _auditSink = auditSink;
        _logger = logger;
    }

    public async Task<Result<DataSubjectRequestReceiptDto>> Handle(
        SubmitDataSubjectRequestCommand command,
        CancellationToken cancellationToken)
    {
        var submittedAtUtc = DateTime.UtcNow;
        var protocolNumber = GenerateProtocolNumber(submittedAtUtc);
        var maxResponseDateUtc = CalculateBusinessDaysDeadline(submittedAtUtc, LegalResponseBusinessDays);

        var confirmationMessage = $"Sua solicitação foi registrada com sucesso sob o protocolo {protocolNumber}. " +
                                  $"Nosso Encarregado de Proteção de Dados (DPO) analisará o pedido no prazo legal de até {LegalResponseBusinessDays} dias úteis (até {maxResponseDateUtc:dd/MM/yyyy}).";

        var receipt = new DataSubjectRequestReceiptDto(
            ProtocolNumber: protocolNumber,
            DataSubjectName: command.FullName.Trim(),
            DataSubjectEmail: command.Email.Trim().ToLowerInvariant(),
            RelationshipType: command.RelationshipType,
            RequestType: command.RequestType,
            SubmittedAtUtc: submittedAtUtc,
            MaxResponseDateUtc: maxResponseDateUtc,
            EstimatedBusinessDays: LegalResponseBusinessDays,
            DpoEmail: DpoOfficialEmail,
            ConfirmationMessage: confirmationMessage
        );

        // 1. Registro de Auditoria Forense SHA-256 no AuditLog
        await _auditSink.RecordDataSubjectRequestSubmittedAuditAsync(
            protocolNumber: protocolNumber,
            dataSubjectEmail: receipt.DataSubjectEmail,
            requestType: command.RequestType.ToString(),
            relationshipType: command.RelationshipType.ToString(),
            ipAddress: string.IsNullOrWhiteSpace(command.IpAddress) ? "127.0.0.1" : command.IpAddress,
            cancellationToken: cancellationToken
        );

        // 2. Disparo de E-mails Transacionais (Titular e DPO) com resiliência
        await DispatchTransactionalEmailsAsync(command, receipt, cancellationToken);

        _logger.LogInformation(
            "Solicitação formal ao DPO registrada com sucesso. Protocolo: {Protocol}, Titular: {Email}, Tipo: {Type}",
            protocolNumber, receipt.DataSubjectEmail, command.RequestType);

        return Result.Success(receipt);
    }

    private async Task DispatchTransactionalEmailsAsync(
        SubmitDataSubjectRequestCommand command,
        DataSubjectRequestReceiptDto receipt,
        CancellationToken cancellationToken)
    {
        try
        {
            // Email 1: Recibo formal para o titular
            var userEmailBody = $@"
Olá, {receipt.DataSubjectName},

Confirmamos o recebimento da sua solicitação formal de direitos de privacidade (LGPD Art. 18) perante o CriaCerto Bovino SaaS.

• Protocolo Oficial: {receipt.ProtocolNumber}
• Tipo de Solicitação: {receipt.RequestType}
• Data de Abertura: {receipt.SubmittedAtUtc:dd/MM/yyyy HH:mm} UTC
• Prazo Regulamentar de Atendimento (LGPD Art. 19, II): até 15 dias úteis ({receipt.MaxResponseDateUtc:dd/MM/yyyy})

Resumo do seu pedido:
""{command.Description.Trim()}""

Nosso Encarregado de Dados (DPO) já está ciente e adotará as providências cabíveis. Caso necessite complementar informações, responda diretamente a este e-mail ou contate {DpoOfficialEmail} citando o número de protocolo acima.

Atenciosamente,
Encarregado pelo Tratamento de Dados Pessoais (DPO)
CriaCerto Bovino SaaS
";

            var userEmail = EmailMessage.Create(
                toEmail: receipt.DataSubjectEmail,
                subject: $"[CriaCerto] Protocolo de Atendimento LGPD - {receipt.ProtocolNumber}",
                htmlBody: $"<pre style=\"font-family: sans-serif; white-space: pre-wrap;\">{userEmailBody}</pre>",
                textBody: userEmailBody,
                toName: receipt.DataSubjectName
            );

            var userSendResult = await _emailSender.SendAsync(userEmail, cancellationToken);
            if (userSendResult.IsFailure)
            {
                _logger.LogWarning("Não foi possível enviar e-mail de confirmação DSR ao titular {Email}: {Error}",
                    receipt.DataSubjectEmail, userSendResult.Error.Message);
            }

            // Email 2: Alerta operacional imediato para a caixa do DPO
            var dpoEmailBody = $@"
ALERTA DE NOVA REQUISIÇÃO DE TITULAR (DSR - LGPD Art. 41)

• Protocolo: {receipt.ProtocolNumber}
• Titular: {receipt.DataSubjectName} ({receipt.DataSubjectEmail})
• Telefone: {command.PhoneNumber ?? "Não informado"}
• Documento/CPF: {command.DocumentNumber ?? "Não informado"}
• Relação com a Plataforma: {receipt.RelationshipType}
• Tipo de Requisição: {receipt.RequestType}
• Data de Entrada: {receipt.SubmittedAtUtc:dd/MM/yyyy HH:mm:ss} UTC
• Data Limite de Resposta (15 dias úteis): {receipt.MaxResponseDateUtc:dd/MM/yyyy}
• IP de Origem: {command.IpAddress ?? "Desconhecido"}

Descrição enviada pelo titular:
--------------------------------------------------------------------------------
{command.Description.Trim()}
--------------------------------------------------------------------------------
";

            var dpoEmail = EmailMessage.Create(
                toEmail: DpoOfficialEmail,
                subject: $"[DSR ALERTA] Requisição LGPD - {receipt.ProtocolNumber} - {receipt.RequestType}",
                htmlBody: $"<pre style=\"font-family: sans-serif; white-space: pre-wrap;\">{dpoEmailBody}</pre>",
                textBody: dpoEmailBody,
                toName: "Encarregado DPO CriaCerto"
            );

            var dpoSendResult = await _emailSender.SendAsync(dpoEmail, cancellationToken);
            if (dpoSendResult.IsFailure)
            {
                _logger.LogWarning("Não foi possível enviar e-mail de alerta DSR à caixa do DPO ({DpoEmail}): {Error}",
                    DpoOfficialEmail, dpoSendResult.Error.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao despachar e-mails transacionais para protocolo {Protocol}", receipt.ProtocolNumber);
        }
    }

    private static string GenerateProtocolNumber(DateTime submittedAtUtc)
    {
        var randomBytes = new byte[2];
        RandomNumberGenerator.Fill(randomBytes);
        var hexSuffix = Convert.ToHexString(randomBytes).ToUpperInvariant();
        return $"DSR-{submittedAtUtc:yyyyMMdd}-{hexSuffix}";
    }

    public static DateTime CalculateBusinessDaysDeadline(DateTime startDateUtc, int businessDays)
    {
        var current = startDateUtc.Date;
        var addedDays = 0;

        while (addedDays < businessDays)
        {
            current = current.AddDays(1);
            if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
            {
                addedDays++;
            }
        }

        return current;
    }
}
