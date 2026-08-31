using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Features.Support.Dtos;
using MediatR;

namespace CriaCerto.Modules.Backoffice.Application.Features.Support.Queries;

public record GetSupportPlaybooksQuery() : IRequest<Result<IReadOnlyCollection<SupportPlaybookDto>>>;

public sealed class GetSupportPlaybooksQueryHandler : IRequestHandler<GetSupportPlaybooksQuery, Result<IReadOnlyCollection<SupportPlaybookDto>>>
{
    private static readonly IReadOnlyCollection<SupportPlaybookDto> Playbooks = new List<SupportPlaybookDto>
    {
        new(
            "pb-sync-01",
            "PB-SYNC-01",
            "Sincronização Offline Travada ou Conflito no Mangueiro",
            "Sincronização & PWA",
            "Procedimento para restabelecer a sincronização de dados coletados em campo (pesagens, inseminações, vacinas) quando o app relatar erro de sincronismo ou concorrência.",
            "RequestClientCacheReset",
            new List<PlaybookStepDto>
            {
                new(1, "Verificar conectividade e status do tenant", "Confirmar se o tenant está ativo e com internet disponível no alojamento ou sede.", true),
                new(2, "Checar a fila de operações pendentes", "Verificar no raio-X se há operações travadas ou divergência de timestamp.", true),
                new(3, "Solicitar Reset Seguro de Cache", "Disparar a ação 'RequestClientCacheReset' para invalidar o cache local e solicitar nova sincronização.", true),
                new(4, "Orientar o operador de campo", "Solicitar que o peão acesse 'Configurações > Sincronizar Agora' no app.", false)
            }),

        new(
            "pb-ent-02",
            "PB-ENT-02",
            "Módulo Inacessível após Migração de Plano",
            "Planos & Licenciamento",
            "Resolução de situações onde um cliente fez upgrade de plano mas os módulos novos continuam bloqueados na interface.",
            "ReconcileEntitlements",
            new List<PlaybookStepDto>
            {
                new(1, "Conferir versão do plano no catálogo", "Validar se a versão do plano contratado inclui os módulos solicitados.", true),
                new(2, "Verificar o status da assinatura", "Garantir que a assinatura não está suspensa por inadimplência.", true),
                new(3, "Executar Reconciliação de Direitos", "Disparar 'ReconcileEntitlements' para forçar recálculo imediato de claims e permissões.", true),
                new(4, "Validação de Acesso com o Usuário", "Pedir ao usuário para recarregar a página (F5) e confirmar o desbloqueio.", false)
            }),

        new(
            "pb-cap-03",
            "PB-CAP-03",
            "Alerta de Capacidade do Rebanho Excedida",
            "Capacidade & Quotas",
            "Tratamento de bloqueio de novos cadastros de animais por atingimento da cota máxima do plano contratado.",
            "ReconcileEntitlements",
            new List<PlaybookStepDto>
            {
                new(1, "Auditar contagem de animais ativos", "Checar no painel a quantidade de animais vivos registrados.", true),
                new(2, "Conferir se há baixas pendentes", "Verificar se vendas ou mortes já foram informadas mas não finalizadas.", false),
                new(3, "Reconciliar Quota de Capacidade", "Disparar 'ReconcileEntitlements' para recontagem oficial do rebanho.", true),
                new(4, "Encaminhar para Up-sell caso necessário", "Se o rebanho real exceder o plano, acionar o time de CS/Comercial.", false)
            }),

        new(
            "pb-queue-04",
            "PB-QUEUE-04",
            "Falha em Fila de Processamento Assíncrono",
            "Processamento Assíncrono",
            "Reprocessamento seguro de mensagens de fila ou jobs que falharam por intermitência técnica ou lock.",
            "RetryFailedQueueItems",
            new List<PlaybookStepDto>
            {
                new(1, "Inspecionar o log de falhas recentes", "Identificar a causa raiz (ex: timeout de banco ou concorrência).", true),
                new(2, "Verificar ausência de corrupção de dados", "Certificar que a falha não gerou duplicidade lógica.", true),
                new(3, "Executar Reprocessamento Seguro", "Disparar 'RetryFailedQueueItems' com ticket de suporte vinculado.", true),
                new(4, "Monitorar o esvaziamento da fila", "Acompanhar no indicador de fila até retorno ao estado 'Idle'.", false)
            }),

        new(
            "pb-lock-05",
            "PB-LOCK-05",
            "Liberação de Bloqueios Transitórios e Concorrência",
            "Acesso & Sessões",
            "Destravamento de recursos em caso de deadlocks temporários ou restrições de concorrência na fazenda.",
            "ResetTransientLocks",
            new List<PlaybookStepDto>
            {
                new(1, "Validar identidade do operador do suporte", "Confirmar se o solicitante possui perfil autorizado na fazenda.", true),
                new(2, "Verificar se há sessão de impersonação ativa", "Consultar se outro analista N2 já está atendendo o chamado.", true),
                new(3, "Executar Reset de Bloqueios", "Disparar 'ResetTransientLocks' para liberar recursos transitórios.", true),
                new(4, "Confirmar liberação com o cliente", "Verificar com o cliente se a ação pretendida foi concluída.", false)
            })
    };

    public Task<Result<IReadOnlyCollection<SupportPlaybookDto>>> Handle(GetSupportPlaybooksQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success(Playbooks));
    }
}
