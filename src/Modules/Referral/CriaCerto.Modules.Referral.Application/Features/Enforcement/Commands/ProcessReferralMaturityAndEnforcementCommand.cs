using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Features.Enforcement.Dtos;
using MediatR;

namespace CriaCerto.Modules.Referral.Application.Features.Enforcement.Commands;

public sealed record ProcessReferralMaturityAndEnforcementCommand(DateTime? ReferenceDateUtc = null)
    : IRequest<Result<ReferralEnforcementSummaryDto>>;

public sealed class ProcessReferralMaturityAndEnforcementCommandHandler
    : IRequestHandler<ProcessReferralMaturityAndEnforcementCommand, Result<ReferralEnforcementSummaryDto>>
{
    private readonly IReferralMaturityEnforcementService _enforcementService;

    public ProcessReferralMaturityAndEnforcementCommandHandler(IReferralMaturityEnforcementService enforcementService)
    {
        _enforcementService = enforcementService;
    }

    public async Task<Result<ReferralEnforcementSummaryDto>> Handle(
        ProcessReferralMaturityAndEnforcementCommand request,
        CancellationToken cancellationToken)
    {
        return await _enforcementService.ExecutePassAsync(request.ReferenceDateUtc, cancellationToken);
    }
}
