using CriaCerto.Modules.Referral.Application.Features.Payouts.Dtos;

namespace CriaCerto.Modules.Referral.Application.Features.Payouts.Services;

public interface IPixBatchCsvGenerator
{
    byte[] GenerateCsv(IReadOnlyList<AvailablePartnerPayoutDto> payouts);
}
