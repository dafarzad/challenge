namespace Lottery.Application.Interfaces;

using Lottery.Domain.Models;

public interface IRegistrationRepository
{
    Task AddAsync(Registration req, CancellationToken ct = default);
    Task<Registration?> GetAsync(Guid requestId, CancellationToken ct = default);
    Task UpdateAsync(Registration req, CancellationToken ct = default);
}