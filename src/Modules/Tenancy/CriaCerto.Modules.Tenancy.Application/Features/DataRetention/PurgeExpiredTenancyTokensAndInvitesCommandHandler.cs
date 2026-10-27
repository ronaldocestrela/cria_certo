using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.DataRetention;

public sealed class PurgeExpiredTenancyTokensAndInvitesCommandHandler
    : IRequestHandler<PurgeExpiredTenancyTokensAndInvitesCommand, Result<TenancyPurgeSummaryDto>>
{
    private readonly ITenancyDbContext _dbContext;

    public PurgeExpiredTenancyTokensAndInvitesCommandHandler(ITenancyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<TenancyPurgeSummaryDto>> Handle(
        PurgeExpiredTenancyTokensAndInvitesCommand request,
        CancellationToken cancellationToken)
    {
        var now = request.ReferenceDateUtc ?? DateTime.UtcNow;
        var tokenCutoff = now.AddDays(-Math.Max(0, request.TokenGraceDays));
        var inviteCutoff = now.AddDays(-Math.Max(0, request.InviteGraceDays));

        // 1. Localizar usuários com tokens elegíveis para expurgo
        var usersWithExpiredTokens = await _dbContext.Users
            .Where(u =>
                (u.EmailConfirmationToken != null && u.EmailConfirmationTokenExpiresAt != null && u.EmailConfirmationTokenExpiresAt < tokenCutoff) ||
                (u.PasswordResetToken != null && u.PasswordResetTokenExpiresAt != null && u.PasswordResetTokenExpiresAt < tokenCutoff))
            .ToListAsync(cancellationToken);

        int clearedEmailTokens = 0;
        int clearedResetTokens = 0;

        foreach (var user in usersWithExpiredTokens)
        {
            if (user.EmailConfirmationToken != null &&
                user.EmailConfirmationTokenExpiresAt.HasValue &&
                user.EmailConfirmationTokenExpiresAt.Value < tokenCutoff)
            {
                clearedEmailTokens++;
            }

            if (user.PasswordResetToken != null &&
                user.PasswordResetTokenExpiresAt.HasValue &&
                user.PasswordResetTokenExpiresAt.Value < tokenCutoff)
            {
                clearedResetTokens++;
            }

            if (!request.DryRun)
            {
                user.ClearExpiredTokens(now, request.TokenGraceDays);
            }
        }

        // 2. Localizar convites de equipe expirados e não aceitos há mais de InviteGraceDays
        var staleInvites = await _dbContext.TeamInvites
            .Where(i => !i.IsAccepted && i.ExpiresAt < inviteCutoff)
            .ToListAsync(cancellationToken);

        int purgedInvitesCount = staleInvites.Count;

        if (!request.DryRun)
        {
            if (staleInvites.Count > 0)
            {
                _dbContext.TeamInvites.RemoveRange(staleInvites);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var summary = new TenancyPurgeSummaryDto(
            EvaluatedUsers: usersWithExpiredTokens.Count,
            ClearedEmailConfirmationTokens: clearedEmailTokens,
            ClearedPasswordResetTokens: clearedResetTokens,
            PurgedTeamInvites: purgedInvitesCount,
            DryRun: request.DryRun,
            ExecutedAtUtc: DateTime.UtcNow
        );

        return Result.Success(summary);
    }
}
