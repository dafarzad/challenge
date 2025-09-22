using System;

namespace Lottery.Domain.Models;

public class Campaign
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public int SuccessTarget { get; set; } = 1000;
}
