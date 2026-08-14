using CriaCerto.Modules.Tenancy.Application.Domain;
using FluentAssertions;

namespace CriaCerto.Modules.Tenancy.UnitTests.Domain;

public class TenantLifecycleTests
{
    [Theory]
    [InlineData(TenantStatus.Trial, TenantStatus.Active, true)]
    [InlineData(TenantStatus.Trial, TenantStatus.Suspended, true)]
    [InlineData(TenantStatus.Trial, TenantStatus.Cancelled, true)]
    [InlineData(TenantStatus.Active, TenantStatus.PastDue, true)]
    [InlineData(TenantStatus.Active, TenantStatus.Suspended, true)]
    [InlineData(TenantStatus.Active, TenantStatus.Cancelled, true)]
    [InlineData(TenantStatus.PastDue, TenantStatus.Active, true)]
    [InlineData(TenantStatus.PastDue, TenantStatus.Suspended, true)]
    [InlineData(TenantStatus.Suspended, TenantStatus.Active, true)]
    [InlineData(TenantStatus.Suspended, TenantStatus.Cancelled, true)]
    [InlineData(TenantStatus.Cancelled, TenantStatus.Archived, true)]
    [InlineData(TenantStatus.Archived, TenantStatus.Active, false)]
    [InlineData(TenantStatus.Active, TenantStatus.Archived, false)]
    [InlineData(TenantStatus.Trial, TenantStatus.PastDue, false)]
    public void CanTransition_Should_Match_Matrix(TenantStatus from, TenantStatus to, bool expected)
    {
        TenantLifecycle.CanTransition(from, to).Should().Be(expected);
    }

    [Theory]
    [InlineData(TenantStatus.Trial, true)]
    [InlineData(TenantStatus.Active, true)]
    [InlineData(TenantStatus.PastDue, true)]
    [InlineData(TenantStatus.Suspended, false)]
    [InlineData(TenantStatus.Cancelled, false)]
    [InlineData(TenantStatus.Archived, false)]
    public void CanProducerAccess_Should_Allow_Grace_States(TenantStatus status, bool expected)
    {
        TenantLifecycle.CanProducerAccess(status).Should().Be(expected);
    }

    [Fact]
    public void TryParseStatus_Should_Map_Maintenance_To_Suspended()
    {
        TenantLifecycle.TryParseStatus("Maintenance", out var status).Should().BeTrue();
        status.Should().Be(TenantStatus.Suspended);
    }
}
