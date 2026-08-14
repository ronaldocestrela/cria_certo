namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public sealed class AdminSavedFilter
{
    public Guid Id { get; private set; }
    public Guid AdminUserId { get; private set; }
    public string Name { get; private set; } = default!;
    public string FilterJson { get; private set; } = default!;
    public bool IsDefault { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private AdminSavedFilter() { }

    public static AdminSavedFilter Create(Guid adminUserId, string name, string filterJson, bool isDefault)
    {
        var now = DateTime.UtcNow;
        return new AdminSavedFilter
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            Name = name.Trim(),
            FilterJson = filterJson,
            IsDefault = isDefault,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Update(string name, string filterJson, bool isDefault)
    {
        Name = name.Trim();
        FilterJson = filterJson;
        IsDefault = isDefault;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ClearDefaultFlag()
    {
        IsDefault = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
