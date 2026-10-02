using Microsoft.EntityFrameworkCore;
using LaundrySite.Models;

namespace LaundrySite.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

    public DbSet<User> Users => Set<User>();
    public DbSet<WashSlot> WashSlots => Set<WashSlot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(x => x.PublicId)
            .IsUnique();
    }
}
