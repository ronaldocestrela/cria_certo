using CriaCerto.Shared.UI.Models;
using CriaCerto.Shared.UI.Services;
using FluentAssertions;

namespace CriaCerto.Web.Client.UnitTests.Services;

public class ToastServiceTests
{
    private readonly ToastService _sut;

    public ToastServiceTests()
    {
        _sut = new ToastService();
    }

    [Fact]
    public void ShowSuccess_ShouldAddToastToActiveToasts_AndTriggerOnShowEvent()
    {
        // Arrange
        ToastMessage? receivedToast = null;
        _sut.OnShow += toast => receivedToast = toast;

        // Act
        _sut.ShowSuccess("Operação realizada com sucesso", "Sucesso");

        // Assert
        _sut.ActiveToasts.Should().HaveCount(1);
        receivedToast.Should().NotBeNull();
        receivedToast!.Level.Should().Be(ToastLevel.Success);
        receivedToast.Message.Should().Be("Operação realizada com sucesso");
        receivedToast.Title.Should().Be("Sucesso");
    }

    [Fact]
    public void ShowError_ShouldAddErrorToast_WithCustomTitleAndDuration()
    {
        // Arrange
        ToastMessage? receivedToast = null;
        _sut.OnShow += toast => receivedToast = toast;

        // Act
        _sut.ShowError("Falha de conexão com o servidor", "Erro de Rede", 8000);

        // Assert
        _sut.ActiveToasts.Should().HaveCount(1);
        receivedToast.Should().NotBeNull();
        receivedToast!.Level.Should().Be(ToastLevel.Error);
        receivedToast.Message.Should().Be("Falha de conexão com o servidor");
        receivedToast.Title.Should().Be("Erro de Rede");
        receivedToast.DurationMs.Should().Be(8000);
    }

    [Fact]
    public void ShowWarning_And_ShowInfo_ShouldAddCorrectToastLevels()
    {
        // Act
        _sut.ShowWarning("Estoque baixo de suplemento", "Atenção");
        _sut.ShowInfo("Nova atualização disponível", "Informação");

        // Assert
        _sut.ActiveToasts.Should().HaveCount(2);
        _sut.ActiveToasts[0].Level.Should().Be(ToastLevel.Warning);
        _sut.ActiveToasts[1].Level.Should().Be(ToastLevel.Info);
    }

    [Fact]
    public void RemoveToast_ShouldRemoveSpecifiedToast_AndTriggerOnHideEvent()
    {
        // Arrange
        Guid? hiddenToastId = null;
        _sut.OnHide += id => hiddenToastId = id;
        _sut.ShowSuccess("Mensagem temporária");
        var toastId = _sut.ActiveToasts.Single().Id;

        // Act
        _sut.RemoveToast(toastId);

        // Assert
        _sut.ActiveToasts.Should().BeEmpty();
        hiddenToastId.Should().Be(toastId);
    }
}
