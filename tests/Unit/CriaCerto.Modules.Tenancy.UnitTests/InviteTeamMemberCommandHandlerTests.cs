using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.InviteTeamMember;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class InviteTeamMemberCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;

    public InviteTeamMemberCommandHandlerTests()
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
    public async Task Handle_Should_Create_Invite_When_Valid()
    {
        // Arrange
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Fazenda Recanto", CNPJ = "12.345.678/0001-90" };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var handler = new InviteTeamMemberCommandHandler(_dbContext);
        var command = new InviteTeamMemberCommand(tenant.Id, "peao@fazenda.com", UserRole.OperadorCurral);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Email.Should().Be("peao@fazenda.com");
        result.Value.Role.Should().Be(UserRole.OperadorCurral);
        result.Value.InviteToken.Should().NotBeNullOrEmpty();

        var inviteInDb = await _dbContext.TeamInvites.FirstOrDefaultAsync(i => i.TenantId == tenant.Id);
        inviteInDb.Should().NotBeNull();
        inviteInDb!.Email.Should().Be("peao@fazenda.com");
        inviteInDb.Role.Should().Be(UserRole.OperadorCurral);
    }

    [Fact]
    public async Task Handle_Should_Fail_When_User_Already_In_Tenant()
    {
        // Arrange
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Fazenda Recanto", CNPJ = "12.345.678/0001-90" };
        var user = new User { Id = Guid.NewGuid(), Email = "zootecnista@fazenda.com", FullName = "Dr. Carlos", PasswordHash = "hash" };
        var userTenant = new UserTenant { TenantId = tenant.Id, UserId = user.Id, Role = UserRole.Zootecnista };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new InviteTeamMemberCommandHandler(_dbContext);
        var command = new InviteTeamMemberCommand(tenant.Id, "zootecnista@fazenda.com", UserRole.Zootecnista);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Tenancy.MemberExists");
    }

    [Fact]
    public async Task Handle_Should_Fail_When_Tenant_Not_Found()
    {
        // Arrange
        var handler = new InviteTeamMemberCommandHandler(_dbContext);
        var command = new InviteTeamMemberCommand(Guid.NewGuid(), "peao@fazenda.com", UserRole.OperadorCurral);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Tenant.NotFound");
    }

    [Fact]
    public async Task Handle_Should_Dispatch_Invite_Email_When_EmailSender_Is_Configured()
    {
        // Arrange
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Fazenda Sol Nascente", CNPJ = "12.345.678/0001-90" };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var emailSender = NSubstitute.Substitute.For<CriaCerto.BuildingBlocks.Abstractions.Email.IEmailSender>();
        emailSender.SendAsync(NSubstitute.Arg.Any<CriaCerto.BuildingBlocks.Abstractions.Email.EmailMessage>(), NSubstitute.Arg.Any<CancellationToken>())
            .Returns(CriaCerto.BuildingBlocks.Abstractions.Results.Result.Success(new CriaCerto.BuildingBlocks.Abstractions.Email.EmailSendResult("msg_invite_123")));

        var handler = new InviteTeamMemberCommandHandler(_dbContext, emailSender);
        var command = new InviteTeamMemberCommand(tenant.Id, "capataz@fazenda.com", UserRole.Admin);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await emailSender.Received(1).SendAsync(
            NSubstitute.Arg.Is<CriaCerto.BuildingBlocks.Abstractions.Email.EmailMessage>(m =>
                m.To.Any(r => r.Email == "capataz@fazenda.com") &&
                m.Subject.Contains("Fazenda Sol Nascente")),
            NSubstitute.Arg.Any<CancellationToken>());
    }
}
