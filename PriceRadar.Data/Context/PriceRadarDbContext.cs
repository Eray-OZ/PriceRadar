using PriceRadar.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace PriceRadar.Data.Context;

public class PriceRadarDbContext : DbContext
{
    public PriceRadarDbContext(DbContextOptions<PriceRadarDbContext> options) : base(options) {}

    public DbSet<PriceHistory> PriceHistories { get; set; }
    public DbSet<TrackedProduct> TrackedProducts { get; set; }
}
