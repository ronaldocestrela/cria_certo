using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using CriaCerto.Modules.Referral.Application.Domain.Services;
using CriaCerto.Modules.Referral.Application.Features.CustomerReferral.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.CustomerReferral.Commands;

public sealed record CustomizeReferralCodeCommand(Guid TenantId, string NewCode) : IRequest<Result<CustomizeReferralCodeResponse>>;

public sealed class CustomizeReferralCodeCommandHandler : IRequestHandler<CustomizeReferralCodeCommand, Result<CustomizeReferralCodeResponse>>
{
    private const string BaseReferralUrl = "https://criacerto.com.br/r/";
    private readonly IReferralDbContext _dbContext;

    public CustomizeReferralCodeCommandHandler(IReferralDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<CustomizeReferralCodeResponse>> Handle(
        CustomizeReferralCodeCommand request,
        CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty)
        {
            return Result.Failure<CustomizeReferralCodeResponse>(ReferralErrors.Participant.TenantRequiredForCustomer);
        }

        if (string.IsNullOrWhiteSpace(request.NewCode) || request.NewCode.Trim().Length < 3)
        {
            return Result.Failure<CustomizeReferralCodeResponse>(ReferralErrors.Code.InvalidFormat);
        }

        var normalizedCode = ReferralCodeNormalizer.NormalizeSlug(request.NewCode);
        if (string.IsNullOrWhiteSpace(normalizedCode) || normalizedCode.Length < 3)
        {
            return Result.Failure<CustomizeReferralCodeResponse>(ReferralErrors.Code.InvalidFormat);
        }

        var participant = await _dbContext.Participants
            .FirstOrDefaultAsync(p => p.TenantId == request.TenantId, cancellationToken);

        if (participant == null)
        {
            return Result.Failure<CustomizeReferralCodeResponse>(ReferralErrors.Participant.NotFound);
        }

        var currentCode = await _dbContext.ReferralCodes
            .FirstOrDefaultAsync(c => c.ParticipantId == participant.Id && c.IsActive, cancellationToken);

        if (currentCode != null && currentCode.Code == normalizedCode)
        {
            return Result.Success(new CustomizeReferralCodeResponse(normalizedCode, $"{BaseReferralUrl}{normalizedCode}"));
        }

        // Verifica unicidade global do código
        var isCodeTaken = await _dbContext.ReferralCodes
            .AnyAsync(c => c.Code == normalizedCode && c.ParticipantId != participant.Id, cancellationToken);

        if (isCodeTaken)
        {
            return Result.Failure<CustomizeReferralCodeResponse>(ReferralErrors.Code.AlreadyExists);
        }

        if (currentCode != null)
        {
            var updateResult = currentCode.UpdateCode(normalizedCode);
            if (updateResult.IsFailure)
            {
                return Result.Failure<CustomizeReferralCodeResponse>(updateResult.Error);
            }
        }
        else
        {
            var createResult = ReferralCode.Create(participant.Id, normalizedCode, "Link Personalizado");
            if (createResult.IsFailure)
            {
                return Result.Failure<CustomizeReferralCodeResponse>(createResult.Error);
            }
            _dbContext.ReferralCodes.Add(createResult.Value);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CustomizeReferralCodeResponse(normalizedCode, $"{BaseReferralUrl}{normalizedCode}"));
    }
}
