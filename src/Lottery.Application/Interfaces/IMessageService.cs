namespace Lottery.Application.Interfaces;

using Lottery.Application.DTOs;

public interface IMessagingService
{
    Task EnqueueAsync(EnqueueRequest req);
    // Return status as simple key/value pairs (status, processedAt, score)
    Task<KeyValuePair<string,string>[]> GetStatusAsync(Guid requestId);
}
