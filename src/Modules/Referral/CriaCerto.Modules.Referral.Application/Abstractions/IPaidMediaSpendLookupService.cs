namespace CriaCerto.Modules.Referral.Application.Abstractions;

public record PaidMediaSummaryDto(decimal TotalSpent, int TotalAcquisitions);

public interface IPaidMediaSpendLookupService
{
    Task<PaidMediaSummaryDto> GetPaidMediaSummaryAsync(DateTime? startDateUtc, DateTime? endDateUtc, CancellationToken cancellationToken = default);
}
