using System.Globalization;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Features.Payouts.Services;
using MediatR;

namespace CriaCerto.Modules.Referral.Application.Features.Payouts.Queries;

public sealed record ExportPixBatchCsvQuery(
    IReadOnlyList<Guid>? ParticipantIds = null
) : IRequest<Result<(byte[] FileBytes, string FileName, string ContentType)>>;

public sealed class ExportPixBatchCsvQueryHandler
    : IRequestHandler<ExportPixBatchCsvQuery, Result<(byte[] FileBytes, string FileName, string ContentType)>>
{
    private readonly IMediator _mediator;
    private readonly IPixBatchCsvGenerator _csvGenerator;

    public ExportPixBatchCsvQueryHandler(IMediator mediator, IPixBatchCsvGenerator csvGenerator)
    {
        _mediator = mediator;
        _csvGenerator = csvGenerator;
    }

    public async Task<Result<(byte[] FileBytes, string FileName, string ContentType)>> Handle(
        ExportPixBatchCsvQuery request,
        CancellationToken cancellationToken)
    {
        var payoutsResult = await _mediator.Send(
            new GetAvailablePartnerPayoutsQuery(FilterParticipantIds: request.ParticipantIds),
            cancellationToken);

        if (payoutsResult.IsFailure)
        {
            return Result.Failure<(byte[] FileBytes, string FileName, string ContentType)>(payoutsResult.Error);
        }

        var csvBytes = _csvGenerator.GenerateCsv(payoutsResult.Value);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var fileName = $"lote_pix_criacerto_{timestamp}.csv";

        return Result.Success((csvBytes, fileName, "text/csv; charset=utf-8"));
    }
}
