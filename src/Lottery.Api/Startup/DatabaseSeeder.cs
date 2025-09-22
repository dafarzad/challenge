using Lottery.Infra.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace Lottery.Api.Startup;

public static class DatabaseSeeder
{
    /// <summary>
    /// Ensures the Campaigns table is cleared and reseeded with a default campaign.
    /// Intended for development/testing only.
    /// </summary>
    public static void EnsureCampaignSeed(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        try
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // delete existing campaigns
            ctx.Database.ExecuteSqlRaw("DELETE FROM \"Campaigns\"");
            var now = DateTime.UtcNow;
            ctx.Campaigns.Add(new Lottery.Domain.Models.Campaign
            {
                Id = 1,
                Name = "Default",
                StartUtc = now.AddMinutes(1),
                EndUtc = now.AddMinutes(6),
                SuccessTarget = 1000
            });
            ctx.SaveChanges();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("Startup");
            logger?.LogError(ex, "Failed to reseed campaigns on startup");
        }
    }
}
