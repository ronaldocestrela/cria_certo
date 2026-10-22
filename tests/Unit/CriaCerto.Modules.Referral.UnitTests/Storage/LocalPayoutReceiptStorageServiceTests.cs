using System.Text;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using CriaCerto.Modules.Referral.Infrastructure.Storage;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Storage;

public sealed class LocalPayoutReceiptStorageServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly LocalPayoutReceiptStorageService _service;

    public LocalPayoutReceiptStorageServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "criacerto_tests_receipts_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _service = new LocalPayoutReceiptStorageService(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveReceiptAsync_WithValidPdf_ShouldSaveAndReturnRelativePath()
    {
        // Arrange
        var content = "%PDF-1.4 test receipt dummy content";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        // Act
        var result = await _service.SaveReceiptAsync(stream, "comprovante_pix.pdf", "application/pdf");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().EndWith(".pdf");
        File.Exists(Path.Combine(_tempDirectory, result.Value)).Should().BeTrue();
    }

    [Fact]
    public async Task SaveReceiptAsync_WithEmptyStream_ShouldReturnEmptyFileError()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        var result = await _service.SaveReceiptAsync(stream, "comprovante.pdf", "application/pdf");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReferralErrors.Receipt.EmptyFile.Code);
    }

    [Fact]
    public async Task SaveReceiptAsync_WithDisallowedExtension_ShouldReturnInvalidExtensionError()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("alert('malicious')"));

        // Act
        var result = await _service.SaveReceiptAsync(stream, "payload.exe", "application/octet-stream");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReferralErrors.Receipt.InvalidExtension.Code);
    }

    [Fact]
    public async Task SaveReceiptAsync_WhenSizeExceedsLimit_ShouldReturnFileTooLargeError()
    {
        // Arrange (over 5 MB: 5 * 1024 * 1024 + 1)
        var largeBuffer = new byte[5 * 1024 * 1024 + 10];
        using var stream = new MemoryStream(largeBuffer);

        // Act
        var result = await _service.SaveReceiptAsync(stream, "heavy_scan.pdf", "application/pdf");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReferralErrors.Receipt.FileTooLarge.Code);
    }

    [Fact]
    public async Task OpenReceiptStreamAsync_WithValidPath_ShouldReturnStream()
    {
        // Arrange
        var content = "SAMPLE RECEIPT CONTENT";
        using var saveStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var saveResult = await _service.SaveReceiptAsync(saveStream, "receipt.png", "image/png");
        saveResult.IsSuccess.Should().BeTrue();

        // Act
        var openResult = await _service.OpenReceiptStreamAsync(saveResult.Value);

        // Assert
        openResult.IsSuccess.Should().BeTrue();
        using var openedStream = openResult.Value.Stream;
        using var reader = new StreamReader(openedStream);
        var readContent = await reader.ReadToEndAsync();
        readContent.Should().Be(content);
        openResult.Value.ContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task OpenReceiptStreamAsync_WhenFileDoesNotExist_ShouldReturnNotFoundError()
    {
        // Act
        var result = await _service.OpenReceiptStreamAsync("non_existent_file.pdf");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReferralErrors.Receipt.FileNotFound.Code);
    }

    [Fact]
    public async Task OpenReceiptStreamAsync_WithPathTraversal_ShouldReturnInvalidPathError()
    {
        // Act
        var result = await _service.OpenReceiptStreamAsync("../../../etc/passwd");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReferralErrors.Receipt.InvalidPath.Code);
    }
}
