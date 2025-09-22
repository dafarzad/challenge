using Lottery.Application.Interfaces;
using Lottery.Domain.Models;
using Lottery.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Lottery.Infra.Repositories;

public class CampaignRepository : ICampaignRepository
{
    private readonly ApplicationDbContext _db;
    public CampaignRepository(ApplicationDbContext db) => _db = db;

    public async Task<Campaign?> GetActiveCampaignAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _db.Campaigns.FirstOrDefaultAsync(c => c.StartUtc <= now && c.EndUtc >= now, ct);
    }

    public async Task<Campaign?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.Campaigns.FindAsync(id, ct) as Campaign;
    }
}
