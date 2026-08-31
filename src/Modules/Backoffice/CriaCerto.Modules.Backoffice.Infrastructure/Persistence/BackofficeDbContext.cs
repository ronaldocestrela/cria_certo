using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Infrastructure.Persistence;

public class BackofficeDbContext : DbContext
{
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AdminRole> AdminRoles => Set<AdminRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<AdminSession> AdminSessions => Set<AdminSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AdminSavedFilter> AdminSavedFilters => Set<AdminSavedFilter>();
    public DbSet<PlanCatalog> PlanCatalogs => Set<PlanCatalog>();
    public DbSet<PlanVersion> PlanVersions => Set<PlanVersion>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<PlanLimit> PlanLimits => Set<PlanLimit>();
    public DbSet<ImpersonationSession> ImpersonationSessions => Set<ImpersonationSession>();
    public DbSet<AdminApprovalRequest> AdminApprovalRequests => Set<AdminApprovalRequest>();

    public BackofficeDbContext(DbContextOptions<BackofficeDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("backoffice");

        modelBuilder.Entity<AdminUser>(builder =>
        {
            builder.ToTable("AdminUsers");
            builder.HasKey(u => u.Id);
            builder.Property(u => u.Name).IsRequired().HasMaxLength(200);
            builder.Property(u => u.Email).IsRequired().HasMaxLength(200);
            builder.Property(u => u.PasswordHash).IsRequired();
            builder.Property(u => u.IsActive);
            builder.Property(u => u.MfaEnabled);
            builder.Property(u => u.MfaSecretKey).HasMaxLength(500);
            builder.Property(u => u.MustChangePasswordOnNextLogin);
            builder.Property(u => u.CreatedAtUtc);
            builder.Property(u => u.LastLoginAtUtc);
            builder.HasIndex(u => u.Email).IsUnique();
            builder.HasMany(u => u.Roles).WithMany();
        });

        modelBuilder.Entity<AdminRole>(builder =>
        {
            builder.ToTable("AdminRoles");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
            builder.HasMany(r => r.Permissions).WithMany();
        });

        modelBuilder.Entity<Permission>(builder =>
        {
            builder.ToTable("Permissions");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<AdminSession>(builder =>
        {
            builder.ToTable("AdminSessions");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.SessionToken).IsRequired().HasMaxLength(500);
            builder.Property(s => s.RefreshToken).HasMaxLength(500);
            builder.HasIndex(s => s.AdminUserId);
            builder.HasIndex(s => s.SessionToken);
            builder.HasIndex(s => s.RefreshToken);
        });

        modelBuilder.Entity<AuditLog>(builder =>
        {
            builder.ToTable("AuditLogs");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.AdminUserEmail).IsRequired().HasMaxLength(200);
            builder.Property(a => a.ActorRole).HasMaxLength(100);
            builder.Property(a => a.Action).IsRequired().HasMaxLength(100);
            builder.Property(a => a.Category).HasConversion<string>().IsRequired().HasMaxLength(50);
            builder.Property(a => a.Severity).HasConversion<string>().IsRequired().HasMaxLength(30);
            builder.Property(a => a.Resource).IsRequired().HasMaxLength(200);
            builder.Property(a => a.TargetTenantName).HasMaxLength(200);
            builder.Property(a => a.IpAddress).IsRequired().HasMaxLength(100);
            builder.Property(a => a.UserAgent).HasMaxLength(500);
            builder.Property(a => a.RecordHash).IsRequired().HasMaxLength(128);
            builder.Property(a => a.PreviousRecordHash).HasMaxLength(128);
            builder.Property(a => a.IsArchived).IsRequired();
            builder.Property(a => a.TimestampUtc).IsRequired();

            builder.HasIndex(a => a.TimestampUtc);
            builder.HasIndex(a => a.AdminUserId);
            builder.HasIndex(a => a.TargetTenantId);
            builder.HasIndex(a => a.Action);
            builder.HasIndex(a => a.Category);
            builder.HasIndex(a => a.Severity);
            builder.HasIndex(a => a.RecordHash);
            builder.HasIndex(a => new { a.TimestampUtc, a.Severity });
        });

        modelBuilder.Entity<AdminSavedFilter>(builder =>
        {
            builder.ToTable("AdminSavedFilters");
            builder.HasKey(f => f.Id);
            builder.Property(f => f.Name).IsRequired().HasMaxLength(100);
            builder.Property(f => f.FilterJson).IsRequired();
            builder.Property(f => f.IsDefault).IsRequired();
            builder.Property(f => f.CreatedAtUtc).IsRequired();
            builder.Property(f => f.UpdatedAtUtc).IsRequired();
            builder.HasIndex(f => new { f.AdminUserId, f.Name }).IsUnique();
            builder.HasIndex(f => f.AdminUserId);
        });

        modelBuilder.Entity<PlanCatalog>(builder =>
        {
            builder.ToTable("PlanCatalogs");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Code).IsRequired().HasMaxLength(50);
            builder.Property(p => p.Name).IsRequired().HasMaxLength(150);
            builder.Property(p => p.Description).IsRequired().HasMaxLength(500);
            builder.Property(p => p.Category).IsRequired().HasMaxLength(50);
            builder.HasIndex(p => p.Code).IsUnique();
            builder.HasMany(p => p.Versions).WithOne().HasForeignKey(v => v.PlanCatalogId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlanVersion>(builder =>
        {
            builder.ToTable("PlanVersions");
            builder.HasKey(v => v.Id);
            builder.Property(v => v.VersionName).IsRequired().HasMaxLength(150);
            builder.Property(v => v.Status).HasConversion<string>().IsRequired().HasMaxLength(30);
            builder.Property(v => v.MonthlyPrice).HasPrecision(18, 2);
            builder.Property(v => v.AnnualPriceMonthly).HasPrecision(18, 2);
            builder.HasMany(v => v.Features).WithOne().HasForeignKey(f => f.PlanVersionId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(v => v.Limits).WithOne().HasForeignKey(l => l.PlanVersionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlanFeature>(builder =>
        {
            builder.ToTable("PlanFeatures");
            builder.HasKey(f => f.Id);
            builder.Property(f => f.FeatureKey).IsRequired().HasMaxLength(100);
            builder.Property(f => f.DisplayName).IsRequired().HasMaxLength(200);
            builder.Property(f => f.FeatureType).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<PlanLimit>(builder =>
        {
            builder.ToTable("PlanLimits");
            builder.HasKey(l => l.Id);
            builder.Property(l => l.LimitKey).IsRequired().HasMaxLength(100);
            builder.Property(l => l.LimitValue).HasPrecision(18, 2);
            builder.Property(l => l.Unit).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<ImpersonationSession>(builder =>
        {
            builder.ToTable("ImpersonationSessions");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.AdminUserEmail).IsRequired().HasMaxLength(200);
            builder.Property(s => s.TargetTenantName).IsRequired().HasMaxLength(200);
            builder.Property(s => s.TargetUserEmail).HasMaxLength(200);
            builder.Property(s => s.SupportTicket).IsRequired().HasMaxLength(50);
            builder.Property(s => s.Justification).IsRequired().HasMaxLength(1000);
            builder.Property(s => s.Status).HasConversion<string>().IsRequired().HasMaxLength(30);
            builder.Property(s => s.IpAddress).IsRequired().HasMaxLength(100);
            builder.Property(s => s.UserAgent).IsRequired().HasMaxLength(500);
            builder.Property(s => s.RevocationReason).HasMaxLength(500);
            builder.HasIndex(s => s.AdminUserId);
            builder.HasIndex(s => s.TargetTenantId);
            builder.HasIndex(s => s.Status);
            builder.HasIndex(s => s.ExpiresAtUtc);
        });

        modelBuilder.Entity<AdminApprovalRequest>(builder =>
        {
            builder.ToTable("AdminApprovalRequests");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.RequestType).HasConversion<string>().IsRequired().HasMaxLength(50);
            builder.Property(r => r.Status).HasConversion<string>().IsRequired().HasMaxLength(30);
            builder.Property(r => r.Title).IsRequired().HasMaxLength(250);
            builder.Property(r => r.Justification).IsRequired().HasMaxLength(1500);
            builder.Property(r => r.SupportTicketId).HasMaxLength(50);
            builder.Property(r => r.TargetResourceId).IsRequired().HasMaxLength(200);
            builder.Property(r => r.ImpactSummary).IsRequired().HasMaxLength(2000);
            builder.Property(r => r.PayloadJson).IsRequired();
            builder.Property(r => r.DiffJson);
            builder.Property(r => r.RequestedByAdminEmail).IsRequired().HasMaxLength(200);
            builder.Property(r => r.ReviewedByAdminEmail).HasMaxLength(200);
            builder.Property(r => r.ReviewNotes).HasMaxLength(1000);
            builder.Property(r => r.RejectionReason).HasMaxLength(1000);
            builder.Property(r => r.ExecutionResultJson);
            builder.Property(r => r.ExecutionError).HasMaxLength(2000);
            builder.HasIndex(r => r.Status);
            builder.HasIndex(r => r.RequestType);
            builder.HasIndex(r => r.RequestedByAdminUserId);
            builder.HasIndex(r => r.ExpiresAtUtc);
        });
    }
}
