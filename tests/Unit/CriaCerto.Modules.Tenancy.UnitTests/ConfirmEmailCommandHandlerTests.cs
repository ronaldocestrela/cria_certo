using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.ConfirmEmail;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class ConfirmEmailCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;

    public ConfirmEmailCommandHandlerTests()
    {
        _sqliteConnection = new SqliteConnection("Filename=:memory:");
        _sqliteConnection.Open();

        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        _dbContext = new TenancyDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _sqliteConnection.Close();
        _sqliteConnection.Dispose();
    }

    [Fact]
    public async Task Handle_Should_ConfirmEmail_When_TokenAndEmailAreValid()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Rodrigo Pecuária",
            Email = "rodrigo@fazenda.com.br",
            PasswordHash = "hashed",
            IsEmailConfirmed = false,
            EmailConfirmationToken = "valid-token-123",
            EmailConfirmationTokenExpiresAt = DateTime.UtcNow.AddHours(24)
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new ConfirmEmailCommandHandler(_dbContext);
        var command = new ConfirmEmailCommand("rodrigo@fazenda.com.br", "valid-token-123");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var userInDb = await _dbContext.Users.FindAsync(user.Id);
        userInDb.Should().NotBeNull();
        userInDb!.IsEmailConfirmed.Should().BeTrue();
        userInDb.EmailConfirmedAtUtc.Should().NotBeNull();
        userInDb.EmailConfirmationToken.Should().BeNull();
        userInDb.EmailConfirmationTokenExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Return_NotFound_When_UserDoesNotExist()
    {
        // Arrange
        var handler = new ConfirmEmailCommandHandler(_dbContext);
        var command = new ConfirmEmailCommand("naoexiste@fazenda.com.br", "token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("User.NotFound");
    }

    [Fact]
    public async Task Handle_Should_Return_Success_When_EmailIsAlreadyConfirmed()
    {
        // Arrange (Idempotente)
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Carlos Boiadeiro",
            Email = "carlos@fazenda.com.br",
            PasswordHash = "hashed",
            IsEmailConfirmed = true,
            EmailConfirmedAtUtc = DateTime.UtcNow.AddDays(-1)
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new ConfirmEmailCommandHandler(_dbContext);
        var command = new ConfirmEmailCommand("carlos@fazenda.com.br", "qualquer-token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_Return_Validation_When_TokenIsIncorrect()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Marcos Prado",
            Email = "marcos@fazenda.com.br",
            PasswordHash = "hashed",
            IsEmailConfirmed = false,
            EmailConfirmationToken = "token-correto-xyz",
            EmailConfirmationTokenExpiresAt = DateTime.UtcNow.AddHours(24)
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new ConfirmEmailCommandHandler(_dbContext);
        var command = new ConfirmEmailCommand("marcos@fazenda.com.br", "token-errado");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidConfirmationToken");

        var userInDb = await _dbContext.Users.FindAsync(user.Id);
        userInDb!.IsEmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_Return_Validation_When_TokenIsExpired()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Lucas Pasto",
            Email = "lucas@fazenda.com.br",
            PasswordHash = "hashed",
            IsEmailConfirmed = false,
            EmailConfirmationToken = "token-expirado",
            EmailConfirmationTokenExpiresAt = DateTime.UtcNow.AddMinutes(-10) // Já expirou
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new ConfirmEmailCommandHandler(_dbContext);
        var command = new ConfirmEmailCommand("lucas@fazenda.com.br", "token-expirado");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.ConfirmationTokenExpired");

        var userInDb = await _dbContext.Users.FindAsync(user.Id);
        userInDb!.IsEmailConfirmed.Should().BeFalse();
    }
}
