using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.UpdateUserProfile;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class UpdateUserProfileCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;
    private readonly ITenancyAuditSink _auditSink;

    public UpdateUserProfileCommandHandlerTests()
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
    public async Task Handle_Should_Update_Profile_And_Record_Audit_When_Valid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "roberto@fazenda.com.br",
            FullName = "Roberto Carlos",
            PhoneNumber = "11988887777"
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateUserProfileCommandHandler(_dbContext, _auditSink);
        var command = new UpdateUserProfileCommand(
            userId,
            "Roberto Carlos da Silva",
            "+55 11 99999-8888",
            "192.168.1.50");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FullName.Should().Be("Roberto Carlos da Silva");
        result.Value.PhoneNumber.Should().Be("+55 11 99999-8888");

        var userInDb = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        userInDb!.FullName.Should().Be("Roberto Carlos da Silva");
        userInDb.PhoneNumber.Should().Be("+55 11 99999-8888");

        await _auditSink.Received(1).RecordUserProfileUpdateAuditAsync(
            userId,
            "roberto@fazenda.com.br",
            "Roberto Carlos",
            "Roberto Carlos da Silva",
            "11988887777",
            "+55 11 99999-8888",
            "192.168.1.50",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_Return_NotFound_When_User_Does_Not_Exist()
    {
        // Arrange
        var handler = new UpdateUserProfileCommandHandler(_dbContext, _auditSink);
        var command = new UpdateUserProfileCommand(
            Guid.NewGuid(),
            "Novo Nome",
            "11999998888");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
