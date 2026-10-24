using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Services;
using CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Queries;

public record SuggestReferralCodeQuery(string Name) : IRequest<Result<SuggestReferralCodeDto>>;

public sealed class SuggestReferralCodeQueryHandler
    : IRequestHandler<SuggestReferralCodeQuery, Result<SuggestReferralCodeDto>>
{
    private readonly IReferralDbContext _dbContext;

    public SuggestReferralCodeQueryHandler(IReferralDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<SuggestReferralCodeDto>> Handle(
        SuggestReferralCodeQuery request,
        CancellationToken cancellationToken)
    {
        var slug = ReferralCodeNormalizer.NormalizeSlug(request.Name);

        var exists = await _dbContext.ReferralCodes
            .AnyAsync(c => c.Code == slug, cancellationToken);

        if (!exists)
        {
            return Result.Success(new SuggestReferralCodeDto(slug, true));
        }

        // Se já existe, sugere com sufixo randômico único
        var uniqueSlug = $"{slug}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
        return Result.Success(new SuggestReferralCodeDto(uniqueSlug, true));
    }
}
