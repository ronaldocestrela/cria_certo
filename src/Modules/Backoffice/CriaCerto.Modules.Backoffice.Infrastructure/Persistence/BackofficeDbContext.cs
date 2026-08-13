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
            builder.Property(a => a.Action).IsRequired().HasMaxLength(100);
            builder.Property(a => a.Resource).IsRequired().HasMaxLength(200);
        });
    }
}
