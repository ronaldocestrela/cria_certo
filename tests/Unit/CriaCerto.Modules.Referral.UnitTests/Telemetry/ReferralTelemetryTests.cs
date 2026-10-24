using System.Diagnostics.Metrics;
using CriaCerto.Modules.Referral.Application.Telemetry;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Telemetry;

public class ReferralTelemetryTests
{
    [Fact]
    public void Meter_ShouldHaveCorrectNameAndVersion()
    {
        ReferralTelemetry.Meter.Name.Should().Be("CriaCerto.Modules.Referral");
        ReferralTelemetry.Meter.Version.Should().Be("1.0.0");
        ReferralTelemetry.ActivitySource.Name.Should().Be("CriaCerto.Modules.Referral");
    }

    [Fact]
    public void RecordClick_ShouldIncrementCounter_WithTags()
    {
        // Arrange
        long measuredValue = 0;
        string? measuredChannel = null;
        string? measuredCode = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ReferralTelemetry.MeterName && instrument.Name == "referral.clicks.total")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            measuredValue += measurement;
            foreach (var tag in tags)
            {
                if (tag.Key == "channel") measuredChannel = tag.Value?.ToString();
                if (tag.Key == "code") measuredCode = tag.Value?.ToString();
            }
        });

        listener.Start();

        // Act
        ReferralTelemetry.RecordClick("Customer", "FAZENDA10");

        // Assert
        measuredValue.Should().Be(1);
        measuredChannel.Should().Be("Customer");
        measuredCode.Should().Be("FAZENDA10");
    }

    [Fact]
    public void RecordAttribution_ShouldIncrementCounter_WithTags()
    {
        // Arrange
        long measuredValue = 0;
        string? measuredChannel = null;
        string? measuredStatus = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ReferralTelemetry.MeterName && instrument.Name == "referral.attributions.total")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            measuredValue += measurement;
            foreach (var tag in tags)
            {
                if (tag.Key == "channel") measuredChannel = tag.Value?.ToString();
                if (tag.Key == "status") measuredStatus = tag.Value?.ToString();
            }
        });

        listener.Start();

        // Act
        ReferralTelemetry.RecordAttribution("CommercialPartner", "Converted");

        // Assert
        measuredValue.Should().Be(1);
        measuredChannel.Should().Be("CommercialPartner");
        measuredStatus.Should().Be("Converted");
    }

    [Fact]
    public void RecordReward_ShouldIncrementBothCountAndAmount_WithTags()
    {
        // Arrange
        long measuredCount = 0;
        double measuredAmount = 0;
        string? measuredRewardType = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ReferralTelemetry.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "referral.rewards.total")
            {
                measuredCount += measurement;
                foreach (var tag in tags)
                {
                    if (tag.Key == "reward_type") measuredRewardType = tag.Value?.ToString();
                }
            }
        });

        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "referral.rewards.amount")
            {
                measuredAmount += measurement;
            }
        });

        listener.Start();

        // Act
        ReferralTelemetry.RecordReward("InvoiceDiscountCredit", 149.90, "BRL");

        // Assert
        measuredCount.Should().Be(1);
        measuredAmount.Should().Be(149.90);
        measuredRewardType.Should().Be("InvoiceDiscountCredit");
    }
}
