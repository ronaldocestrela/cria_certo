using CriaCerto.BuildingBlocks.Abstractions.Results;
using MediatR;

namespace CriaCerto.Modules.Backoffice.Application.Features.DataRetention.Commands;

public sealed record PurgeExpiredAdminSessionsCommand(
    DateTime? ReferenceDateUtc = null,
    int SessionGraceDays = 7,
    bool DryRun = false
) : IRequest<Result<BackofficePurgeSummaryDto>>;

public sealed record BackofficePurgeSummaryDto(
    int EvaluatedSessions,
    int PurgedRevokedSessions,
    int PurgedExpiredSessions,
    int TotalPurged,
    bool DryRun,
    DateTime ExecutedAtUtc
);
