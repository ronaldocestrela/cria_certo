using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.AnonymizeUserAccount;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class AnonymizeUserAccountCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;
    private readonly ITenancyAuditSink _auditSink;

    public AnonymizeUserAccountCommandHandlerTests()
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
    public async Task Handle_Should_Succeed_And_Anonymize_User_When_Valid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var initialPassword = "Password@123";
        var user = new User
        {
            Id = userId,
            Email = "produtor.original@fazenda.com.br",
            FullName = "Carlos Eduardo",
            PhoneNumber = "+5567999887766",
            PasswordHash = PasswordHasher.Hash(initialPassword),
            IsEmailConfirmed = true,
            EmailConfirmedAtUtc = DateTime.UtcNow.AddDays(-10),
            EmailConfirmationToken = "token-confirm-123",
            PasswordResetToken = "token-reset-456",
            CreatedAtUtc = DateTime.UtcNow.AddMonths(-2)
        };

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Ouro Verde",
            CNPJ = "12.345.678/0001-90",
            CnpjNormalized = "12345678000190",
            Status = TenantLifecycle.ToStatusString(TenantStatus.Active),
            SubscribedPlan = "Pro"
        };

        // Outro admin para permitir a saída
        var otherAdmin = new User
        {
            Id = Guid.NewGuid(),
            Email = "outro.admin@fazenda.com.br",
            FullName = "Mariana Silva",
            PasswordHash = PasswordHasher.Hash("AdminPass@123")
        };

        var userTenant = new UserTenant
        {
            UserId = userId,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow.AddMonths(-2)
        };

        var otherUserTenant = new UserTenant
        {
            UserId = otherAdmin.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow.AddMonths(-1)
        };

        _dbContext.Users.AddRange(user, otherAdmin);
        _dbContext.Tenants.Add(tenant);
        _dbContext.UserTenants.AddRange(userTenant, otherUserTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new AnonymizeUserAccountCommandHandler(_dbContext, _auditSink);
        var command = new AnonymizeUserAccountCommand(
            userId,
            initialPassword,
            "EXCLUIR",
            "Encerramento de atividades rurais",
            "187.12.34.56"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updatedUser = await _dbContext.Users.FindAsync(userId);
        updatedUser.Should().NotBeNull();
        updatedUser!.FullName.Should().Be("Titular Anonimizado");
        updatedUser.Email.Should().StartWith("anon_");
        updatedUser.Email.Should().EndWith("@deleted.criacerto.com.br");
        updatedUser.PhoneNumber.Should().BeNull();
        updatedUser.PasswordHash.Should().Be("ANONYMIZED_DISABLED_CREDENTIAL");
        updatedUser.EmailConfirmationToken.Should().BeNull();
        updatedUser.PasswordResetToken.Should().BeNull();
        updatedUser.IsAnonymized.Should().BeTrue();
        updatedUser.AnonymizedAtUtc.Should().NotBeNull();
        updatedUser.AnonymizationReason.Should().Be("Encerramento de atividades rurais");

        // Associações desvinculadas
        var userTenants = await _dbContext.UserTenants.Where(ut => ut.UserId == userId).ToListAsync();
        userTenants.Should().BeEmpty();

        // Outro admin permanece intacto
        var otherTenants = await _dbContext.UserTenants.Where(ut => ut.UserId == otherAdmin.Id).ToListAsync();
        otherTenants.Should().HaveCount(1);

        // Auditoria forense disparada
        await _auditSink.Received(1).RecordUserAccountAnonymizedAuditAsync(
            userId,
            "produtor.original@fazenda.com.br",
            Arg.Is<string>(e => e.StartsWith("anon_")),
            "Encerramento de atividades rurais",
            "187.12.34.56",
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_Should_Fail_When_User_Not_Found()
    {
        // Arrange
        var handler = new AnonymizeUserAccountCommandHandler(_dbContext, _auditSink);
        var command = new AnonymizeUserAccountCommand(
            Guid.NewGuid(),
            "QualquerSenha@123",
            "EXCLUIR"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("User.NotFound");
    }

    [Fact]
    public async Task Handle_Should_Fail_When_User_Already_Anonymized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "anon_1234@deleted.criacerto.com.br",
            FullName = "Titular Anonimizado",
            PasswordHash = "ANONYMIZED_DISABLED_CREDENTIAL",
            IsAnonymized = true,
            AnonymizedAtUtc = DateTime.UtcNow.AddDays(-1)
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new AnonymizeUserAccountCommandHandler(_dbContext, _auditSink);
        var command = new AnonymizeUserAccountCommand(
            userId,
            "Senha@123",
            "EXCLUIR"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("User.AlreadyAnonymized");
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
            FullName = "Jose Silva",
            PasswordHash = PasswordHasher.Hash("SenhaCorreta@123")
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new AnonymizeUserAccountCommandHandler(_dbContext, _auditSink);
        var command = new AnonymizeUserAccountCommand(
            userId,
            "SenhaErrada@123",
            "EXCLUIR"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidCurrentPassword");
    }

    [Fact]
    public async Task Handle_Should_Fail_When_User_Is_Sole_Admin_Of_Active_Tenant()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var password = "MinhaSenha@123";
        var user = new User
        {
            Id = userId,
            Email = "dono.fazenda@agro.com.br",
            FullName = "Fazendeiro Chefe",
            PasswordHash = PasswordHasher.Hash(password)
        };

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Solitária",
            CNPJ = "11.222.333/0001-44",
            CnpjNormalized = "11222333000144",
            Status = TenantLifecycle.ToStatusString(TenantStatus.Active),
            SubscribedPlan = "Enterprise"
        };

        var userTenant = new UserTenant
        {
            UserId = userId,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow.AddMonths(-5)
        };

        _dbContext.Users.Add(user);
        _dbContext.Tenants.Add(tenant);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new AnonymizeUserAccountCommandHandler(_dbContext, _auditSink);
        var command = new AnonymizeUserAccountCommand(
            userId,
            password,
            "EXCLUIR"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("User.SoleAdministratorOfActiveTenant");
        result.Error.Message.Should().Contain("Fazenda Solitária");
    }

    [Fact]
    public async Task Handle_Should_Succeed_When_User_Is_Only_Operator_In_Active_Tenant()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var password = "MinhaSenha@123";
        var user = new User
        {
            Id = userId,
            Email = "peao@agro.com.br",
            FullName = "Operador de Curral",
            PasswordHash = PasswordHasher.Hash(password)
        };

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Nelore",
            CNPJ = "44.333.222/0001-11",
            CnpjNormalized = "44333222000111",
            Status = TenantLifecycle.ToStatusString(TenantStatus.Active),
            SubscribedPlan = "Starter"
        };

        var userTenant = new UserTenant
        {
            UserId = userId,
            TenantId = tenant.Id,
            Role = UserRole.OperadorCurral,
            JoinedAt = DateTime.UtcNow.AddMonths(-1)
        };

        _dbContext.Users.Add(user);
        _dbContext.Tenants.Add(tenant);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new AnonymizeUserAccountCommandHandler(_dbContext, _auditSink);
        var command = new AnonymizeUserAccountCommand(
            userId,
            password,
            "EXCLUIR"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updatedUser = await _dbContext.Users.FindAsync(userId);
        updatedUser!.IsAnonymized.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_Succeed_When_Tenant_Is_Cancelled_Even_If_Sole_Admin()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var password = "MinhaSenha@123";
        var user = new User
        {
            Id = userId,
            Email = "cancelado@agro.com.br",
            FullName = "Ex Produtor",
            PasswordHash = PasswordHasher.Hash(password)
        };

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Cancelada",
            CNPJ = "55.666.777/0001-88",
            CnpjNormalized = "55666777000188",
            Status = TenantLifecycle.ToStatusString(TenantStatus.Cancelled),
            SubscribedPlan = "Starter"
        };

        var userTenant = new UserTenant
        {
            UserId = userId,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow.AddMonths(-10)
        };

        _dbContext.Users.Add(user);
        _dbContext.Tenants.Add(tenant);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new AnonymizeUserAccountCommandHandler(_dbContext, _auditSink);
        var command = new AnonymizeUserAccountCommand(
            userId,
            password,
            "EXCLUIR"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updatedUser = await _dbContext.Users.FindAsync(userId);
        updatedUser!.IsAnonymized.Should().BeTrue();
    }
}
