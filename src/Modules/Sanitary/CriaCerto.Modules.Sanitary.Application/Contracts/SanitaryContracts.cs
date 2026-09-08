using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Application.Abstractions.Messaging;
using CriaCerto.Modules.Sanitary.Application.Domain;
using CriaCerto.Modules.Sanitary.Application.Domain.Services;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Sanitary.Application.Contracts;

// --- DTOs ---
public sealed record VaccinationCampaignDto(
    Guid Id,
    string Name,
    CampaignType Type,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    string? Description,
    bool IsActive,
    DateTime CreatedAtUtc);

public sealed record TreatmentRecordDto(
    Guid Id,
    Guid? AnimalId,
    Guid? LotId,
    string ProductCommercialName,
    TreatmentType Type,
    string? BatchNumber,
    string Dosage,
    int WithdrawalDays,
    DateTime ApplicationDateUtc,
    DateTime WithdrawalEndDateUtc,
    bool IsWithdrawalActive,
    string? AppliedByVeterinarian,
    string? Notes);

public sealed record SlaughterEligibilityDto(
    Guid AnimalId,
    bool IsEligibleForSlaughter,
    int RemainingWithdrawalDays,
    string? BlockingTreatmentName,
    DateTime? ActiveWithdrawalEndsAtUtc);

// --- INTERFACES ---
public interface ISanitaryDbContext
{
    DbSet<VaccinationCampaign> VaccinationCampaigns { get; }
    DbSet<TreatmentRecord> TreatmentRecords { get; }
    DbSet<VaccineReference> VaccineReferences { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

// --- COMMANDS & HANDLERS ---
public sealed record CreateVaccinationCampaignCommand(
    string Name,
    CampaignType Type,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    string? Description) : ICommand<VaccinationCampaignDto>;

public sealed class CreateVaccinationCampaignCommandHandler : IRequestHandler<CreateVaccinationCampaignCommand, Result<VaccinationCampaignDto>>
{
    private readonly ISanitaryDbContext _context;

    public CreateVaccinationCampaignCommandHandler(ISanitaryDbContext context)
    {
        _context = context;
    }

    public async Task<Result<VaccinationCampaignDto>> Handle(CreateVaccinationCampaignCommand request, CancellationToken cancellationToken)
    {
        var campaignResult = VaccinationCampaign.Create(
            request.Name,
            request.Type,
            request.StartDateUtc,
            request.EndDateUtc,
            request.Description);

        if (campaignResult.IsFailure)
            return Result.Failure<VaccinationCampaignDto>(campaignResult.Error);

        _context.VaccinationCampaigns.Add(campaignResult.Value);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = new VaccinationCampaignDto(
            campaignResult.Value.Id,
            campaignResult.Value.Name,
            campaignResult.Value.Type,
            campaignResult.Value.StartDateUtc,
            campaignResult.Value.EndDateUtc,
            campaignResult.Value.Description,
            campaignResult.Value.IsActive,
            campaignResult.Value.CreatedAtUtc);

        return Result.Success(dto);
    }
}

public sealed record ApplyTreatmentCommand(
    Guid? AnimalId,
    Guid? LotId,
    string ProductCommercialName,
    TreatmentType Type,
    string? BatchNumber,
    string Dosage,
    int WithdrawalDays,
    DateTime ApplicationDateUtc,
    string? AppliedByVeterinarian,
    string? Notes) : ICommand<TreatmentRecordDto>;

public sealed class ApplyTreatmentCommandHandler : IRequestHandler<ApplyTreatmentCommand, Result<TreatmentRecordDto>>
{
    private readonly ISanitaryDbContext _context;

    public ApplyTreatmentCommandHandler(ISanitaryDbContext context)
    {
        _context = context;
    }

    public async Task<Result<TreatmentRecordDto>> Handle(ApplyTreatmentCommand request, CancellationToken cancellationToken)
    {
        var treatmentResult = TreatmentRecord.Create(
            request.AnimalId,
            request.ProductCommercialName,
            request.Type,
            request.BatchNumber,
            request.Dosage,
            request.WithdrawalDays,
            request.ApplicationDateUtc,
            request.AppliedByVeterinarian,
            request.LotId,
            request.Notes);

        if (treatmentResult.IsFailure)
            return Result.Failure<TreatmentRecordDto>(treatmentResult.Error);

        _context.TreatmentRecords.Add(treatmentResult.Value);
        await _context.SaveChangesAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var dto = new TreatmentRecordDto(
            treatmentResult.Value.Id,
            treatmentResult.Value.AnimalId,
            treatmentResult.Value.LotId,
            treatmentResult.Value.ProductCommercialName,
            treatmentResult.Value.Type,
            treatmentResult.Value.BatchNumber,
            treatmentResult.Value.Dosage,
            treatmentResult.Value.WithdrawalDays,
            treatmentResult.Value.ApplicationDateUtc,
            treatmentResult.Value.WithdrawalEndDateUtc,
            treatmentResult.Value.IsWithdrawalPeriodActive(now),
            treatmentResult.Value.AppliedByVeterinarian,
            treatmentResult.Value.Notes);

        return Result.Success(dto);
    }
}

// --- QUERIES & HANDLERS ---
public sealed record GetActiveCampaignsQuery : IQuery<List<VaccinationCampaignDto>>;

public sealed class GetActiveCampaignsQueryHandler : IRequestHandler<GetActiveCampaignsQuery, Result<List<VaccinationCampaignDto>>>
{
    private readonly ISanitaryDbContext _context;

    public GetActiveCampaignsQueryHandler(ISanitaryDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<VaccinationCampaignDto>>> Handle(GetActiveCampaignsQuery request, CancellationToken cancellationToken)
    {
        var campaigns = await _context.VaccinationCampaigns
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.StartDateUtc)
            .Select(c => new VaccinationCampaignDto(
                c.Id,
                c.Name,
                c.Type,
                c.StartDateUtc,
                c.EndDateUtc,
                c.Description,
                c.IsActive,
                c.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success(campaigns);
    }
}

public sealed record ValidateSlaughterEligibilityQuery(Guid AnimalId) : IQuery<SlaughterEligibilityDto>;

public sealed class ValidateSlaughterEligibilityQueryHandler : IRequestHandler<ValidateSlaughterEligibilityQuery, Result<SlaughterEligibilityDto>>
{
    private readonly ISanitaryDbContext _context;

    public ValidateSlaughterEligibilityQueryHandler(ISanitaryDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SlaughterEligibilityDto>> Handle(ValidateSlaughterEligibilityQuery request, CancellationToken cancellationToken)
    {
        var treatments = await _context.TreatmentRecords
            .AsNoTracking()
            .Where(t => t.AnimalId == request.AnimalId)
            .ToListAsync(cancellationToken);

        var eval = WithdrawalPeriodService.EvaluateSlaughterEligibility(request.AnimalId, treatments, DateTime.UtcNow);

        var dto = new SlaughterEligibilityDto(
            eval.AnimalId,
            eval.IsEligibleForSlaughter,
            eval.RemainingWithdrawalDays,
            eval.BlockingTreatmentName,
            eval.ActiveWithdrawalEndsAtUtc);

        if (!eval.IsEligibleForSlaughter)
        {
            return Result.Failure<SlaughterEligibilityDto>(SanitaryErrors.ActiveSlaughterWithdrawalPeriod);
        }

        return Result.Success(dto);
    }
}

public sealed record GetTreatmentsQuery : IQuery<List<TreatmentRecordDto>>;

public sealed class GetTreatmentsQueryHandler : IRequestHandler<GetTreatmentsQuery, Result<List<TreatmentRecordDto>>>
{
    private readonly ISanitaryDbContext _context;

    public GetTreatmentsQueryHandler(ISanitaryDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<TreatmentRecordDto>>> Handle(GetTreatmentsQuery request, CancellationToken cancellationToken)
    {
        var records = await _context.TreatmentRecords
            .AsNoTracking()
            .OrderByDescending(t => t.ApplicationDateUtc)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var treatments = records
            .Select(t => new TreatmentRecordDto(
                t.Id,
                t.AnimalId,
                t.LotId,
                t.ProductCommercialName,
                t.Type,
                t.BatchNumber,
                t.Dosage,
                t.WithdrawalDays,
                t.ApplicationDateUtc,
                t.WithdrawalEndDateUtc,
                t.IsWithdrawalPeriodActive(now),
                t.AppliedByVeterinarian,
                t.Notes))
            .ToList();

        return Result.Success(treatments);
    }
}
