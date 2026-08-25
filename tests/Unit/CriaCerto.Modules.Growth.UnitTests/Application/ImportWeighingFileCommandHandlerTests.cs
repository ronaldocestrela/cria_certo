using System.Text;
using CriaCerto.Modules.Growth.Application.Abstractions;
using CriaCerto.Modules.Growth.Application.Contracts;
using CriaCerto.Modules.Growth.Application.Domain;
using CriaCerto.Modules.Growth.Application.Services.ScaleParsers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Growth.UnitTests.Application;

public class ImportWeighingFileCommandHandlerTests
{
    private static IGrowthDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<MockGrowthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MockGrowthDbContext(options);
    }

    private class MockGrowthDbContext : DbContext, IGrowthDbContext
    {
        public MockGrowthDbContext(DbContextOptions<MockGrowthDbContext> options) : base(options) { }

        public DbSet<PasturePaddock> Paddocks => Set<PasturePaddock>();
        public DbSet<Lot> Lots => Set<Lot>();
        public DbSet<LotMovement> LotMovements => Set<LotMovement>();
        public DbSet<Weighing> Weighings => Set<Weighing>();
    }

    [Fact]
    public async Task ImportWeighingFileCommandHandler_ValidTruTestCsv_ShouldImportRecordsAndCalculateGpd()
    {
        // Arrange
        var dbContext = CreateInMemoryDbContext();
        var tenantId = Guid.NewGuid();

        // Seed previous weighing for BR-101
        var prevDate = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc);
        var prevWeighing = Weighing.Create(tenantId, "BR-101", null, prevDate, 400.0m, 50.0m).Value;
        dbContext.Weighings.Add(prevWeighing);
        await dbContext.SaveChangesAsync();

        var factory = new ScaleFileParserFactory(new IWeighingScaleFileParser[]
        {
            new TruTestScaleParser(),
            new CoimmaScaleParser(),
            new ToledoScaleParser(),
            new GenericCsvScaleParser()
        });

        var handler = new ImportWeighingFileCommandHandler(dbContext, factory);

        var csvContent = "VID,Weight,Date\nBR-101,430.0,2026-07-20\nBR-102,350.0,2026-07-20";
        var fileBytes = Encoding.UTF8.GetBytes(csvContent);

        var command = new ImportWeighingFileCommand(
            fileBytes,
            "trutest_test.csv",
            ScaleModelEnum.TruTest,
            tenantId,
            null,
            DateTime.UtcNow,
            50.0m);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalRowsProcessed.Should().Be(2);
        result.Value.SuccessCount.Should().Be(2);
        result.Value.ErrorCount.Should().Be(0);

        var br101Weighing = await dbContext.Weighings.FirstOrDefaultAsync(w => w.TenantId == tenantId && w.AnimalTagId == "BR-101" && w.WeightKg == 430.0m);
        br101Weighing.Should().NotBeNull();
        br101Weighing!.CalculatedAdgKgPerDay.Should().BeGreaterThan(0.0m);
    }
}
