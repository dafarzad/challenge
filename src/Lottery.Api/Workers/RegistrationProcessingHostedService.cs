using Lottery.Domain.Models;
using Lottery.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace Lottery.Api.Workers;

public class RegistrationProcessingHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConnectionMultiplexer _redis;
    private readonly ILogger<RegistrationProcessingHostedService> _logger;
    private readonly RegistrationProcessingOptions _options;

    public RegistrationProcessingHostedService(
        IServiceScopeFactory scopeFactory,
        ConnectionMultiplexer redis,
        ILogger<RegistrationProcessingHostedService> logger,
        IOptions<RegistrationProcessingOptions> options)
    {
        _scopeFactory = scopeFactory;
        _redis = redis;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Registration processor started with CampaignId {CampaignId}, BatchSize {BatchSize}, MaxDegreeOfParallelism {MaxDegreeOfParallelism}",
            _options.CampaignId, _options.BatchSize, _options.MaxDegreeOfParallelism);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Requeue stuck items before each batch
                await RecoverStuckRegistrationsAsync(stoppingToken);

                await ProcessBatchAsync(stoppingToken);

                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in processor loop. Retrying in 5 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Registration processor stopped.");
    }

    private async Task ProcessBatchAsync(CancellationToken stoppingToken)
    {
        List<Registration> claimedRegistrations;

        // --- STEP 1: Claim registrations atomically ---
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await using var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);

            claimedRegistrations = await dbContext.LotteryRequests
                .Where(r => r.Status == LotteryStatus.Pending && r.CampaignId == _options.CampaignId)
                .OrderBy(r => r.CreatedAt)
                .Take(_options.BatchSize)
                .ToListAsync(stoppingToken);

            if (claimedRegistrations.Count == 0)
            {
                return;
            }

            foreach (var reg in claimedRegistrations)
            {
                reg.Status = LotteryStatus.Processing;
                reg.ProcessedAt = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(stoppingToken);
            await transaction.CommitAsync(stoppingToken);
        }

        _logger.LogInformation("Claimed {Count} registrations to process.", claimedRegistrations.Count);

        var redisDb = _redis.GetDatabase();
        using var semaphore = new SemaphoreSlim(_options.MaxDegreeOfParallelism, _options.MaxDegreeOfParallelism);

        var tasks = claimedRegistrations.Select(async registration =>
        {
            await semaphore.WaitAsync(stoppingToken);
            try
            {
                await ProcessRegistrationAsync(registration.RequestId, redisDb, stoppingToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task ProcessRegistrationAsync(Guid requestId, IDatabase redisDb, CancellationToken stoppingToken)
    {
        try
        {
            // Simulate processing (e.g., API call)
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            var newStatus = await DetermineWinnerStatusAsync(redisDb);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var reg = await dbContext.LotteryRequests.FindAsync(new object[] { requestId }, stoppingToken);
            if (reg is null)
            {
                _logger.LogWarning("Registration {RequestId} not found during processing.", requestId);
                return;
            }

            reg.Status = newStatus;
            reg.ProcessedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(stoppingToken);

            await UpdateRedisCacheAsync(redisDb, reg);

            _logger.LogInformation("Processed registration {RequestId} with status {Status}.", reg.RequestId, reg.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing registration {RequestId}.", requestId);

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var reg = await dbContext.LotteryRequests.FindAsync(new object[] { requestId }, stoppingToken);
                if (reg != null && reg.Status != LotteryStatus.Failed)
                {
                    reg.Status = LotteryStatus.Failed;
                    reg.ProcessedAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogWarning(innerEx, "Failed to mark registration {RequestId} as failed.", requestId);
            }
        }
    }

    private async Task<LotteryStatus> DetermineWinnerStatusAsync(IDatabase redisDb)
    {
        // Simple 50/50 chance for demo purposes
        bool isPotentialWinner = Random.Shared.Next(0, 2) == 0;

        if (!isPotentialWinner)
            return LotteryStatus.Failed;

        long newWinnerCount = await redisDb.StringIncrementAsync(_options.RedisSuccessCountKey);

        if (newWinnerCount <= _options.WinnerLimit)
        {
            return LotteryStatus.Success;
        }
        else
        {
            await redisDb.StringDecrementAsync(_options.RedisSuccessCountKey);
            return LotteryStatus.Failed;
        }
    }

    private async Task UpdateRedisCacheAsync(IDatabase redisDb, Registration registration)
    {
        var hashKey = $"lottery:status:{registration.RequestId}";
        var hashEntries = new[]
        {
            new HashEntry("status", registration.Status.ToString()),
            new HashEntry("processedAt", registration.ProcessedAt?.ToString("o") ?? string.Empty)
        };
        await redisDb.HashSetAsync(hashKey, hashEntries);
        await redisDb.KeyExpireAsync(hashKey, TimeSpan.FromDays(30));
    }

    private async Task RecoverStuckRegistrationsAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var cutoff = DateTime.UtcNow - TimeSpan.FromHours(1);

        var stuck = await dbContext.LotteryRequests
            .Where(r => r.Status == LotteryStatus.Processing && r.ProcessedAt < cutoff && r.CampaignId == _options.CampaignId)
            .ToListAsync(stoppingToken);

        if (stuck.Count == 0) return;

        foreach (var reg in stuck)
        {
            reg.Status = LotteryStatus.Pending;
            reg.ProcessedAt = null;
        }

        await dbContext.SaveChangesAsync(stoppingToken);

        _logger.LogWarning("Recovered {Count} stuck registrations back to Pending.", stuck.Count);
    }
}
