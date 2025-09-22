using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lottery.Domain.Models;

public enum LotteryStatus { Pending, Processing, Success, Failed }

public class Registration
{
    [Key]
    public Guid RequestId { get; set; }
    [MaxLength(100)]
    public string FirstName { get; set; } = null!;
    [MaxLength(100)]
    public string LastName { get; set; } = null!;
    [MaxLength(20)]
    public string Phone { get; set; } = null!;
    [MaxLength(20)]
    public string NationalId { get; set; } = null!;
    public int CampaignId { get; set; }

    [ForeignKey(nameof(CampaignId))]
    public Campaign? Campaign { get; set; }
    public LotteryStatus Status { get; set; } = LotteryStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}
