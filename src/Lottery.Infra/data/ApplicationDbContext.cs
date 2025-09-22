using Lottery.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Lottery.Infra.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Registration> LotteryRequests { get; set; } = null!;
    public DbSet<Campaign> Campaigns { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // seed a default campaign running for the next 5 minutes
        var now = DateTime.UtcNow;
        modelBuilder.Entity<Campaign>().HasData(new Campaign
        {
            Id = 1,
            Name = "Default",
            StartUtc = now.AddMinutes(1),
            EndUtc = now.AddMinutes(6),
            SuccessTarget = 1000
        });
    }
}
