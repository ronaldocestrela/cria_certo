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

        public static readonly Error NotFound = Error.NotFound(
            "ReferralParticipant.NotFound",
            "Participante do programa de indicação não encontrado.");
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

        public static readonly Error AlreadyExists = Error.Conflict(
            "ReferralCode.AlreadyExists",
            "O código de indicação informado já está em uso por outra fazenda ou parceiro.");
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

        public static readonly Error InvalidPercentageDiscountValue = Error.Validation(
            "PlanReferralDiscountPolicy.InvalidPercentageDiscountValue",
            "O percentual de desconto deve ser no máximo 100%.");

        public static readonly Error InvalidDiscountCap = Error.Validation(
            "PlanReferralDiscountPolicy.InvalidDiscountCap",
            "O teto máximo de desconto deve estar entre 1% e 100%.");

        public static readonly Error PlanCatalogRequired = Error.Validation(
            "PlanReferralDiscountPolicy.PlanCatalogRequired",
            "O identificador do plano no catálogo é obrigatório.");

        public static readonly Error NotFound = Error.NotFound(
            "PlanReferralDiscountPolicy.NotFound",
            "A política de desconto para o plano especificado não foi encontrada.");
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

        public static readonly Error TransactionReferenceRequired = Error.Validation(
            "ReferralRewardLedger.TransactionReferenceRequired",
            "O código ou identificador de transação bancária / Pix é obrigatório para registrar a baixa.");

        public static readonly Error ReceiptRequired = Error.Validation(
            "ReferralRewardLedger.ReceiptRequired",
            "O arquivo de comprovante de pagamento é obrigatório para registrar a baixa.");

        public static readonly Error NotFound = Error.NotFound(
            "ReferralRewardLedger.NotFound",
            "O lançamento de comissão especificado não foi encontrado no livro-razão.");
    }

    public static class PartnerProfile
    {
        public static readonly Error ParticipantRequired = Error.Validation(
            "CommercialPartnerProfile.ParticipantRequired",
            "O identificador do participante é obrigatório.");

        public static readonly Error FullNameRequired = Error.Validation(
            "CommercialPartnerProfile.FullNameRequired",
            "O nome completo ou razão social do parceiro comercial é obrigatório.");

        public static readonly Error EmailRequired = Error.Validation(
            "CommercialPartnerProfile.EmailRequired",
            "O e-mail de contato do parceiro comercial é obrigatório.");

        public static readonly Error PhoneRequired = Error.Validation(
            "CommercialPartnerProfile.PhoneRequired",
            "O telefone ou WhatsApp do parceiro comercial é obrigatório.");

        public static readonly Error EmailAlreadyInUse = Error.Conflict(
            "CommercialPartnerProfile.EmailAlreadyInUse",
            "Já existe um parceiro comercial cadastrado com este endereço de e-mail.");

        public static readonly Error DocumentAlreadyInUse = Error.Conflict(
            "CommercialPartnerProfile.DocumentAlreadyInUse",
            "Já existe um parceiro comercial cadastrado com este CPF/CNPJ.");

        public static readonly Error CodeAlreadyInUse = Error.Conflict(
            "ReferralCode.CodeAlreadyInUse",
            "O código de indicação informado já está em uso por outro parceiro ou campanha.");

        public static readonly Error NotFound = Error.NotFound(
            "CommercialPartnerProfile.NotFound",
            "O perfil do parceiro comercial não foi encontrado.");
    }

    public static class Receipt
    {
        public static readonly Error EmptyFile = Error.Validation(
            "PayoutReceipt.EmptyFile",
            "O arquivo de comprovante não possui conteúdo.");

        public static readonly Error FileTooLarge = Error.Validation(
            "PayoutReceipt.FileTooLarge",
            "O arquivo de comprovante não pode exceder 5 MB.");

        public static readonly Error InvalidExtension = Error.Validation(
            "PayoutReceipt.InvalidExtension",
            "Apenas arquivos PDF, PNG ou JPG/JPEG são permitidos como comprovante.");

        public static readonly Error FileNotFound = Error.NotFound(
            "PayoutReceipt.NotFound",
            "O arquivo do comprovante não foi encontrado no armazenamento.");

        public static readonly Error InvalidPath = Error.Validation(
            "PayoutReceipt.InvalidPath",
            "Caminho de arquivo inválido ou tentativa de acesso não autorizada.");
    }

    public static class PayoutBatch
    {
        public static readonly Error NoCommissionsSelected = Error.Validation(
            "PayoutBatch.NoCommissionsSelected",
            "Ao menos uma comissão liberada deve ser selecionada para efetuar a baixa.");

        public static readonly Error PartnerNotFound = Error.NotFound(
            "PayoutBatch.PartnerNotFound",
            "O parceiro comercial informado não foi encontrado.");

        public static readonly Error InvalidCommissionEntry = Error.Conflict(
            "PayoutBatch.InvalidCommissionEntry",
            "Uma ou mais comissões informadas não pertencem ao parceiro ou não estão disponíveis para pagamento.");
    }
}

