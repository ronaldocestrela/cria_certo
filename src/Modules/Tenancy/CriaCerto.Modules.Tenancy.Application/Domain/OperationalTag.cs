namespace CriaCerto.Modules.Tenancy.Application.Domain;

public sealed class OperationalTag
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#6366f1";
    public string Category { get; set; } = TenantSegmentationCatalog.TagCategories.Support;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<TenantOperationalTag> TenantTags { get; set; } = new();
}
