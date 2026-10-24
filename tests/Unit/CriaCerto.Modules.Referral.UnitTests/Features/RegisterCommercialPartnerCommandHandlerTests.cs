using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Commands;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
namespace CriaCerto.Modules.Referral.UnitTests.Features;

public sealed class RegisterCommercialPartnerCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ReferralDbContext _dbContext;
    private readonly TestPartnerAccountProvisioner _accountProvisioner;

    public RegisterCommercialPartnerCommandHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ReferralDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new ReferralDbContext(options);
        _dbContext.Database.EnsureCreated();

        _accountProvisioner = new TestPartnerAccountProvisioner(Guid.NewGuid());
    }

    private sealed class TestPartnerAccountProvisioner : IPartnerAccountProvisioner
    {
        private readonly Result<Guid> _result;

        public TestPartnerAccountProvisioner(Guid userId)
        {
            _result = Result.Success(userId);
        }

        public TestPartnerAccountProvisioner(Result<Guid> result)
        {
            _result = result;
        }

        public Task<Result<Guid>> ProvisionPartnerUserAsync(
            string fullName,
            string email,
            string? phoneNumber,
            string referralCode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }


    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldCreatePartnerProfileContractAndCode()
    {
        // Arrange
        var handler = new RegisterCommercialPartnerCommandHandler(_dbContext, _accountProvisioner);
        var command = new RegisterCommercialPartnerCommand(
            FullName: "Dr. João Silva",
            TradeName: "Silva Consultoria",
            Email: "joao.silva@consultoria.com",
            PhoneNumber: "+5567999887766",
            Category: PartnerCategory.ZootechnicianConsultant,
            City: "Campo Grande",
            State: "MS",
            Notes: "Parceiro especialista em confinamento",
            LegalDocumentNumber: "52.998.224/0001-90", // CNPJ válido
            PixKeyType: PixKeyType.Email,
            PixKey: "financeiro@consultoria.com",
            CommissionType: CommissionType.PercentageRecurring,
            CommissionValue: 20m,
            MaxRecurringMonths: 12,
            PaymentTermsDays: 30,
            PreferredReferralCode: "SILVA-AGRO",
            CreatedByAdminId: Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FullName.Should().Be("Dr. João Silva");
        result.Value.TradeName.Should().Be("Silva Consultoria");
        result.Value.ReferralCode.Should().Be("SILVA-AGRO");
        result.Value.CommissionType.Should().Be(CommissionType.PercentageRecurring);
        result.Value.CommissionValue.Should().Be(20m);
        result.Value.MaxRecurringMonths.Should().Be(12);
        result.Value.Status.Should().Be(ParticipantStatus.Active);
        result.Value.UserId.Should().NotBeNull();

        // Check DbContext
        var savedProfile = await _dbContext.PartnerProfiles.FirstOrDefaultAsync();
        savedProfile.Should().NotBeNull();
        savedProfile!.Email.Should().Be("joao.silva@consultoria.com");

        var savedContract = await _dbContext.PartnerContracts.FirstOrDefaultAsync();
        savedContract.Should().NotBeNull();
        savedContract!.IsActive.Should().BeTrue();

        var savedCode = await _dbContext.ReferralCodes.FirstOrDefaultAsync();
        savedCode.Should().NotBeNull();
        savedCode!.Code.Should().Be("SILVA-AGRO");
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ShouldReturnConflictFailure()
    {
        // Arrange
        var handler = new RegisterCommercialPartnerCommandHandler(_dbContext, _accountProvisioner);
        var command1 = new RegisterCommercialPartnerCommand(
            FullName: "Dr. João Silva",
            TradeName: "Silva Consultoria",
            Email: "duplicado@consultoria.com",
            PhoneNumber: "+5567999887766",
            Category: PartnerCategory.ZootechnicianConsultant,
            City: "Campo Grande",
            State: "MS",
            Notes: null,
            LegalDocumentNumber: "52.998.224/0001-90",
            PixKeyType: PixKeyType.Email,
            PixKey: "duplicado@consultoria.com",
            CommissionType: CommissionType.PercentageRecurring,
            CommissionValue: 20m,
            MaxRecurringMonths: 12,
            PaymentTermsDays: 30,
            PreferredReferralCode: "SILVA-AGRO",
            CreatedByAdminId: Guid.NewGuid());

        await handler.Handle(command1, CancellationToken.None);

        var command2 = new RegisterCommercialPartnerCommand(
            FullName: "Outro Parceiro",
            TradeName: "Outra Consultoria",
            Email: "duplicado@consultoria.com", // mesmo email
            PhoneNumber: "+5567999887766",
            Category: PartnerCategory.ZootechnicianConsultant,
            City: "Campo Grande",
            State: "MS",
            Notes: null,
            LegalDocumentNumber: "00.000.000/0001-91",
            PixKeyType: PixKeyType.Email,
            PixKey: "outro@consultoria.com",
            CommissionType: CommissionType.PercentageRecurring,
            CommissionValue: 20m,
            MaxRecurringMonths: 12,
            PaymentTermsDays: 30,
            PreferredReferralCode: "OUTRO-CODIGO",
            CreatedByAdminId: Guid.NewGuid());

        // Act
        var result = await handler.Handle(command2, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CommercialPartnerProfile.EmailAlreadyInUse");
    }

    [Fact]
    public async Task Handle_WithDuplicateDocument_ShouldReturnConflictFailure()
    {
        // Arrange
        var handler = new RegisterCommercialPartnerCommandHandler(_dbContext, _accountProvisioner);
        var command1 = new RegisterCommercialPartnerCommand(
            FullName: "Dr. João Silva",
            TradeName: "Silva Consultoria",
            Email: "parceiro1@consultoria.com",
            PhoneNumber: "+5567999887766",
            Category: PartnerCategory.ZootechnicianConsultant,
            City: "Campo Grande",
            State: "MS",
            Notes: null,
            LegalDocumentNumber: "52.998.224/0001-90",
            PixKeyType: PixKeyType.Email,
            PixKey: "duplicado@consultoria.com",
            CommissionType: CommissionType.PercentageRecurring,
            CommissionValue: 20m,
            MaxRecurringMonths: 12,
            PaymentTermsDays: 30,
            PreferredReferralCode: "SILVA-AGRO",
            CreatedByAdminId: Guid.NewGuid());

        await handler.Handle(command1, CancellationToken.None);

        var command2 = new RegisterCommercialPartnerCommand(
            FullName: "Outro Parceiro",
            TradeName: "Outra Consultoria",
            Email: "parceiro2@consultoria.com",
            PhoneNumber: "+5567999887766",
            Category: PartnerCategory.ZootechnicianConsultant,
            City: "Campo Grande",
            State: "MS",
            Notes: null,
            LegalDocumentNumber: "52.998.224/0001-90", // mesmo documento
            PixKeyType: PixKeyType.Email,
            PixKey: "outro@consultoria.com",
            CommissionType: CommissionType.PercentageRecurring,
            CommissionValue: 20m,
            MaxRecurringMonths: 12,
            PaymentTermsDays: 30,
            PreferredReferralCode: "OUTRO-CODIGO",
            CreatedByAdminId: Guid.NewGuid());

        // Act
        var result = await handler.Handle(command2, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CommercialPartnerProfile.DocumentAlreadyInUse");
    }

    [Fact]
    public async Task Handle_WhenAccountProvisionerFails_ShouldReturnFailureAndNotPersistRecords()
    {
        // Arrange
        var failingProvisioner = new TestPartnerAccountProvisioner(
            Result.Failure<Guid>(Error.Failure("Auth.UserCreationError", "Não foi possível criar o usuário no Identity.")));

        var handler = new RegisterCommercialPartnerCommandHandler(_dbContext, failingProvisioner);
        var command = new RegisterCommercialPartnerCommand(
            FullName: "Dr. João Silva",
            TradeName: "Silva Consultoria",
            Email: "falha.provisionamento@consultoria.com",
            PhoneNumber: "+5567999887766",
            Category: PartnerCategory.ZootechnicianConsultant,
            City: "Campo Grande",
            State: "MS",
            Notes: null,
            LegalDocumentNumber: "52.998.224/0001-90",
            PixKeyType: PixKeyType.Email,
            PixKey: "falha@consultoria.com",
            CommissionType: CommissionType.PercentageRecurring,
            CommissionValue: 20m,
            MaxRecurringMonths: 12,
            PaymentTermsDays: 30,
            PreferredReferralCode: "SILVA-AGRO",
            CreatedByAdminId: Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.UserCreationError");

        // DB should remain clean - no orphaned records
        var savedParticipant = await _dbContext.Participants.FirstOrDefaultAsync();
        savedParticipant.Should().BeNull();

        var savedProfile = await _dbContext.PartnerProfiles.FirstOrDefaultAsync();
        savedProfile.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenAccountProvisionerReturnsEmptyGuid_ShouldReturnFailureAndNotPersistRecords()
    {
        // Arrange
        var emptyGuidProvisioner = new TestPartnerAccountProvisioner(Result.Success(Guid.Empty));

        var handler = new RegisterCommercialPartnerCommandHandler(_dbContext, emptyGuidProvisioner);
        var command = new RegisterCommercialPartnerCommand(
            FullName: "Dr. João Silva",
            TradeName: "Silva Consultoria",
            Email: "empty.guid@consultoria.com",
            PhoneNumber: "+5567999887766",
            Category: PartnerCategory.ZootechnicianConsultant,
            City: "Campo Grande",
            State: "MS",
            Notes: null,
            LegalDocumentNumber: "52.998.224/0001-90",
            PixKeyType: PixKeyType.Email,
            PixKey: "empty@consultoria.com",
            CommissionType: CommissionType.PercentageRecurring,
            CommissionValue: 20m,
            MaxRecurringMonths: 12,
            PaymentTermsDays: 30,
            PreferredReferralCode: "SILVA-AGRO",
            CreatedByAdminId: Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CommercialPartnerProfile.UserProvisioningFailed");

        // DB should remain clean
        var savedParticipant = await _dbContext.Participants.FirstOrDefaultAsync();
        savedParticipant.Should().BeNull();
    }
}
