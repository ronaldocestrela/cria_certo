using CriaCerto.Modules.Breeding.Application.Domain;

namespace CriaCerto.Modules.Breeding.Application.Contracts;

public sealed record CattleListResponse<TAnimal>(IReadOnlyList<TAnimal> Items, int TotalCount, int Page, int PageSize);

public sealed record TimelineEventDto(
    DateTime EventDate,
    string EventType,
    string Title,
    string Description,
    string Category,
    string IconName);

public sealed record CowSummaryDto(
    Guid Id,
    string EarTag,
    string? SisbovId,
    string? RfidTag,
    string? Nickname,
    string Breed,
    string Category,
    ReproductiveStatus Status,
    int ParityCount,
    DateTime? LastCalvingDate,
    double? IepMonths,
    decimal? BodyConditionScore);

public sealed record CowDetailDto(
    Guid Id,
    string EarTag,
    string? SisbovId,
    string? RfidTag,
    string? Tattoo,
    string? Nickname,
    string? RegistryNumber,
    string Breed,
    string Origin,
    DateTime? BirthDate,
    DateTime? EntryDate,
    decimal? EntryWeightKg,
    string? SireInfo,
    string? DamInfo,
    decimal? BodyConditionScore,
    string Category,
    ReproductiveStatus Status,
    int ParityCount,
    DateTime? LastCalvingDate,
    double? IepMonths,
    int? OpenDays,
    IReadOnlyList<TimelineEventDto> Timeline);

public sealed record BullSummaryDto(
    Guid Id,
    string EarTag,
    string Name,
    string Breed,
    string? RegistryNumber,
    bool IsActive);

public sealed record SemenBatchDto(
    Guid Id,
    string BatchCode,
    string BullName,
    string Breed,
    int StrawQuantity,
    SemenType Type);

public sealed record IatfProtocolDto(
    Guid Id,
    string Name,
    DateTime StartDate,
    DateTime InseminationDate,
    Guid SemenBatchId,
    int CowCount);
