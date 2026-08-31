using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Errors;

public static class ApprovalErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Approvals.NotFound", "A solicitação de aprovação administrativa não foi encontrada.");

    public static readonly Error CannotSelfApprove =
        Error.Conflict("Approvals.CannotSelfApprove", "Violação do princípio 4-Eyes: O solicitante da ação administrativa não pode autoaprovar ou autorejeitar a própria solicitação.");

    public static readonly Error AlreadyDecided =
        Error.Conflict("Approvals.AlreadyDecided", "A solicitação de aprovação já foi deliberada ou cancelada e não permite nova decisão.");

    public static readonly Error Expired =
        Error.Conflict("Approvals.Expired", "A solicitação de aprovação expirou o prazo limite de deliberação.");

    public static readonly Error CannotExecuteUnapproved =
        Error.Conflict("Approvals.CannotExecuteUnapproved", "Apenas solicitações com status Aprovado podem ser executadas.");

    public static readonly Error OnlyRequesterCanCancel =
        Error.Unauthorized("Approvals.OnlyRequesterCanCancel", "Apenas o administrador solicitante pode cancelar sua solicitação pendente.");

    public static readonly Error JustificationRequired =
        Error.Validation("Approvals.JustificationRequired", "A justificativa técnica da solicitação é obrigatória e deve possuir no mínimo 10 caracteres.");

    public static readonly Error TitleRequired =
        Error.Validation("Approvals.TitleRequired", "O título da solicitação de aprovação é obrigatório.");

    public static readonly Error TargetResourceRequired =
        Error.Validation("Approvals.TargetResourceRequired", "O identificador do recurso alvo é obrigatório.");

    public static readonly Error ImpactSummaryRequired =
        Error.Validation("Approvals.ImpactSummaryRequired", "O resumo de impacto operacional é obrigatório.");

    public static readonly Error PayloadRequired =
        Error.Validation("Approvals.PayloadRequired", "O payload de execução é obrigatório.");

    public static readonly Error RejectionReasonRequired =
        Error.Validation("Approvals.RejectionReasonRequired", "O motivo da rejeição da solicitação é obrigatório e deve possuir no mínimo 5 caracteres.");

    public static readonly Error ExecutionFailed =
        Error.Failure("Approvals.ExecutionFailed", "Ocorreu uma falha durante a execução automática da ação administrativa aprovada.");
}
