using System.Reflection;
using CriaCerto.Web.Client.Pages.Breeding;
using CriaCerto.Web.Client.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Pwa;

public class PwaFieldPagesOfflineStateTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    private static readonly string BatchBreedingLogPath = Path.Combine(
        RepoRoot,
        "src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Breeding/BatchBreedingLog.razor");

    private static readonly string PregnancyCheckTasksPath = Path.Combine(
        RepoRoot,
        "src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Breeding/PregnancyCheckTasks.razor");

    [Fact]
    public void FieldPages_ShouldInjectOfflineSyncServiceAndImplementIDisposable()
    {
        File.Exists(BatchBreedingLogPath).Should().BeTrue();
        File.Exists(PregnancyCheckTasksPath).Should().BeTrue();

        var batchContent = File.ReadAllText(BatchBreedingLogPath);
        var pregnancyContent = File.ReadAllText(PregnancyCheckTasksPath);

        batchContent.Should().Contain("@implements IDisposable");
        batchContent.Should().Contain("@inject IOfflineSyncService SyncService");
        typeof(IDisposable).IsAssignableFrom(typeof(BatchBreedingLog)).Should().BeTrue();

        pregnancyContent.Should().Contain("@implements IDisposable");
        pregnancyContent.Should().Contain("@inject IOfflineSyncService SyncService");
        typeof(IDisposable).IsAssignableFrom(typeof(PregnancyCheckTasks)).Should().BeTrue();
    }

    [Fact]
    public void FieldPages_ShouldSubscribeOnInitializedAndUnsubscribeOnDispose()
    {
        var batchContent = File.ReadAllText(BatchBreedingLogPath);
        var pregnancyContent = File.ReadAllText(PregnancyCheckTasksPath);

        batchContent.Should().Contain("SyncService.OnStateChanged += HandleSyncStateChanged;");
        batchContent.Should().Contain("SyncService.OnStateChanged -= HandleSyncStateChanged;");

        pregnancyContent.Should().Contain("SyncService.OnStateChanged += HandleSyncStateChanged;");
        pregnancyContent.Should().Contain("SyncService.OnStateChanged -= HandleSyncStateChanged;");
    }

    [Fact]
    public void FieldPages_ShouldDelegateSyncPendingAndHandleMidRequestOfflineFallback()
    {
        var batchContent = File.ReadAllText(BatchBreedingLogPath);
        var pregnancyContent = File.ReadAllText(PregnancyCheckTasksPath);

        // SyncPendingAsync deve delegar diretamente sem callbacks legados manuais
        batchContent.Should().Contain("await BreedingOps.SyncPendingOpsAsync();");
        batchContent.Should().NotContain("type == \"breedingBatch\"");

        pregnancyContent.Should().Contain("await BreedingOps.SyncPendingOpsAsync();");
        pregnancyContent.Should().NotContain("type == \"breedingBatch\"");

        // Ambas as páginas devem verificar se a conexão caiu durante o envio (!SyncService.IsOnline)
        // e validar o retorno booleano do enfileiramento offline
        batchContent.Should().Contain("else if (!SyncService.IsOnline)");
        batchContent.Should().Contain("var enqueued = await BreedingOps.EnqueueBreedingBatchAsync(request);");

        pregnancyContent.Should().Contain("if (response is not true)");
        pregnancyContent.Should().Contain("else if (!SyncService.IsOnline)");
        pregnancyContent.Should().Contain("var enqueued = await BreedingOps.EnqueueDiagnosisAsync(request);");
    }

    [Fact]
    public void BatchBreedingLog_ShouldUpdateOfflineStateReactively_AndStopListeningAfterDispose()
    {
        // Arrange
        var syncServiceMock = Substitute.For<IOfflineSyncService>();
        syncServiceMock.IsOnline.Returns(true);
        syncServiceMock.PendingCount.Returns(0);

        var page = new BatchBreedingLog();
        SetProperty(page, "SyncService", syncServiceMock);

        var handler = CreateStateChangedDelegate(page);
        syncServiceMock.OnStateChanged += handler;

        // Act 1: Queda de sinal no curral com 3 itens pendentes
        syncServiceMock.IsOnline.Returns(false);
        syncServiceMock.PendingCount.Returns(3);
        InvokeApplySyncState(page);

        // Assert 1
        GetField<bool>(page, "_isOnline").Should().BeFalse();
        GetField<int>(page, "_pendingCount").Should().Be(3);
        GetField<string>(page, "_offlineMessage").Should().Be("Modo Offline - 3 operação(ões) pendente(s)");

        // Act 2: Dispose remove a inscrição do evento OnStateChanged
        page.Dispose();
        syncServiceMock.Received(1).OnStateChanged -= Arg.Any<Action>();
    }

    [Fact]
    public void PregnancyCheckTasks_ShouldUpdateOfflineStateReactively_AndStopListeningAfterDispose()
    {
        // Arrange
        var syncServiceMock = Substitute.For<IOfflineSyncService>();
        syncServiceMock.IsOnline.Returns(true);
        syncServiceMock.PendingCount.Returns(0);

        var page = new PregnancyCheckTasks();
        SetProperty(page, "SyncService", syncServiceMock);

        var handler = CreateStateChangedDelegate(page);
        syncServiceMock.OnStateChanged += handler;

        // Act 1: Queda de sinal no curral sem itens pendentes
        syncServiceMock.IsOnline.Returns(false);
        syncServiceMock.PendingCount.Returns(0);
        InvokeApplySyncState(page);

        // Assert 1
        GetField<bool>(page, "_isOnline").Should().BeFalse();
        GetField<int>(page, "_pendingCount").Should().Be(0);
        GetField<string>(page, "_offlineMessage").Should().Be("Modo Offline - Sincronização pendente");

        // Act 2: Novo diagnóstico enfileirado offline
        syncServiceMock.PendingCount.Returns(2);
        InvokeApplySyncState(page);

        // Assert 2
        GetField<int>(page, "_pendingCount").Should().Be(2);
        GetField<string>(page, "_offlineMessage").Should().Be("Modo Offline - 2 operação(ões) pendente(s)");

        // Act 3: Dispose remove a inscrição do evento OnStateChanged
        page.Dispose();
        syncServiceMock.Received(1).OnStateChanged -= Arg.Any<Action>();
    }

    [Fact]
    public async Task BatchBreedingLog_ConfirmBatch_WhenNetworkDropsMidRequest_ShouldFallbackToOfflineFeedback()
    {
        // Arrange
        var jsRuntimeMock = Substitute.For<Microsoft.JSInterop.IJSRuntime>();
        var syncServiceMock = Substitute.For<IOfflineSyncService>();
        var isOnline = true;
        var pendingCount = 0;

        syncServiceMock.IsOnline.Returns(_ => isOnline);
        syncServiceMock.PendingCount.Returns(_ => pendingCount);
        syncServiceMock.When(x => x.SetNetworkStatus(Arg.Any<bool>()))
            .Do(call => isOnline = call.Arg<bool>());
        syncServiceMock
            .EnqueueOperationAsync("Breeding", "RegisterBreedingBatch", Arg.Any<CriaCerto.Web.Client.Models.RegisterBreedingBatchRequest>())
            .Returns(_ =>
            {
                pendingCount++;
                return CriaCerto.BuildingBlocks.Abstractions.Results.Result.Success();
            });

        using var httpClient = new HttpClient(new ThrowingNetworkHandler())
        {
            BaseAddress = new Uri("https://localhost:7000/")
        };
        var breedingOps = new BreedingOpsApiClient(httpClient, jsRuntimeMock, syncServiceMock);

        var page = new BatchBreedingLog();
        SetProperty(page, "SyncService", syncServiceMock);
        SetProperty(page, "BreedingOps", breedingOps);

        var entries = GetField<List<CriaCerto.Web.Client.Models.BreedingEntryFormModel>>(page, "_entries");
        entries[0].TagId = "BR-205";
        entries[0].BoarOrSemenRef = "NELORE-01";

        // Act
        var confirmMethod = typeof(BatchBreedingLog).GetMethod("ConfirmBatch", BindingFlags.Instance | BindingFlags.NonPublic);
        confirmMethod.Should().NotBeNull();
        await (Task)confirmMethod!.Invoke(page, null)!;

        // Assert
        GetField<bool>(page, "_isOnline").Should().BeFalse();
        GetField<int>(page, "_pendingCount").Should().Be(1);
        GetField<string?>(page, "_feedback").Should().Be("Lote salvo offline. Será sincronizado quando houver conexão.");
    }

    [Fact]
    public async Task PregnancyCheckTasks_SubmitDiagnosis_WhenNetworkDropsMidRequest_ShouldFallbackToOfflineFeedback()
    {
        // Arrange
        var jsRuntimeMock = Substitute.For<Microsoft.JSInterop.IJSRuntime>();
        var syncServiceMock = Substitute.For<IOfflineSyncService>();
        var isOnline = true;
        var pendingCount = 0;

        syncServiceMock.IsOnline.Returns(_ => isOnline);
        syncServiceMock.PendingCount.Returns(_ => pendingCount);
        syncServiceMock.When(x => x.SetNetworkStatus(Arg.Any<bool>()))
            .Do(call => isOnline = call.Arg<bool>());
        syncServiceMock
            .EnqueueOperationAsync("Breeding", "RegisterDiagnosis", Arg.Any<CriaCerto.Web.Client.Models.RegisterPregnancyDiagnosisRequest>())
            .Returns(_ =>
            {
                pendingCount++;
                return CriaCerto.BuildingBlocks.Abstractions.Results.Result.Success();
            });

        using var httpClient = new HttpClient(new ThrowingNetworkHandler())
        {
            BaseAddress = new Uri("https://localhost:7000/")
        };
        var breedingOps = new BreedingOpsApiClient(httpClient, jsRuntimeMock, syncServiceMock);

        var page = new PregnancyCheckTasks();
        SetProperty(page, "SyncService", syncServiceMock);
        SetProperty(page, "BreedingOps", breedingOps);

        var taskDto = new CriaCerto.Web.Client.Models.PregnancyCheckTaskDto(
            Guid.NewGuid(),
            "BR-404",
            "Nelore",
            "Piquete 02",
            Guid.NewGuid(),
            new DateOnly(2026, 10, 21),
            24,
            "Ricardo S.",
            CriaCerto.Web.Client.Models.BreedingMethod.TimedArtificialInsemination,
            2,
            false);

        var tasks = GetField<List<CriaCerto.Web.Client.Models.PregnancyCheckTaskDto>>(page, "_tasks");
        tasks.Add(taskDto);

        // Act
        var submitMethod = typeof(PregnancyCheckTasks).GetMethod("SubmitDiagnosis", BindingFlags.Instance | BindingFlags.NonPublic);
        submitMethod.Should().NotBeNull();
        await (Task)submitMethod!.Invoke(page, [taskDto, CriaCerto.Web.Client.Models.PregnancyDiagnosisResult.Pregnant])!;

        // Assert
        GetField<bool>(page, "_isOnline").Should().BeFalse();
        GetField<int>(page, "_pendingCount").Should().Be(1);
        tasks.Should().BeEmpty();
        GetField<string?>(page, "_feedback").Should().Be("BR-404 salvo offline para sincronização.");
    }

    private sealed class ThrowingNetworkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Queda abrupta de sinal 4G/Wi-Fi no curral.");
    }

    private static Action CreateStateChangedDelegate(object target)
    {
        var method = target.GetType().GetMethod("HandleSyncStateChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        return (Action)Delegate.CreateDelegate(typeof(Action), target, method!);
    }

    private static void InvokeApplySyncState(object target)
    {
        var method = target.GetType().GetMethod("ApplySyncStateFromService", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull("as páginas de campo devem sincronizar _isOnline, _pendingCount e _offlineMessage a partir do SyncService");
        method!.Invoke(target, null);
    }

    private static void SetProperty(object target, string propertyName, object value)
    {
        var prop = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        prop.Should().NotBeNull();
        prop!.SetValue(target, value);
    }

    private static T GetField<T>(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        return (T)field!.GetValue(target)!;
    }

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "agents.md")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}

