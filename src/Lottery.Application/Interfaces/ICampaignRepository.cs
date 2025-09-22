using Lottery.Domain.Models;

namespace Lottery.Application.Interfaces;

public interface ICampaignRepository
{
    /// <summary>
    /// Returns the currently active campaign (if any) based on UTC now.
    /// </summary>
    Task<Campaign?> GetActiveCampaignAsync(CancellationToken ct = default);
    Task<Campaign?> GetByIdAsync(int id, CancellationToken ct = default);
}
