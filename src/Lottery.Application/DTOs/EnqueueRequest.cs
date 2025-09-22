using Lottery.Domain.Models;

namespace Lottery.Application.DTOs;

public record EnqueueRequest(
    Guid RequestId,
    string FirstName,
    string LastName,
    string Phone,
    string NationalId,
    int CampaignId,
    LotteryStatus Status,
    DateTime CreatedAt
);
