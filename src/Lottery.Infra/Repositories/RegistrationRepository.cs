using Lottery.Application.Interfaces;
using Lottery.Domain.Models;
using Lottery.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Lottery.Infra.Repositories;

public class RegisterRepository : IRegistrationRepository
{
    private readonly ApplicationDbContext _db;
    public RegisterRepository(ApplicationDbContext db) => _db = db;

    public async Task AddAsync(Registration req, CancellationToken ct = default)
    {
        _db.LotteryRequests.Add(req);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Registration?> GetAsync(Guid requestId, CancellationToken ct = default)
    {
        return await _db.LotteryRequests.FirstOrDefaultAsync(r => r.RequestId == requestId, ct);
    }

    public async Task UpdateAsync(Registration req, CancellationToken ct = default)
    {
        _db.LotteryRequests.Update(req);
        await _db.SaveChangesAsync(ct);
    }
}

