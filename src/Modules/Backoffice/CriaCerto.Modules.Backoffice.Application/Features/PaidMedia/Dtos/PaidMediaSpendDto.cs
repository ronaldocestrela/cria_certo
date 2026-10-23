namespace CriaCerto.Modules.Backoffice.Application.Features.PaidMedia.Dtos;

public record PaidMediaSpendDto(
    Guid Id,
    string Channel,
    DateTime PeriodStartDateUtc,
    DateTime PeriodEndDateUtc,
    decimal Amount,
    int AcquisitionsCount,
    decimal CacDirect,
    string? Notes,
    Guid CreatedByAdminId,
    DateTime CreatedAtUtc
);

public record CreatePaidMediaSpendRequest(
    string Channel,
    DateTime PeriodStartDateUtc,
    DateTime PeriodEndDateUtc,
    decimal Amount,
    int AcquisitionsCount,
    string? Notes
);
