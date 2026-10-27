using CriaCerto.BuildingBlocks.Abstractions.Results;
using MediatR;

namespace CriaCerto.Modules.Tenancy.Application.Features.AnonymizeUserAccount;

public sealed record AnonymizeUserAccountRequest(
    string CurrentPassword,
    string ConfirmationText,
    string? ExitReason = null
);

public sealed record AnonymizeUserAccountCommand(
    Guid UserId,
    string CurrentPassword,
    string ConfirmationText,
    string? ExitReason = null,
    string? ClientIpAddress = null
) : IRequest<Result>;
