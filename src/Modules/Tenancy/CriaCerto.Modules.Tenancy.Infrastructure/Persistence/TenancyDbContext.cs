using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Infrastructure.Persistence;

public sealed class TenancyDbContext : DbContext, ITenancyDbContext
{
    public TenancyDbContext(DbContextOptions<TenancyDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<UserTenant> UserTenants => Set<UserTenant>();
    public DbSet<ProductionUnit> ProductionUnits => Set<ProductionUnit>();
    public DbSet<TeamInvite> TeamInvites => Set<TeamInvite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("tenancy");

        modelBuilder.Entity<User>(builder =>
        {
            builder.ToTable("Users");
            builder.HasKey(u => u.Id);
            builder.HasIndex(u => u.Email).IsUnique();
            builder.Property(u => u.Email).HasMaxLength(150).IsRequired();
            builder.Property(u => u.FullName).HasMaxLength(150).IsRequired();
            builder.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
            builder.Property(u => u.PhoneNumber).HasMaxLength(30);
            builder.Property(u => u.PasswordResetToken).HasMaxLength(100);
            builder.Property(u => u.PasswordResetTokenExpiresAt);
        });

        modelBuilder.Entity<Tenant>(builder =>
        {
            builder.ToTable("Tenants");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Name).HasMaxLength(150).IsRequired();
            builder.Property(t => t.LegalName).HasMaxLength(200);
            builder.Property(t => t.CNPJ).HasMaxLength(20).IsRequired();
            builder.Property(t => t.CnpjNormalized).HasMaxLength(14).IsRequired();
            builder.HasIndex(t => t.CnpjNormalized).IsUnique();
            builder.Property(t => t.ExternalIdentifier).HasMaxLength(80);
            builder.HasIndex(t => t.ExternalIdentifier)
                .IsUnique()
                .HasFilter("[ExternalIdentifier] IS NOT NULL");
            builder.Property(t => t.Status).HasMaxLength(50).IsRequired();
            builder.Property(t => t.SubscribedPlan).HasMaxLength(50).IsRequired();
            builder.Property(t => t.State).HasMaxLength(50);
            builder.Property(t => t.City).HasMaxLength(100);
            builder.Property(t => t.StateRegistration).HasMaxLength(50);
            builder.Property(t => t.AreaInHectares).HasPrecision(18, 2);
            builder.Property(t => t.Type).HasMaxLength(100);
            builder.Property(t => t.TechnicalOwnerName).HasMaxLength(150);
            builder.Property(t => t.TechnicalOwnerEmail).HasMaxLength(150);
            builder.Property(t => t.CommercialOwnerName).HasMaxLength(150);
            builder.Property(t => t.CommercialOwnerEmail).HasMaxLength(150);
            builder.Property(t => t.IsProtected).IsRequired().HasDefaultValue(false);
            builder.Property(t => t.StatusReason).HasMaxLength(500);
            builder.Property(t => t.StatusChangedAtUtc);
            builder.Property(t => t.CreatedAtUtc).IsRequired();
            builder.Property(t => t.UpdatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<UserTenant>(builder =>
        {
            builder.ToTable("UserTenants");
            builder.HasKey(ut => new { ut.UserId, ut.TenantId });
            builder.Property(ut => ut.Role).HasConversion<int>().IsRequired();
            builder.Property(ut => ut.JoinedAt).IsRequired();

            builder.HasOne(ut => ut.User)
                .WithMany(u => u.UserTenants)
                .HasForeignKey(ut => ut.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(ut => ut.Tenant)
                .WithMany(t => t.UserTenants)
                .HasForeignKey(ut => ut.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductionUnit>(builder =>
        {
            builder.ToTable("ProductionUnits");
            builder.HasKey(pu => pu.Id);
            builder.Property(pu => pu.Code).HasMaxLength(50).IsRequired();
            builder.Property(pu => pu.Name).HasMaxLength(100).IsRequired();
            builder.Property(pu => pu.Type).HasMaxLength(50).IsRequired();
            builder.Property(pu => pu.Status).HasMaxLength(50).IsRequired();
            builder.Property(pu => pu.LocationDetails).HasMaxLength(250);

            builder.HasOne(pu => pu.Tenant)
                .WithMany(t => t.ProductionUnits)
                .HasForeignKey(pu => pu.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeamInvite>(builder =>
        {
            builder.ToTable("TeamInvites");
            builder.HasKey(ti => ti.Id);
            builder.Property(ti => ti.Email).HasMaxLength(150).IsRequired();
            builder.Property(ti => ti.Role).HasConversion<int>().IsRequired();
            builder.Property(ti => ti.InviteToken).HasMaxLength(100).IsRequired();
            builder.Property(ti => ti.CreatedAt).IsRequired();
            builder.Property(ti => ti.ExpiresAt).IsRequired();
            builder.Property(ti => ti.IsAccepted).IsRequired();

            builder.HasOne(ti => ti.Tenant)
                .WithMany()
                .HasForeignKey(ti => ti.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

