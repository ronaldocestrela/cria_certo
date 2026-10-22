using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Referral.Application.Abstractions;

public interface IPayoutReceiptStorageService
{
    Task<Result<string>> SaveReceiptAsync(
        Stream contentStream,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Result<(Stream Stream, string ContentType, string FileName)>> OpenReceiptStreamAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    bool DeleteReceipt(string filePath);
}
