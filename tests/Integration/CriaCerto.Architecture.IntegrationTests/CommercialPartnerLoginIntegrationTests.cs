using CriaCerto.Api.Services;
using CriaCerto.BuildingBlocks.Abstractions.Email;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Commands;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.ConfirmEmail;
using CriaCerto.Modules.Tenancy.Application.Features.Login;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CriaCerto.Architecture.IntegrationTests;

public sealed class CommercialPartnerLoginIntegrationTests : IDisposable
{
    private readonly SqliteConnection _tenancyConnection;
    private readonly SqliteConnection _referralConnection;
    private readonly TenancyDbContext _tenancyDbContext;
    private readonly ReferralDbContext _referralDbContext;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<PartnerAccountProvisioner> _provisionerLogger;
    private readonly IConfiguration _configuration;

    public CommercialPartnerLoginIntegrationTests()
    {
        _tenancyConnection = new SqliteConnection("DataSource=:memory:");
        _tenancyConnection.Open();
        var tenancyOptions = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_tenancyConnection)
            .Options;
        _tenancyDbContext = new TenancyDbContext(tenancyOptions);
        _tenancyDbContext.Database.EnsureCreated();

        _referralConnection = new SqliteConnection("DataSource=:memory:");
        _referralConnection.Open();
        var referralOptions = new DbContextOptionsBuilder<ReferralDbContext>()
            .UseSqlite(_referralConnection)
            .Options;
        _referralDbContext = new ReferralDbContext(referralOptions);
        _referralDbContext.Database.EnsureCreated();

        _emailSender = Substitute.For<IEmailSender>();
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new EmailSendResult("msg_test_123"))));

        _provisionerLogger = Substitute.For<ILogger<PartnerAccountProvisioner>>();

        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["App:WebBaseUrl"] = "http://localhost:5205",
            ["Jwt:SecretKey"] = "SuperSecretJwtKeyThatIsVeryLongAndSecureForTesting123!",
            ["Jwt:Issuer"] = "CriaCertoTest",
            ["Jwt:Audience"] = "CriaCertoTestClient"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();
    }

    public void Dispose()
    {
        _tenancyDbContext.Dispose();
        _referralDbContext.Dispose();
        _tenancyConnection.Dispose();
        _referralConnection.Dispose();
    }

    [Fact]
    public async Task RegisterPartner_ThenLogin_ShouldSucceed_AndReturnPartnerToken()
    {
        // 1. Setup Provisioner and Register Handler
        var provisioner = new PartnerAccountProvisioner(
            _tenancyDbContext,
            _configuration,
            _provisionerLogger,
            _emailSender);

        var registerHandler = new RegisterCommercialPartnerCommandHandler(_referralDbContext, provisioner);

        var adminId = Guid.NewGuid();
        var registerCommand = new RegisterCommercialPartnerCommand(
            FullName: "João da Silva",
            TradeName: "Silva Consultoria",
            Email: "joao.silva@consultoria.com.br",
            PhoneNumber: "+5567999887766",
            Category: PartnerCategory.ZootechnicianConsultant,
            City: "Campo Grande",
            State: "MS",
            Notes: "Parceiro Zootecnista",
            LegalDocumentNumber: "52.998.224/0001-90",
            PixKeyType: PixKeyType.Email,
            PixKey: "financeiro@consultoria.com.br",
            CommissionType: CommissionType.PercentageRecurring,
            CommissionValue: 20m,
            MaxRecurringMonths: 12,
            PaymentTermsDays: 30,
            PreferredReferralCode: "SILVA-AGRO",
            CreatedByAdminId: adminId);

        var registerResult = await registerHandler.Handle(registerCommand, CancellationToken.None);
        registerResult.IsSuccess.Should().BeTrue();
        registerResult.Value.UserId.Should().NotBeNull();

        // Check Tenancy DB has user - pre-confirmed for immediate login with provisional credentials
        var createdUser = await _tenancyDbContext.Users.FirstOrDefaultAsync(u => u.Email == "joao.silva@consultoria.com.br");
        createdUser.Should().NotBeNull();
        createdUser!.IsEmailConfirmed.Should().BeTrue();

        // Extract the temporary password sent via email
        var emailSent = _emailSender.ReceivedCalls().First();
        var emailMessage = (EmailMessage)emailSent.GetArguments()[0]!;
        emailMessage.HtmlBody.Should().Contain("CriaCerto#");

        // Extract temp password from email text/html
        var tempPassStart = emailMessage.HtmlBody.IndexOf("CriaCerto#");
        var tempPassword = emailMessage.HtmlBody.Substring(tempPassStart, 16); // e.g. CriaCerto#123456

        // 2. Setup Login Handler with PartnerRoleLookupService
        var partnerLookupService = new PartnerRoleLookupService(_referralDbContext);
        var jwtService = new CriaCerto.Modules.Tenancy.Infrastructure.Services.JwtService(_configuration);
        var loginHandler = new LoginCommandHandler(_tenancyDbContext, jwtService, partnerLookupService);

        // Act: Attempt login with the temp password and mixed-case email
        var loginResult = await loginHandler.Handle(new LoginCommand("  JOAO.SILVA@CONSULTORIA.COM.BR  ", tempPassword), CancellationToken.None);

        // Assert
        loginResult.IsSuccess.Should().BeTrue();
        loginResult.Value.IsPartner.Should().BeTrue();
        loginResult.Value.RedirectUrl.Should().Be("/partner/portal");
        loginResult.Value.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WhenPartnerUserIdWasNull_ShouldAutoReconcileAndGrantAccess()
    {
        // Arrange: Simulate existing partner whose UserId was null
        var adminId = Guid.NewGuid();
        var participantResult = CriaCerto.Modules.Referral.Application.Domain.ReferralProgramParticipant.CreateCommercialPartner(adminId);
        participantResult.IsSuccess.Should().BeTrue();
        var participant = participantResult.Value; // UserId is null!

        var profileResult = CriaCerto.Modules.Referral.Application.Domain.CommercialPartnerProfile.Create(
            participant.Id,
            "Dr. Carlos Orfao",
            "Carlos Consultoria",
            "carlos.orfao@agro.com.br",
            "+5567999112233",
            PartnerCategory.Veterinarian);
        profileResult.IsSuccess.Should().BeTrue();

        _referralDbContext.Participants.Add(participant);
        _referralDbContext.PartnerProfiles.Add(profileResult.Value);
        await _referralDbContext.SaveChangesAsync();

        // And in Tenancy, user exists with same email
        var user = new CriaCerto.Modules.Tenancy.Application.Domain.User
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Carlos Orfao",
            Email = "carlos.orfao@agro.com.br",
            PasswordHash = PasswordHasher.Hash("Senha123!"),
            IsEmailConfirmed = true
        };
        _tenancyDbContext.Users.Add(user);
        await _tenancyDbContext.SaveChangesAsync();

        var partnerLookupService = new PartnerRoleLookupService(_referralDbContext);
        var jwtService = new CriaCerto.Modules.Tenancy.Infrastructure.Services.JwtService(_configuration);
        var loginHandler = new LoginCommandHandler(_tenancyDbContext, jwtService, partnerLookupService);

        // Act
        var loginResult = await loginHandler.Handle(new LoginCommand("carlos.orfao@agro.com.br", "Senha123!"), CancellationToken.None);

        // Assert
        loginResult.IsSuccess.Should().BeTrue();
        loginResult.Value.IsPartner.Should().BeTrue();
        loginResult.Value.RedirectUrl.Should().Be("/partner/portal");

        // Verify participant is now reconciled with UserId in database
        var updatedParticipant = await _referralDbContext.Participants.FirstAsync(p => p.Id == participant.Id);
        updatedParticipant.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void DI_ShouldResolve_LoginCommandHandler_With_PartnerRoleLookupService()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        
        services.AddScoped<ITenancyDbContext>(_ => _tenancyDbContext);
        services.AddScoped<IReferralDbContext>(_ => _referralDbContext);
        services.AddScoped<IJwtService>(_ => new CriaCerto.Modules.Tenancy.Infrastructure.Services.JwtService(_configuration));
        services.AddScoped<IPartnerRoleLookupService, PartnerRoleLookupService>();
        services.AddScoped<IPartnerAccountProvisioner>(_ => new PartnerAccountProvisioner(_tenancyDbContext, _configuration, _provisionerLogger, _emailSender));
        
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
            typeof(LoginCommand).Assembly,
            typeof(RegisterCommercialPartnerCommand).Assembly));

        var sp = services.BuildServiceProvider();

        using var scope = sp.CreateScope();
        var loginHandler = scope.ServiceProvider.GetRequiredService<MediatR.IRequestHandler<LoginCommand, Result<AuthResponse>>>();
        loginHandler.Should().NotBeNull();
        loginHandler.Should().BeOfType<LoginCommandHandler>();
        var partnerLookupField = typeof(LoginCommandHandler).GetField("_partnerRoleLookupService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var partnerLookupValue = partnerLookupField!.GetValue(loginHandler);
        partnerLookupValue.Should().NotBeNull("IPartnerRoleLookupService should be injected into LoginCommandHandler by DI");

        var registerHandler = scope.ServiceProvider.GetRequiredService<MediatR.IRequestHandler<RegisterCommercialPartnerCommand, Result<CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Dtos.CommercialPartnerDto>>>();
        registerHandler.Should().NotBeNull();
        registerHandler.Should().BeOfType<RegisterCommercialPartnerCommandHandler>();
        var accountProvisionerField = typeof(RegisterCommercialPartnerCommandHandler).GetField("_accountProvisioner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var accountProvisionerValue = accountProvisionerField!.GetValue(registerHandler);
        accountProvisionerValue.Should().NotBeNull("IPartnerAccountProvisioner should be injected into RegisterCommercialPartnerCommandHandler by DI");
    }
}
