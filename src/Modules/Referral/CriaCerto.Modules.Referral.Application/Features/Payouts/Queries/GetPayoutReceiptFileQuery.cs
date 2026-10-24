using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using MediatR;

namespace CriaCerto.Modules.Referral.Application.Features.Payouts.Queries;

public sealed record GetPayoutReceiptFileQuery(
    string FileName
) : IRequest<Result<(Stream Stream, string ContentType, string DownloadFileName)>>;

public sealed class GetPayoutReceiptFileQueryHandler
    : IRequestHandler<GetPayoutReceiptFileQuery, Result<(Stream Stream, string ContentType, string DownloadFileName)>>
{
    private readonly IPayoutReceiptStorageService _storageService;

    public GetPayoutReceiptFileQueryHandler(IPayoutReceiptStorageService storageService)
    {
        _storageService = storageService;
    }

    public async Task<Result<(Stream Stream, string ContentType, string DownloadFileName)>> Handle(
        GetPayoutReceiptFileQuery request,
        CancellationToken cancellationToken)
    {
        var openResult = await _storageService.OpenReceiptStreamAsync(request.FileName, cancellationToken);
        if (openResult.IsFailure)
        {
            return Result.Failure<(Stream Stream, string ContentType, string DownloadFileName)>(openResult.Error);
        }

        return Result.Success((openResult.Value.Stream, openResult.Value.ContentType, openResult.Value.FileName));
    }
}
