using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.ChangeUserPassword;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class ChangeUserPasswordCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;
    private readonly ITenancyAuditSink _auditSink;

    public ChangeUserPasswordCommandHandlerTests()
    {
        _sqliteConnection = new SqliteConnection("Filename=:memory:");
        _sqliteConnection.Open();

        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        _dbContext = new TenancyDbContext(options);
        _dbContext.Database.EnsureCreated();
        _auditSink = Substitute.For<ITenancyAuditSink>();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _sqliteConnection.Close();
        _sqliteConnection.Dispose();
    }

    [Fact]
    public async Task Handle_Should_Update_Password_And_Audit_When_Current_Password_Is_Valid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var initialPassword = "SenhaAntiga@123";
        var user = new User
        {
            Id = userId,
            Email = "usuario@fazenda.com.br",
            FullName = "Usuario Teste",
            PasswordHash = PasswordHasher.Hash(initialPassword)
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new ChangeUserPasswordCommandHandler(_dbContext, _auditSink);
        var command = new ChangeUserPasswordCommand(
            userId,
            initialPassword,
            "NovaSenhaForte#2026",
            "NovaSenhaForte#2026",
            "10.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var userInDb = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        PasswordHasher.Verify("NovaSenhaForte#2026", userInDb!.PasswordHash).Should().BeTrue();
        PasswordHasher.Verify(initialPassword, userInDb.PasswordHash).Should().BeFalse();

        await _auditSink.Received(1).RecordUserPasswordChangeAuditAsync(
            userId,
            "usuario@fazenda.com.br",
            "10.0.0.1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_Fail_When_Current_Password_Is_Incorrect()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "usuario@fazenda.com.br",
            FullName = "Usuario Teste",
            PasswordHash = PasswordHasher.Hash("SenhaCorreta@123")
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new ChangeUserPasswordCommandHandler(_dbContext, _auditSink);
        var command = new ChangeUserPasswordCommand(
            userId,
            "SenhaErrada@999",
            "NovaSenhaForte#2026",
            "NovaSenhaForte#2026");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCurrentPassword");
    }

    [Fact]
    public async Task Handle_Should_Fail_When_New_Password_Is_Same_As_Current()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var password = "MesmaSenha@123";
        var user = new User
        {
            Id = userId,
            Email = "usuario@fazenda.com.br",
            FullName = "Usuario Teste",
            PasswordHash = PasswordHasher.Hash(password)
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new ChangeUserPasswordCommandHandler(_dbContext, _auditSink);
        var command = new ChangeUserPasswordCommand(
            userId,
            password,
            password,
            password);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.SamePassword");
    }
}
