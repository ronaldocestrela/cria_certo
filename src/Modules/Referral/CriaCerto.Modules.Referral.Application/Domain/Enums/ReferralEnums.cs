namespace CriaCerto.Modules.Referral.Application.Domain.Enums;

public enum ParticipantType
{
    Customer = 1,
    CommercialPartner = 2
}

public enum ParticipantStatus
{
    Active = 1,
    Suspended = 2
}

public enum CommissionType
{
    PercentageRecurring = 1,
    PercentageFirstMonth = 2,
    FixedPerConversion = 3
}

public enum PixKeyType
{
    Cpf = 1,
    Cnpj = 2,
    Email = 3,
    Phone = 4,
    RandomKey = 5
}

public enum DiscountType
{
    FixedValue = 1,
    Percentage = 2
}

public enum AttributionStatus
{
    Pending = 1,
    Converted = 2,
    Churned = 3,
    Fraudulent = 4
}

public enum RewardType
{
    InvoiceDiscountCredit = 1,
    CashCommission = 2
}

public enum LedgerRewardStatus
{
    PendingMaturity = 1,
    AvailableForPayout = 2,
    AppliedToInvoice = 3,
    PaidOut = 4,
    Cancelled = 5
}
