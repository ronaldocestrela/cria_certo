using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Impersonation.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Impersonation.Queries;

public record GetActiveImpersonationSessionQuery(Guid AdminUserId) : IRequest<Result<ImpersonationSessionDto?>>;

public class GetActiveImpersonationSessionQueryHandler : IRequestHandler<GetActiveImpersonationSessionQuery, Result<ImpersonationSessionDto?>>
{
    private readonly DbContext _dbContext;

    public GetActiveImpersonationSessionQueryHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ImpersonationSessionDto?>> Handle(
        GetActiveImpersonationSessionQuery query,
        CancellationToken cancellationToken)
    {
        var session = await _dbContext.Set<ImpersonationSession>()
            .Where(s => s.AdminUserId == query.AdminUserId && s.Status == ImpersonationSessionStatus.Active)
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return Result.Success<ImpersonationSessionDto?>(null);
        }

        if (!session.IsActive())
        {
            session.MarkAsExpired();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success<ImpersonationSessionDto?>(null);
        }

        var dto = new ImpersonationSessionDto(
            session.Id,
            string.Empty, // Token is not re-exposed in query for security
            session.TargetTenantId,
            session.TargetTenantName,
            session.TargetUserId,
            session.TargetUserEmail,
            session.SupportTicket,
            session.Justification,
            session.StartedAtUtc,
            session.ExpiresAtUtc,
            session.GetRemainingSeconds(),
            session.Status.ToString());

        return Result.Success<ImpersonationSessionDto?>(dto);
    }
}
