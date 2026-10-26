using CriaCerto.BuildingBlocks.Abstractions.Results;
using MediatR;

namespace CriaCerto.Modules.Tenancy.Application.Features.DataRetention;

public sealed record PurgeExpiredTenancyTokensAndInvitesCommand(
    DateTime? ReferenceDateUtc = null,
    int TokenGraceDays = 7,
    int InviteGraceDays = 30,
    bool DryRun = false
) : IRequest<Result<TenancyPurgeSummaryDto>>;

public sealed record TenancyPurgeSummaryDto(
    int EvaluatedUsers,
    int ClearedEmailConfirmationTokens,
    int ClearedPasswordResetTokens,
    int PurgedTeamInvites,
    bool DryRun,
    DateTime ExecutedAtUtc
);
