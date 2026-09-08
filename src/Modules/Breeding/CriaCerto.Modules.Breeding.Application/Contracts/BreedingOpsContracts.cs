using CriaCerto.Modules.Breeding.Application.Domain;

namespace CriaCerto.Modules.Breeding.Application.Contracts;

public sealed record RegisterIatfProtocolRequest(
    string Name,
    DateTime StartDate,
    DateTime InseminationDate,
    Guid SemenBatchId,
    IReadOnlyList<Guid> CowIds,
    Guid? BullId = null);

public sealed record RegisterPregnancyDiagnosisRequest(
    Guid CowId,
    DateTime DiagnosisDate,
    DiagnosisMethod Method,
    bool IsPregnant,
    int? GestationalAgeDays,
    string? Notes);

public sealed record PregnancyDiagnosisDto(
    Guid Id,
    Guid CowId,
    string CowEarTag,
    DateTime DiagnosisDate,
    DiagnosisMethod Method,
    bool IsPregnant,
    int? GestationalAgeDays,
    string? Notes);
