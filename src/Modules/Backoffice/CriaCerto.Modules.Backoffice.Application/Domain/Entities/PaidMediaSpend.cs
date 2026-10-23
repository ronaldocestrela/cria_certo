using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public sealed class PaidMediaSpend
{
    public Guid Id { get; private set; }
    public PaidMediaChannel Channel { get; private set; }
    public DateTime PeriodStartDateUtc { get; private set; }
    public DateTime PeriodEndDateUtc { get; private set; }
    public decimal Amount { get; private set; }
    public int AcquisitionsCount { get; private set; }
    public string? Notes { get; private set; }
    public Guid CreatedByAdminId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private PaidMediaSpend() { }

    public static Result<PaidMediaSpend> Create(
        PaidMediaChannel channel,
        DateTime startDateUtc,
        DateTime endDateUtc,
        decimal amount,
        int acquisitionsCount,
        Guid createdByAdminId,
        string? notes = null)
    {
        if (createdByAdminId == Guid.Empty)
        {
            return Result.Failure<PaidMediaSpend>(Error.Validation(
                "PaidMediaSpend.AdminRequired",
                "O identificador do administrador responsável é obrigatório."));
        }

        if (amount <= 0)
        {
            return Result.Failure<PaidMediaSpend>(Error.Validation(
                "PaidMediaSpend.InvalidAmount",
                "O valor investido deve ser maior que zero."));
        }

        if (endDateUtc < startDateUtc)
        {
            return Result.Failure<PaidMediaSpend>(Error.Validation(
                "PaidMediaSpend.InvalidPeriod",
                "A data final do período não pode ser anterior à data inicial."));
        }

        if (acquisitionsCount < 0)
        {
            return Result.Failure<PaidMediaSpend>(Error.Validation(
                "PaidMediaSpend.InvalidAcquisitionsCount",
                "A quantidade de aquisições não pode ser negativa."));
        }

        var spend = new PaidMediaSpend
        {
            Id = Guid.NewGuid(),
            Channel = channel,
            PeriodStartDateUtc = startDateUtc,
            PeriodEndDateUtc = endDateUtc,
            Amount = amount,
            AcquisitionsCount = acquisitionsCount,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedByAdminId = createdByAdminId,
            CreatedAtUtc = DateTime.UtcNow
        };

        return Result.Success(spend);
    }
}
