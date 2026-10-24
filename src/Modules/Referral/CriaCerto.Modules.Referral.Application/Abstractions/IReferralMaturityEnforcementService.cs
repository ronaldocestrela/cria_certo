using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Features.Enforcement.Dtos;

namespace CriaCerto.Modules.Referral.Application.Abstractions;

public interface IReferralMaturityEnforcementService
{
    Task<Result<ReferralEnforcementSummaryDto>> ExecutePassAsync(DateTime? referenceDateUtc = null, CancellationToken cancellationToken = default);
}
