using Microsoft.EntityFrameworkCore;
using ShopFinder.Api.Models;

namespace ShopFinder.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Shop>(e =>
        {
            e.HasIndex(s => s.Phone).IsUnique();
            e.Property(s => s.Name).HasMaxLength(120).IsRequired();
            e.Property(s => s.Category).HasMaxLength(60);
            e.Property(s => s.Description).HasMaxLength(1000);
            e.Property(s => s.Address).HasMaxLength(300);
            e.Property(s => s.Phone).HasMaxLength(30).IsRequired();
            e.Property(s => s.OpenDays).HasMaxLength(20);
            e.Property(s => s.StatusOverride).HasMaxLength(10);
            e.HasMany(s => s.Products)
             .WithOne(p => p.Shop)
             .HasForeignKey(p => p.ShopId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Product>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(120).IsRequired();
            e.Property(p => p.Description).HasMaxLength(1000);
            // SQLite stores decimal as TEXT (exact). On SQL Server, add .HasPrecision(10, 2).
        });
    }
}
