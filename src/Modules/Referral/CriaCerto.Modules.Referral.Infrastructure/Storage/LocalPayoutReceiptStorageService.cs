using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Errors;

namespace CriaCerto.Modules.Referral.Infrastructure.Storage;

public sealed class LocalPayoutReceiptStorageService : IPayoutReceiptStorageService
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg"
    };

    private readonly string _baseDirectory;

    public LocalPayoutReceiptStorageService(string? baseDirectory = null)
    {
        _baseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "storage", "receipts")
            : Path.GetFullPath(baseDirectory);

        if (!Directory.Exists(_baseDirectory))
        {
            Directory.CreateDirectory(_baseDirectory);
        }
    }

    public async Task<Result<string>> SaveReceiptAsync(
        Stream contentStream,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (contentStream is null || contentStream.Length == 0)
        {
            return Result.Failure<string>(ReferralErrors.Receipt.EmptyFile);
        }

        if (contentStream.Length > MaxFileSizeBytes)
        {
            return Result.Failure<string>(ReferralErrors.Receipt.FileTooLarge);
        }

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return Result.Failure<string>(ReferralErrors.Receipt.InvalidExtension);
        }

        var safeFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(_baseDirectory, safeFileName);

        if (contentStream.CanSeek && contentStream.Position != 0)
        {
            contentStream.Position = 0;
        }

        await using (var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await contentStream.CopyToAsync(fileStream, cancellationToken);
        }

        return Result.Success(safeFileName);
    }

    public Task<Result<(Stream Stream, string ContentType, string FileName)>> OpenReceiptStreamAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Task.FromResult(Result.Failure<(Stream Stream, string ContentType, string FileName)>(ReferralErrors.Receipt.InvalidPath));
        }

        var fullPath = Path.GetFullPath(Path.Combine(_baseDirectory, filePath));

        // Check path traversal
        if (!fullPath.StartsWith(_baseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Result.Failure<(Stream Stream, string ContentType, string FileName)>(ReferralErrors.Receipt.InvalidPath));
        }

        if (!File.Exists(fullPath))
        {
            return Task.FromResult(Result.Failure<(Stream Stream, string ContentType, string FileName)>(ReferralErrors.Receipt.FileNotFound));
        }

        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        var contentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var fileName = Path.GetFileName(fullPath);

        return Task.FromResult(Result.Success(((Stream)stream, contentType, fileName)));
    }

    public bool DeleteReceipt(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(_baseDirectory, filePath));
            if (!fullPath.StartsWith(_baseDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return true;
            }
        }
        catch
        {
            // Ignore deletion errors on cleanup
        }

        return false;
    }
}
