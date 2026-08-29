using CriaCerto.BuildingBlocks.Abstractions.Licensing;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Application.Abstractions.Messaging;
using CriaCerto.Modules.Breeding.Application.Abstractions;
using CriaCerto.Modules.Breeding.Application.Contracts;
using CriaCerto.Modules.Breeding.Application.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Breeding.Application.Features.BreedingOps;

[RequiresModule("Breeding")]
public sealed record RegisterIatfProtocolCommand(
    string Name,
    DateTime StartDate,
    DateTime InseminationDate,
    Guid SemenBatchId,
    IReadOnlyList<Guid> CowIds,
    Guid TenantId,
    Guid? BullId = null) : ICommand<IatfProtocolDto>;

[RequiresModule("Breeding")]
public sealed record RegisterPregnancyDiagnosisCommand(
    Guid CowId,
    DateTime DiagnosisDate,
    DiagnosisMethod Method,
    bool IsPregnant,
    Guid TenantId,
    int? GestationalAgeDays = null,
    string? Notes = null) : ICommand<PregnancyDiagnosisDto>;

public sealed class RegisterIatfProtocolCommandValidator : AbstractValidator<RegisterIatfProtocolCommand>
{
    public RegisterIatfProtocolCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CowIds).NotEmpty();
    }
}

public sealed class RegisterPregnancyDiagnosisCommandValidator : AbstractValidator<RegisterPregnancyDiagnosisCommand>
{
    public RegisterPregnancyDiagnosisCommandValidator()
    {
        RuleFor(x => x.CowId).NotEmpty();
        RuleFor(x => x.DiagnosisDate).LessThanOrEqualTo(DateTime.UtcNow);
    }
}

public sealed class RegisterIatfProtocolCommandHandler : IRequestHandler<RegisterIatfProtocolCommand, Result<IatfProtocolDto>>
{
    private readonly IBreedingDbContext _dbContext;

    public RegisterIatfProtocolCommandHandler(IBreedingDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<IatfProtocolDto>> Handle(RegisterIatfProtocolCommand request, CancellationToken cancellationToken)
    {
        string? bullName = null;
        if (request.BullId.HasValue && request.BullId.Value != Guid.Empty)
        {
            var bull = await _dbContext.Cows
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.BullId.Value &&
                                          (request.TenantId == Guid.Empty || c.TenantId == request.TenantId || c.TenantId == Guid.Empty), cancellationToken);

            if (bull is not null)
            {
                bullName = !string.IsNullOrWhiteSpace(bull.Nickname)
                    ? $"{bull.EarTag} - {bull.Nickname} ({bull.Breed})"
                    : $"{bull.EarTag} ({bull.Breed})";
            }
        }

        var protocolResult = IatfProtocol.Create(
            request.Name,
            request.StartDate,
            request.InseminationDate,
            request.SemenBatchId,
            request.CowIds.ToList(),
            request.TenantId,
            request.BullId,
            bullName);

        if (protocolResult.IsFailure)
            return Result.Failure<IatfProtocolDto>(protocolResult.Error);

        var cows = await _dbContext.Cows.Where(c => request.CowIds.Contains(c.Id)).ToListAsync(cancellationToken);
        foreach (var cow in cows)
        {
            var startResult = cow.StartIatfProtocol(protocolResult.Value.Id);
            if (startResult.IsFailure)
                return Result.Failure<IatfProtocolDto>(startResult.Error);
        }

        _dbContext.IatfProtocols.Add(protocolResult.Value);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new IatfProtocolDto(
            protocolResult.Value.Id,
            protocolResult.Value.Name,
            protocolResult.Value.StartDate,
            protocolResult.Value.InseminationDate,
            protocolResult.Value.SemenBatchId,
            protocolResult.Value.CowIds.Count,
            protocolResult.Value.BullId,
            protocolResult.Value.BullName));
    }
}

public sealed class RegisterPregnancyDiagnosisCommandHandler : IRequestHandler<RegisterPregnancyDiagnosisCommand, Result<PregnancyDiagnosisDto>>
{
    private readonly IBreedingDbContext _dbContext;

    public RegisterPregnancyDiagnosisCommandHandler(IBreedingDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<PregnancyDiagnosisDto>> Handle(RegisterPregnancyDiagnosisCommand request, CancellationToken cancellationToken)
    {
        var cow = await _dbContext.Cows.FirstOrDefaultAsync(c => c.Id == request.CowId, cancellationToken);
        if (cow is null)
            return Result.Failure<PregnancyDiagnosisDto>(Error.NotFound("Cow.NotFound", "Matriz bovina não encontrada."));

        var diagResult = PregnancyDiagnosis.Create(request.CowId, request.DiagnosisDate, request.Method, request.IsPregnant, request.TenantId, request.GestationalAgeDays, request.Notes);
        if (diagResult.IsFailure)
            return Result.Failure<PregnancyDiagnosisDto>(diagResult.Error);

        var cowDiagResult = cow.RecordPregnancyDiagnosis(request.IsPregnant, request.DiagnosisDate);
        if (cowDiagResult.IsFailure)
            return Result.Failure<PregnancyDiagnosisDto>(cowDiagResult.Error);

        _dbContext.PregnancyDiagnoses.Add(diagResult.Value);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new PregnancyDiagnosisDto(
            diagResult.Value.Id,
            cow.Id,
            cow.EarTag,
            diagResult.Value.DiagnosisDate,
            diagResult.Value.Method,
            diagResult.Value.IsPregnant,
            diagResult.Value.GestationalAgeDays,
            diagResult.Value.Notes));
    }
}

[RequiresModule("Breeding")]
public sealed record GetIatfProtocolsQuery(Guid TenantId) : IQuery<List<IatfProtocolDto>>;

public sealed class GetIatfProtocolsQueryHandler : IRequestHandler<GetIatfProtocolsQuery, Result<List<IatfProtocolDto>>>
{
    private readonly IBreedingDbContext _dbContext;

    public GetIatfProtocolsQueryHandler(IBreedingDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<List<IatfProtocolDto>>> Handle(GetIatfProtocolsQuery request, CancellationToken cancellationToken)
    {
        var protocols = await _dbContext.IatfProtocols
            .AsNoTracking()
            .Where(p => p.TenantId == request.TenantId)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);

        var dtos = protocols.Select(p => new IatfProtocolDto(
            p.Id,
            p.Name,
            p.StartDate,
            p.InseminationDate,
            p.SemenBatchId,
            p.CowIds.Count,
            p.BullId,
            p.BullName)).ToList();

        return Result.Success(dtos);
    }
}

