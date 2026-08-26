using PriceRadar.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace PriceRadar.Data.Context;

public class PriceRadarDbContext : DbContext
{
    public PriceRadarDbContext(DbContextOptions<PriceRadarDbContext> options) : base(options) {}

    public DbSet<PriceHistory> PriceHistories { get; set; }
    public DbSet<TrackedProduct> TrackedProducts { get; set; }
    public DbSet<ApplicationUser> ApplicationUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(user => user.UserName)
            .IsUnique();

        modelBuilder.Entity<ApplicationUser>()
            .HasMany(user => user.TrackedProducts)
            .WithOne(product => product.User)
            .HasForeignKey(product => product.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
