using CriaCerto.Modules.Breeding.Application.Abstractions;
using CriaCerto.Modules.Breeding.Application.Domain;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Breeding.Infrastructure.Persistence;

public sealed class BreedingDbContext : DbContext, IBreedingDbContext
{
    public BreedingDbContext(DbContextOptions<BreedingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Cow> Cows => Set<Cow>();
    public DbSet<Bull> Bulls => Set<Bull>();
    public DbSet<SemenBatch> SemenBatches => Set<SemenBatch>();
    public DbSet<IatfProtocol> IatfProtocols => Set<IatfProtocol>();
    public DbSet<PregnancyDiagnosis> PregnancyDiagnoses => Set<PregnancyDiagnosis>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("breeding");

        modelBuilder.Entity<Cow>(builder =>
        {
            builder.ToTable("Cows");
            builder.HasKey(c => c.Id);
            builder.HasIndex(c => c.EarTag);
            builder.Property(c => c.EarTag).HasMaxLength(50).IsRequired();
            builder.Property(c => c.SisbovId).HasMaxLength(50);
            builder.Property(c => c.RfidTag).HasMaxLength(50);
            builder.Property(c => c.Tattoo).HasMaxLength(50);
            builder.Property(c => c.Breed).HasMaxLength(100).IsRequired();
            builder.Property(c => c.BirthDate).IsRequired(false);
            builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(40);
        });

        modelBuilder.Entity<Bull>(builder =>
        {
            builder.ToTable("Bulls");
            builder.HasKey(b => b.Id);
            builder.HasIndex(b => b.EarTag);
            builder.Property(b => b.EarTag).HasMaxLength(50).IsRequired();
            builder.Property(b => b.Name).HasMaxLength(100).IsRequired();
            builder.Property(b => b.Breed).HasMaxLength(100).IsRequired();
            builder.Property(b => b.BirthDate).IsRequired(false);
            builder.Property(b => b.RegistryNumber).HasMaxLength(50);
        });

        modelBuilder.Entity<SemenBatch>(builder =>
        {
            builder.ToTable("SemenBatches");
            builder.HasKey(s => s.Id);
            builder.HasIndex(s => s.BatchCode);
            builder.Property(s => s.BatchCode).HasMaxLength(50).IsRequired();
            builder.Property(s => s.BullName).HasMaxLength(100).IsRequired();
            builder.Property(s => s.Breed).HasMaxLength(100).IsRequired();
            builder.Property(s => s.Type).HasConversion<string>().HasMaxLength(40);
        });

        modelBuilder.Entity<IatfProtocol>(builder =>
        {
            builder.ToTable("IatfProtocols");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
            builder.Property(p => p.BullId).IsRequired(false);
            builder.Property(p => p.BullName).HasMaxLength(150).IsRequired(false);
        });

        modelBuilder.Entity<PregnancyDiagnosis>(builder =>
        {
            builder.ToTable("PregnancyDiagnoses");
            builder.HasKey(d => d.Id);
            builder.HasIndex(d => d.CowId);
            builder.Property(d => d.Method).HasConversion<string>().HasMaxLength(40);
            builder.Property(d => d.Notes).HasMaxLength(500);
        });
    }
}
