using CriaCerto.Api.Configuration;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Architecture.IntegrationTests;

public class DotEnvLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public DotEnvLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DotEnvLoaderTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void LoadFile_WithValidEntriesAndComments_ShouldPopulateEnvironmentVariables()
    {
        var envFilePath = Path.Combine(_tempDir, ".env");
        var testKey1 = "TEST_ENV_KEY_" + Guid.NewGuid().ToString("N")[..8];
        var testKey2 = "TEST_ENV_QUOTED_" + Guid.NewGuid().ToString("N")[..8];

        var content = $"""
            # Comentário de teste
            // Outro tipo de comentário
            {testKey1}=SecretValue123!

            # Chave com aspas
            {testKey2}="QuotedSecretValue456"
            """;

        File.WriteAllText(envFilePath, content);

        try
        {
            DotEnvLoader.LoadFile(envFilePath);

            Environment.GetEnvironmentVariable(testKey1).Should().Be("SecretValue123!");
            Environment.GetEnvironmentVariable(testKey2).Should().Be("QuotedSecretValue456");
        }
        finally
        {
            Environment.SetEnvironmentVariable(testKey1, null);
            Environment.SetEnvironmentVariable(testKey2, null);
        }
    }

    [Fact]
    public void LoadFile_WhenVariableAlreadyExists_ShouldNotOverwriteExistingValue()
    {
        var envFilePath = Path.Combine(_tempDir, ".env");
        var testKey = "TEST_EXISTING_KEY_" + Guid.NewGuid().ToString("N")[..8];

        Environment.SetEnvironmentVariable(testKey, "OriginalValue");

        try
        {
            File.WriteAllText(envFilePath, $"{testKey}=NewIgnoredValue");

            DotEnvLoader.LoadFile(envFilePath);

            Environment.GetEnvironmentVariable(testKey).Should().Be("OriginalValue");
        }
        finally
        {
            Environment.SetEnvironmentVariable(testKey, null);
        }
    }
}
