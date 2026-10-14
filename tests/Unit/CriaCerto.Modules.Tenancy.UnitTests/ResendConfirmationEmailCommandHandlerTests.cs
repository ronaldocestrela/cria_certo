using CriaCerto.BuildingBlocks.Abstractions.Email;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.ResendConfirmationEmail;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class ResendConfirmationEmailCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;

    public ResendConfirmationEmailCommandHandlerTests()
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
    public async Task Handle_Should_GenerateNewTokenAndDispatchEmail_When_UserExistsAndNotConfirmed()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Fabio Gado",
            Email = "fabio@fazenda.com.br",
            PasswordHash = "hash",
            IsEmailConfirmed = false
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var emailSender = Substitute.For<IEmailSender>();
        emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new EmailSendResult("msg_resend_123")));

        var handler = new ResendConfirmationEmailCommandHandler(_dbContext, emailSender);
        var command = new ResendConfirmationEmailCommand("fabio@fazenda.com.br");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();

        var userInDb = await _dbContext.Users.FindAsync(user.Id);
        userInDb!.EmailConfirmationToken.Should().Be(result.Value);
        userInDb.EmailConfirmationTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);

        await emailSender.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m =>
                m.To.Any(r => r.Email == "fabio@fazenda.com.br") &&
                m.Subject.Contains("Confirme seu e-mail")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_Return_Conflict_When_UserIsAlreadyConfirmed()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Mariana Leite",
            Email = "mariana@fazenda.com.br",
            PasswordHash = "hash",
            IsEmailConfirmed = true,
            EmailConfirmedAtUtc = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new ResendConfirmationEmailCommandHandler(_dbContext);
        var command = new ResendConfirmationEmailCommand("mariana@fazenda.com.br");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Auth.EmailAlreadyConfirmed");
    }

    [Fact]
    public async Task Handle_Should_Return_Success_With_Empty_Token_When_UserDoesNotExist_ToPreventEnumeration()
    {
        // Arrange
        var handler = new ResendConfirmationEmailCommandHandler(_dbContext);
        var command = new ResendConfirmationEmailCommand("inexistente@fazenda.com.br");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
