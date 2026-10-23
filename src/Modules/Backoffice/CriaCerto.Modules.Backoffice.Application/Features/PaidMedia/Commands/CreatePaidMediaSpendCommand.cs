using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Features.PaidMedia.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.PaidMedia.Commands;

public record CreatePaidMediaSpendCommand(
    string Channel,
    DateTime PeriodStartDateUtc,
    DateTime PeriodEndDateUtc,
    decimal Amount,
    int AcquisitionsCount,
    string? Notes,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<PaidMediaSpendDto>>;

public sealed class CreatePaidMediaSpendCommandHandler : IRequestHandler<CreatePaidMediaSpendCommand, Result<PaidMediaSpendDto>>
{
    private readonly DbContext _dbContext;

    public CreatePaidMediaSpendCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PaidMediaSpendDto>> Handle(CreatePaidMediaSpendCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PaidMediaChannel>(request.Channel, true, out var channel))
        {
            return Result.Failure<PaidMediaSpendDto>(Error.Validation(
                "PaidMediaSpend.InvalidChannel",
                $"Canal de mídia paga inválido: '{request.Channel}'."));
        }

        var spendResult = PaidMediaSpend.Create(
            channel,
            request.PeriodStartDateUtc,
            request.PeriodEndDateUtc,
            request.Amount,
            request.AcquisitionsCount,
            request.PerformedByAdminUserId,
            request.Notes);

        if (spendResult.IsFailure)
        {
            return Result.Failure<PaidMediaSpendDto>(spendResult.Error);
        }

        var spend = spendResult.Value;
        _dbContext.Set<PaidMediaSpend>().Add(spend);

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "PaidMediaSpend.Created",
            $"PaidMediaSpend/{spend.Id}",
            request.IpAddress,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                spend.Id,
                spend.Channel,
                spend.Amount,
                spend.AcquisitionsCount,
                spend.PeriodStartDateUtc,
                spend.PeriodEndDateUtc
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var cacDirect = spend.AcquisitionsCount > 0 ? Math.Round(spend.Amount / spend.AcquisitionsCount, 2) : 0m;

        var dto = new PaidMediaSpendDto(
            spend.Id,
            spend.Channel.ToString(),
            spend.PeriodStartDateUtc,
            spend.PeriodEndDateUtc,
            spend.Amount,
            spend.AcquisitionsCount,
            cacDirect,
            spend.Notes,
            spend.CreatedByAdminId,
            spend.CreatedAtUtc);

        return Result.Success(dto);
    }
}
