namespace CriaCerto.Modules.Tenancy.Application.Domain;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string CNPJ { get; set; } = string.Empty;
    public string CnpjNormalized { get; set; } = string.Empty;
    public string? ExternalIdentifier { get; set; }
    public string Status { get; set; } = "Active"; // Active, Suspended, Maintenance
    public string SubscribedPlan { get; set; } = "Starter"; // Starter, Pro, Enterprise
    public int Capacity { get; set; } = 1000;
    public string State { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string StateRegistration { get; set; } = string.Empty;
    public decimal AreaInHectares { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? TechnicalOwnerName { get; set; }
    public string? TechnicalOwnerEmail { get; set; }
    public string? CommercialOwnerName { get; set; }
    public string? CommercialOwnerEmail { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<UserTenant> UserTenants { get; set; } = new();
    public List<ProductionUnit> ProductionUnits { get; set; } = new();
}
