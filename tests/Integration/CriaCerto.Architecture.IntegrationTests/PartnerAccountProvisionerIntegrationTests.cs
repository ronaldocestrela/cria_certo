using CriaCerto.Api.Services;
using CriaCerto.BuildingBlocks.Abstractions.Email;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.Login;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CriaCerto.Architecture.IntegrationTests;

public sealed class PartnerAccountProvisionerIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenancyDbContext _dbContext;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<PartnerAccountProvisioner> _logger;

    public PartnerAccountProvisionerIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new TenancyDbContext(options);
        _dbContext.Database.EnsureCreated();

        _emailSender = Substitute.For<IEmailSender>();
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new EmailSendResult("msg_test_123"))));

        _logger = Substitute.For<ILogger<PartnerAccountProvisioner>>();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ProvisionPartnerUserAsync_WhenNewUser_ProvisionsUserWithCredentialsAndSendsEmail()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["App:WebBaseUrl"] = "http://localhost:5205"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        var provisioner = new PartnerAccountProvisioner(
            _dbContext,
            configuration,
            _logger,
            _emailSender);

        var fullName = "Carlos Consultor";
        var email = "carlos@consultoria.agr.br";
        var phone = "+5567999881122";
        var referralCode = "CARLOS-CONSULTORIA";

        // Act
        var result = await provisioner.ProvisionPartnerUserAsync(
            fullName,
            email,
            phone,
            referralCode,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        var createdUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        createdUser.Should().NotBeNull();
        createdUser!.FullName.Should().Be(fullName);
        createdUser.PhoneNumber.Should().Be(phone);
        createdUser.IsEmailConfirmed.Should().BeFalse();
        createdUser.EmailConfirmationToken.Should().NotBeNullOrWhiteSpace();
        createdUser.PasswordResetToken.Should().NotBeNullOrWhiteSpace();

        // Email enviado deve conter link de ativação, código de indicação e a senha temporária
        await _emailSender.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m =>
                m.To.Any(r => r.Email == email) &&
                m.Subject.Contains("Parceiros") &&
                m.HtmlBody.Contains("http://localhost:5205/r/CARLOS-CONSULTORIA") &&
                m.HtmlBody.Contains("http://localhost:5205/confirm-email?token=") &&
                m.HtmlBody.Contains("Senha Temporária") &&
                m.HtmlBody.Contains("http://localhost:5205/forgot-password?email=") &&
                createdUser.PasswordHash.Length > 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProvisionPartnerUserAsync_WhenUserAlreadyExists_LinksUserAndSendsNotificationEmailWithoutOverwritingPassword()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["App:WebBaseUrl"] = "http://localhost:5205"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        var existingPasswordHash = PasswordHasher.Hash("MinhaSenhaExistente@123");
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "marcos@fazendaesperanca.com.br",
            FullName = "Marcos Produtor",
            PasswordHash = existingPasswordHash,
            IsEmailConfirmed = true
        };
        _dbContext.Users.Add(existingUser);
        await _dbContext.SaveChangesAsync();

        var provisioner = new PartnerAccountProvisioner(
            _dbContext,
            configuration,
            _logger,
            _emailSender);

        // Act
        var result = await provisioner.ProvisionPartnerUserAsync(
            "Marcos Produtor",
            existingUser.Email,
            null,
            "MARCOS-PARCEIRO",
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(existingUser.Id);

        var userInDb = await _dbContext.Users.FindAsync(existingUser.Id);
        userInDb!.PasswordHash.Should().Be(existingPasswordHash); // senha mantida intacta

        // Email de boas-vindas informando sobre o programa e o link de indicação
        await _emailSender.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m =>
                m.To.Any(r => r.Email == existingUser.Email) &&
                m.Subject.Contains("Parceiro") &&
                m.HtmlBody.Contains("http://localhost:5205/r/MARCOS-PARCEIRO") &&
                m.HtmlBody.Contains("suas credenciais habituais")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProvisionPartnerUserAsync_WhenEmailSenderNull_SucceedsGracefully()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var provisioner = new PartnerAccountProvisioner(
            _dbContext,
            configuration,
            _logger,
            null); // sem email sender

        // Act
        var result = await provisioner.ProvisionPartnerUserAsync(
            "Ana Maria",
            "ana@parceira.com",
            null,
            "ANA-MARIA",
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        var createdUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == "ana@parceira.com");
        createdUser.Should().NotBeNull();
    }
}
