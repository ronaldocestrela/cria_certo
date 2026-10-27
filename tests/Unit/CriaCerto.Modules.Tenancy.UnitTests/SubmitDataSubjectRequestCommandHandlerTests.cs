using CriaCerto.BuildingBlocks.Abstractions.Email;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Features.Compliance.Dpo;
using CriaCerto.Modules.Tenancy.Application.Features.Compliance.Dpo.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class SubmitDataSubjectRequestCommandHandlerTests
{
    private readonly IEmailSender _emailSender;
    private readonly ITenancyAuditSink _auditSink;
    private readonly SubmitDataSubjectRequestCommandHandler _handler;

    public SubmitDataSubjectRequestCommandHandlerTests()
    {
        _emailSender = Substitute.For<IEmailSender>();
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(Result<EmailSendResult>.Success(new EmailSendResult("msg-mock-123")));

        _auditSink = Substitute.For<ITenancyAuditSink>();

        _handler = new SubmitDataSubjectRequestCommandHandler(
            _emailSender,
            _auditSink,
            NullLogger<SubmitDataSubjectRequestCommandHandler>.Instance
        );
    }

    [Fact]
    public async Task Handle_Should_Generate_Protocol_And_Return_Success_Receipt()
    {
        // Arrange
        var command = new SubmitDataSubjectRequestCommand(
            FullName: "Maria Pecuária Oliveira",
            Email: "maria@fazendasantatereza.com.br",
            PhoneNumber: "+55 (67) 98877-6655",
            DocumentNumber: "987.654.321-00",
            RelationshipType: DataSubjectRole.ProducerAdmin,
            RequestType: DataSubjectRequestType.AccessAndConfirmation,
            Description: "Solicito relatório detalhado de todos os dados pessoais e zootécnicos vinculados ao meu CPF.",
            IpAddress: "201.86.120.45",
            UserAgent: "Mozilla/5.0"
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ProtocolNumber.Should().StartWith("DSR-");
        result.Value.DataSubjectName.Should().Be("Maria Pecuária Oliveira");
        result.Value.DataSubjectEmail.Should().Be("maria@fazendasantatereza.com.br");
        result.Value.RelationshipType.Should().Be(DataSubjectRole.ProducerAdmin);
        result.Value.RequestType.Should().Be(DataSubjectRequestType.AccessAndConfirmation);
        result.Value.EstimatedBusinessDays.Should().Be(15);
        result.Value.MaxResponseDateUtc.Should().BeAfter(result.Value.SubmittedAtUtc);
        result.Value.DpoEmail.Should().Be("dpo@criacerto.com.br");

        // Verify Audit Log Invocation
        await _auditSink.Received(1).RecordDataSubjectRequestSubmittedAuditAsync(
            Arg.Is<string>(p => p.StartsWith("DSR-")),
            "maria@fazendasantatereza.com.br",
            DataSubjectRequestType.AccessAndConfirmation.ToString(),
            DataSubjectRole.ProducerAdmin.ToString(),
            "201.86.120.45",
            Arg.Any<CancellationToken>()
        );

        // Verify Emails Sent: 1 to Data Subject + 1 to DPO
        await _emailSender.Received(2).SendAsync(
            Arg.Any<EmailMessage>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_Should_Succeed_Even_If_Email_Sender_Fails_Resiliently()
    {
        // Arrange
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<EmailSendResult>(EmailErrors.NetworkFailure("Resend indisponível temporariamente.")));

        var command = new SubmitDataSubjectRequestCommand(
            FullName: "Carlos Vaqueiro",
            Email: "carlos@fazenda.com.br",
            PhoneNumber: null,
            DocumentNumber: null,
            RelationshipType: DataSubjectRole.FarmTeamMember,
            RequestType: DataSubjectRequestType.Rectification,
            Description: "Meu número de telefone cadastrado está incorreto e solicito a retificação formal.",
            IpAddress: "127.0.0.1"
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: Failure in transactional email should not abort protocol creation for the data subject
        result.IsSuccess.Should().BeTrue();
        result.Value.ProtocolNumber.Should().StartWith("DSR-");

        await _auditSink.Received(1).RecordDataSubjectRequestSubmittedAuditAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()
        );
    }
}
