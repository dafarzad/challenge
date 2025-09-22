namespace Lottery.Api.Workers;

public class RegistrationProcessingOptions
{
    public const string SectionName = "LotteryRegistrationProcessing";

    public int CampaignId { get; set; } = 1;
    public int BatchSize { get; set; } = 20;
    public int PollIntervalSeconds { get; set; } = 2;
    public int WinnerLimit { get; set; } = 1000;
    public int MaxDegreeOfParallelism { get; set; } = 4;
    public string RedisSuccessCountKey { get; set; } = "lottery:success_count";
}