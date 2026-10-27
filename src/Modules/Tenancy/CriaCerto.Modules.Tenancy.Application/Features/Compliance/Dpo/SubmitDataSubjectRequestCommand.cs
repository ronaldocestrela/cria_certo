using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Features.Compliance.Dpo.Models;
using MediatR;

namespace CriaCerto.Modules.Tenancy.Application.Features.Compliance.Dpo;

/// <summary>
/// Comando para submissão de solicitação formal ao Encarregado de Proteção de Dados (DPO - Art. 41).
/// </summary>
public record SubmitDataSubjectRequestCommand(
    string FullName,
    string Email,
    string? PhoneNumber,
    string? DocumentNumber,
    DataSubjectRole RelationshipType,
    DataSubjectRequestType RequestType,
    string Description,
    string? IpAddress = null,
    string? UserAgent = null
) : IRequest<Result<DataSubjectRequestReceiptDto>>;

/// <summary>
/// Payload recebido no endpoint público de solicitação ao DPO.
/// </summary>
public sealed record SubmitDataSubjectRequestRequest(
    string FullName,
    string Email,
    string? PhoneNumber,
    string? DocumentNumber,
    DataSubjectRole RelationshipType,
    DataSubjectRequestType RequestType,
    string Description
);

