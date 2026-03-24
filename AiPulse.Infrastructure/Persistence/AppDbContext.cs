using AiPulse.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AiPulse.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ContentItem> ContentItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContentItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Source).HasConversion<string>();
            entity.Property(e => e.ContentType).HasConversion<string>();
        });
    }
}
