using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Commands;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class CreateAdminUserCommandHandlerTests
{
    private (BackofficeDbContext DbContext, SqliteConnection Connection) GetInMemoryDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<BackofficeDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new BackofficeDbContext(options);
        dbContext.Database.EnsureCreated();

        return (dbContext, connection);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateAdminUserAndAuditLog()
    {
        // Arrange
        var (dbContext, connection) = GetInMemoryDbContext();
        using (connection)
        using (dbContext)
        {
            var handler = new CreateAdminUserCommandHandler(dbContext);

            var role = AdminRole.Create("SupportN1", "Support level 1").Value;
            dbContext.AdminRoles.Add(role);
            await dbContext.SaveChangesAsync();

            var command = new CreateAdminUserCommand(
                Name: "Gabriel Ferreira",
                Email: "gabriel.support@criacerto.com.br",
                RawPassword: "StrongPassword123!",
                RoleIds: new List<Guid> { role.Id },
                PerformedByAdminUserId: Guid.NewGuid(),
                PerformedByAdminEmail: "owner@criacerto.com.br",
                IpAddress: "192.168.1.50"
            );

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Name.Should().Be("Gabriel Ferreira");
            result.Value.Email.Should().Be("gabriel.support@criacerto.com.br");
            result.Value.MustChangePasswordOnNextLogin.Should().BeTrue();

            var userInDb = await dbContext.AdminUsers.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Email == command.Email.ToLowerInvariant());
            userInDb.Should().NotBeNull();
            userInDb!.Roles.Should().ContainSingle(r => r.Name == "SupportN1");

            var auditInDb = await dbContext.AuditLogs.FirstOrDefaultAsync(a => a.Action == "AdminUser.Created");
            auditInDb.Should().NotBeNull();
            auditInDb!.AdminUserEmail.Should().Be("owner@criacerto.com.br");
        }
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ShouldReturnConflictFailure()
    {
        // Arrange
        var (dbContext, connection) = GetInMemoryDbContext();
        using (connection)
        using (dbContext)
        {
            var existingUser = AdminUser.Create("Existing Admin", "duplicate@criacerto.com.br", "hash").Value;
            dbContext.AdminUsers.Add(existingUser);
            await dbContext.SaveChangesAsync();

            var handler = new CreateAdminUserCommandHandler(dbContext);
            var command = new CreateAdminUserCommand(
                Name: "New User",
                Email: "duplicate@criacerto.com.br",
                RawPassword: "Password123!",
                RoleIds: new List<Guid>(),
                PerformedByAdminUserId: Guid.NewGuid(),
                PerformedByAdminEmail: "owner@criacerto.com.br",
                IpAddress: "127.0.0.1"
            );

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.Conflict);
        }
    }
}
