using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Referral.Application.Domain.Errors;

public static class ReferralErrors
{
    public static class Participant
    {
        public static readonly Error TenantRequiredForCustomer = Error.Validation(
            "ReferralParticipant.TenantRequiredForCustomer",
            "O TenantId é obrigatório para participantes do tipo Cliente.");

        public static readonly Error AdminRequiredForPartner = Error.Validation(
            "ReferralParticipant.AdminRequiredForPartner",
            "O identificador do administrador responsável no Backoffice é obrigatório para cadastrar um parceiro comercial.");

        public static readonly Error SuspensionReasonRequired = Error.Validation(
            "ReferralParticipant.SuspensionReasonRequired",
            "A justificativa/motivo para suspensão do participante é obrigatória.");
    }

    public static class Code
    {
        public static readonly Error NotFound = Error.NotFound(
            "ReferralCode.NotFound",
            "O código de indicação especificado não foi encontrado.");

        public static readonly Error InvalidFormat = Error.Validation(
            "ReferralCode.InvalidFormat",
            "O código de indicação deve possuir ao menos 3 caracteres alfanuméricos válidos.");

        public static readonly Error Inactive = Error.Validation(
            "ReferralCode.Inactive",
            "O código de indicação está inativo e não pode receber cliques ou ser utilizado.");
    }

    public static class Contract
    {
        public static readonly Error InvalidPercentageRate = Error.Validation(
            "PartnerContract.InvalidPercentageRate",
            "A taxa percentual de comissão deve ser maior que zero e menor ou igual a 100%.");

        public static readonly Error InvalidFixedAmount = Error.Validation(
            "PartnerContract.InvalidFixedAmount",
            "O valor fixo por conversão deve ser maior que zero.");

        public static readonly Error LegalDocumentRequired = Error.Validation(
            "PartnerContract.LegalDocumentRequired",
            "O número do documento fiscal (CPF ou CNPJ) é obrigatório.");

        public static readonly Error PixKeyRequired = Error.Validation(
            "PartnerContract.PixKeyRequired",
            "A chave Pix para repasse financeiro é obrigatória.");

        public static readonly Error InvalidCloseDate = Error.Validation(
            "PartnerContract.InvalidCloseDate",
            "A data de encerramento do contrato não pode ser anterior à data de início de vigência.");
    }

    public static class Policy
    {
        public static readonly Error InvalidDiscountValue = Error.Validation(
            "PlanReferralDiscountPolicy.InvalidDiscountValue",
            "O valor ou percentual de desconto deve ser maior que zero.");

        public static readonly Error PlanCatalogRequired = Error.Validation(
            "PlanReferralDiscountPolicy.PlanCatalogRequired",
            "O identificador do plano no catálogo é obrigatório.");
    }

    public static class Attribution
    {
        public static readonly Error SelfReferralBlocked = Error.Validation(
            "ReferralAttribution.SelfReferralBlocked",
            "Auto-indicação não é permitida. A fazenda indicada não pode ser a mesma do indicador.");

        public static readonly Error AlreadyConverted = Error.Validation(
            "ReferralAttribution.AlreadyConverted",
            "A indicação já foi convertida previamente com a confirmação de pagamento do plano.");

        public static readonly Error FraudReasonRequired = Error.Validation(
            "ReferralAttribution.FraudReasonRequired",
            "O motivo da identificação de fraude é obrigatório.");
    }

    public static class Ledger
    {
        public static readonly Error InvalidAmount = Error.Validation(
            "ReferralRewardLedger.InvalidAmount",
            "O valor da recompensa deve ser estritamente positivo.");

        public static readonly Error CancellationReasonRequired = Error.Validation(
            "ReferralRewardLedger.CancellationReasonRequired",
            "O motivo do cancelamento do lançamento no ledger é obrigatório.");

        public static readonly Error CannotCancelPaidOut = Error.Conflict(
            "ReferralRewardLedger.CannotCancelPaidOut",
            "Não é possível cancelar um lançamento cujo repasse financeiro já foi liquidado/pago.");

        public static readonly Error InvalidStateTransition = Error.Conflict(
            "ReferralRewardLedger.InvalidStateTransition",
            "A transição de status solicitada é inválida para o estado atual do lançamento.");
    }
}
