using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.RegisterUser;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class RegisterUserCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;

    public RegisterUserCommandHandlerTests()
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
    public async Task Handle_Should_Create_User_When_Data_Is_Valid()
    {
        // Arrange
        var handler = new RegisterUserCommandHandler(_dbContext);
        var command = new RegisterUserCommand("João Silva", "joao@fazenda.com.br", "Senha@123", "11999999999");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.FullName.Should().Be("João Silva");
        result.Value.Email.Should().Be("joao@fazenda.com.br");

        var userInDb = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == "joao@fazenda.com.br");
        userInDb.Should().NotBeNull();
        userInDb!.FullName.Should().Be("João Silva");
    }

    [Fact]
    public async Task Handle_Should_Return_Conflict_When_Email_Already_Exists()
    {
        // Arrange
        _dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            FullName = "Maria Santos",
            Email = "maria@fazenda.com.br",
            PasswordHash = "hash"
        });
        await _dbContext.SaveChangesAsync();

        var handler = new RegisterUserCommandHandler(_dbContext);
        var command = new RegisterUserCommand("Maria Novo Nome", "maria@fazenda.com.br", "Senha@123");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("User.EmailAlreadyExists");
    }

    [Fact]
    public async Task Handle_Should_Dispatch_Welcome_Email_When_EmailSender_Is_Configured()
    {
        // Arrange
        var emailSender = NSubstitute.Substitute.For<CriaCerto.BuildingBlocks.Abstractions.Email.IEmailSender>();
        emailSender.SendAsync(NSubstitute.Arg.Any<CriaCerto.BuildingBlocks.Abstractions.Email.EmailMessage>(), NSubstitute.Arg.Any<CancellationToken>())
            .Returns(CriaCerto.BuildingBlocks.Abstractions.Results.Result.Success(new CriaCerto.BuildingBlocks.Abstractions.Email.EmailSendResult("msg_welcome_123")));

        var handler = new RegisterUserCommandHandler(_dbContext, emailSender);
        var command = new RegisterUserCommand("Antonio Boiadeiro", "antonio@fazenda.com.br", "Senha@123", "67999998888");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await emailSender.Received(1).SendAsync(
            NSubstitute.Arg.Is<CriaCerto.BuildingBlocks.Abstractions.Email.EmailMessage>(m =>
                m.To.Any(r => r.Email == "antonio@fazenda.com.br") &&
                m.Subject.Contains("Bem-vindo")),
            NSubstitute.Arg.Any<CancellationToken>());
    }
}
