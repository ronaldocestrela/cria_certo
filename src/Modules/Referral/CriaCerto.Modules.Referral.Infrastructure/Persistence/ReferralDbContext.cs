using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Infrastructure.Persistence;

public class ReferralDbContext : DbContext, IReferralDbContext
{
    public DbSet<ReferralProgramParticipant> Participants => Set<ReferralProgramParticipant>();
    public DbSet<ReferralCode> ReferralCodes => Set<ReferralCode>();
    public DbSet<PartnerContract> PartnerContracts => Set<PartnerContract>();
    public DbSet<PlanReferralDiscountPolicy> PlanDiscountPolicies => Set<PlanReferralDiscountPolicy>();
    public DbSet<ReferralAttribution> Attributions => Set<ReferralAttribution>();
    public DbSet<ReferralRewardLedger> RewardLedger => Set<ReferralRewardLedger>();

    public ReferralDbContext(DbContextOptions<ReferralDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("referral");

        modelBuilder.Entity<ReferralProgramParticipant>(builder =>
        {
            builder.ToTable("Participants");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.TenantId);
            builder.Property(p => p.UserId);

            builder.Property(p => p.ParticipantType)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(p => p.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(p => p.SuspensionReason)
                .HasMaxLength(500);

            builder.Property(p => p.CreatedByAdminId);
            builder.Property(p => p.CreatedAtUtc).IsRequired();
            builder.Property(p => p.UpdatedAtUtc);

            builder.HasIndex(p => p.TenantId);
            builder.HasIndex(p => p.UserId);
            builder.HasIndex(p => p.Status);
            builder.HasIndex(p => p.ParticipantType);
            builder.HasIndex(p => p.CreatedAtUtc);
        });

        modelBuilder.Entity<ReferralCode>(builder =>
        {
            builder.ToTable("ReferralCodes");
            builder.HasKey(c => c.Id);

            builder.Property(c => c.ParticipantId).IsRequired();

            builder.Property(c => c.Code)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(c => c.CampaignTitle)
                .HasMaxLength(150);

            builder.Property(c => c.ClicksCount)
                .IsRequired()
                .HasDefaultValue(0L);

            builder.Property(c => c.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(c => c.CreatedAtUtc).IsRequired();

            builder.HasIndex(c => c.Code).IsUnique();
            builder.HasIndex(c => c.ParticipantId);
            builder.HasIndex(c => c.IsActive);

            builder.HasOne<ReferralProgramParticipant>()
                .WithMany()
                .HasForeignKey(c => c.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PartnerContract>(builder =>
        {
            builder.ToTable("PartnerContracts");
            builder.HasKey(c => c.Id);

            builder.Property(c => c.ParticipantId).IsRequired();

            builder.Property(c => c.CommissionType)
                .HasConversion<string>()
                .HasMaxLength(40)
                .IsRequired();

            builder.Property(c => c.CommissionValue)
                .HasPrecision(18, 4)
                .IsRequired();

            builder.Property(c => c.MaxRecurringMonths);

            builder.Property(c => c.PaymentTermsDays)
                .IsRequired()
                .HasDefaultValue(30);

            builder.Property(c => c.PixKey)
                .HasMaxLength(150)
                .IsRequired();

            builder.Property(c => c.PixKeyType)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(c => c.LegalDocumentNumber)
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(c => c.ValidFromUtc).IsRequired();
            builder.Property(c => c.ValidToUtc);
            builder.Property(c => c.IsActive).IsRequired();
            builder.Property(c => c.CreatedAtUtc).IsRequired();

            builder.HasIndex(c => c.ParticipantId);
            builder.HasIndex(c => c.IsActive);
            builder.HasIndex(c => new { c.ValidFromUtc, c.ValidToUtc });

            builder.HasOne<ReferralProgramParticipant>()
                .WithMany()
                .HasForeignKey(c => c.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlanReferralDiscountPolicy>(builder =>
        {
            builder.ToTable("PlanDiscountPolicies");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.PlanCatalogId).IsRequired();

            builder.Property(p => p.PlanCode)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(p => p.DiscountType)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(p => p.DiscountValue)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(p => p.MaxDiscountCapPercentage)
                .HasPrecision(5, 2)
                .IsRequired()
                .HasDefaultValue(100.00m);

            builder.Property(p => p.IsActive).IsRequired();
            builder.Property(p => p.CreatedAtUtc).IsRequired();

            builder.HasIndex(p => p.PlanCatalogId);
            builder.HasIndex(p => p.PlanCode);
            builder.HasIndex(p => p.IsActive);
        });

        modelBuilder.Entity<ReferralAttribution>(builder =>
        {
            builder.ToTable("Attributions");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.ReferralCodeId).IsRequired();
            builder.Property(a => a.ReferrerParticipantId).IsRequired();
            builder.Property(a => a.ReferredTenantId).IsRequired();
            builder.Property(a => a.AttributionDateUtc).IsRequired();
            builder.Property(a => a.ConvertedAtUtc);

            builder.Property(a => a.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(a => a.OriginIpAddress)
                .HasMaxLength(45);

            builder.Property(a => a.UserAgent)
                .HasMaxLength(500);

            builder.Property(a => a.DeviceFingerprintHash)
                .HasMaxLength(128);

            builder.Property(a => a.FraudReason)
                .HasMaxLength(500);

            builder.HasIndex(a => a.ReferredTenantId);
            builder.HasIndex(a => a.ReferralCodeId);
            builder.HasIndex(a => a.ReferrerParticipantId);
            builder.HasIndex(a => a.Status);
            builder.HasIndex(a => a.AttributionDateUtc);
            builder.HasIndex(a => new { a.ReferredTenantId, a.Status });

            builder.HasOne<ReferralCode>()
                .WithMany()
                .HasForeignKey(a => a.ReferralCodeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<ReferralProgramParticipant>()
                .WithMany()
                .HasForeignKey(a => a.ReferrerParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReferralRewardLedger>(builder =>
        {
            builder.ToTable("RewardLedger");
            builder.HasKey(l => l.Id);

            builder.Property(l => l.ReferralAttributionId).IsRequired();
            builder.Property(l => l.ParticipantId).IsRequired();

            builder.Property(l => l.RewardType)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(l => l.GrossAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(l => l.NetAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(l => l.Currency)
                .HasMaxLength(3)
                .IsRequired();

            builder.Property(l => l.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(l => l.MaturityDateUtc);

            builder.Property(l => l.StripeInvoiceId)
                .HasMaxLength(100);

            builder.Property(l => l.StripeCreditBalanceTransactionId)
                .HasMaxLength(100);

            builder.Property(l => l.PayoutTransactionReference)
                .HasMaxLength(150);

            builder.Property(l => l.CancellationReason)
                .HasMaxLength(500);

            builder.Property(l => l.CreatedAtUtc).IsRequired();
            builder.Property(l => l.UpdatedAtUtc);

            builder.HasIndex(l => l.ParticipantId);
            builder.HasIndex(l => l.ReferralAttributionId);
            builder.HasIndex(l => l.Status);
            builder.HasIndex(l => l.MaturityDateUtc);
            builder.HasIndex(l => l.StripeInvoiceId);
            builder.HasIndex(l => new { l.Status, l.MaturityDateUtc });

            builder.HasOne<ReferralAttribution>()
                .WithMany()
                .HasForeignKey(l => l.ReferralAttributionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<ReferralProgramParticipant>()
                .WithMany()
                .HasForeignKey(l => l.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
