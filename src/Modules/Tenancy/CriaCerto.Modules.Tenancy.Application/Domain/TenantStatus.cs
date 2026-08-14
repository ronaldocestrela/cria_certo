namespace CriaCerto.Modules.Tenancy.Application.Domain;

public enum TenantStatus
{
    Trial = 0,
    Active = 1,
    PastDue = 2,
    Suspended = 3,
    Cancelled = 4,
    Archived = 5
}
