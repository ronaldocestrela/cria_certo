using CriaCerto.Web.Client.Models;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Layout;

public class NavigationMenuTests
{
    [Fact]
    public void NavigationCatalog_ShouldContainCoreBovineOperationalModules()
    {
        // Act
        var items = NavigationCatalog.GetAllItems();

        // Assert
        items.Should().NotBeEmpty();
        items.Should().Contain(i => i.Href == "analytics/executive-dashboard");
        items.Should().Contain(i => i.Href == "breeding/registry");
        items.Should().Contain(i => i.Href == "breeding/iatf");
        items.Should().Contain(i => i.Href == "breeding/diagnostico");
        items.Should().Contain(i => i.Href == "calving/records");
        items.Should().Contain(i => i.Href == "growth/pastures");
        items.Should().Contain(i => i.Href == "growth/curral-weighing");
        items.Should().Contain(i => i.Href == "sanitary/campaigns");
        items.Should().Contain(i => i.Href == "nutrition/silos");
        items.Should().Contain(i => i.Href == "nutrition/trough-log");
        items.Should().Contain(i => i.Href == "nutrition/pasture-supplementation");
    }

    [Fact]
    public void NavigationCatalog_ShouldNotContainObsoleteTemplateRoutes()
    {
        // Act
        var items = NavigationCatalog.GetAllItems();

        // Assert - verify neither weather nor counter exist in production mobile navigation
        items.Should().NotContain(i => i.Href.Contains("weather", StringComparison.OrdinalIgnoreCase));
        items.Should().NotContain(i => i.Href.Contains("counter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NavigationItems_ShouldHaveValidShortTitles_ForMobileRendering()
    {
        // Act
        var items = NavigationCatalog.GetAllItems();

        // Assert
        foreach (var item in items)
        {
            item.Href.Should().NotBeNullOrWhiteSpace();
            item.Title.Should().NotBeNullOrWhiteSpace();
            item.ShortTitle.Should().NotBeNullOrWhiteSpace();
            item.ShortTitle.Length.Should().BeLessThanOrEqualTo(20, "Mobile titles should be compact for horizontal scrolling");
        }
    }

    [Fact]
    public void GetOperationalItems_ShouldOnlyReturnBovineOperations()
    {
        // Act
        var operationalItems = NavigationCatalog.GetOperationalItems();

        // Assert
        operationalItems.Should().NotBeEmpty();
        operationalItems.Should().OnlyContain(i => i.Category == "Operações Bovinas");
    }

    [Fact]
    public void GetSettingsItems_ShouldOnlyReturnSettings()
    {
        // Act
        var settingsItems = NavigationCatalog.GetSettingsItems();

        // Assert
        settingsItems.Should().NotBeEmpty();
        settingsItems.Should().OnlyContain(i => i.Category == "Configurações");
    }

    [Fact]
    public void NavigationCatalog_ShouldNotHaveDuplicateHrefs()
    {
        // Act
        var allItems = NavigationCatalog.GetAllItems();

        // Assert
        allItems.Select(i => i.Href).Should().OnlyHaveUniqueItems();
    }
}
