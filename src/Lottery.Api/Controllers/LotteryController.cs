using System.Text.Json;
using Lottery.Application.DTOs;
using Lottery.Application.Interfaces;
using Lottery.Domain.Models;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/lottery")]
public class LotteryController : ControllerBase
{
    private readonly IRegistrationRepository _regRepo;
    private readonly ICampaignRepository _campaignRepo;
    private readonly IMessagingService _queue;
    private readonly IRedisService _cache;

    public LotteryController(IRegistrationRepository repo, ICampaignRepository campaignRepo, IMessagingService queue, IRedisService cache)
    {
       _regRepo = repo; _campaignRepo = campaignRepo; _queue = queue; _cache = cache;
    }

[HttpPost("register")]
public async Task<IActionResult> Register(RegisterRequestDto dto)
{
    var campaignId = dto.CampaignId;
    var cacheKey = $"campaign:{campaignId}";
    Campaign? campaign = null;

    // Try from cache
    var cached = await _cache.GetStringAsync(cacheKey);
    if (!string.IsNullOrEmpty(cached))
    {
        try
        {
            campaign = JsonSerializer.Deserialize<Campaign>(cached);
        }
        catch
        {
            campaign = null;
        }
    }

    // Fallback: load from DB if not in cache
    if (campaign == null)
    {
        campaign = await _campaignRepo.GetByIdAsync(campaignId);
        if (campaign == null)
            return BadRequest(new { error = "lottery not found" });

       var ttl = TimeSpan.FromDays(1);
       
        try
            {
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(campaign), ttl);
            }
            catch { /* ignore cache failure */ }
    }

    // ---- Validate campaign time window ----
    var now = DateTime.UtcNow;
    if (now < campaign.StartUtc)
    {
        return BadRequest(new { error = "lottery has not started yet", startsAt = campaign.StartUtc });
    }

    if (now > campaign.EndUtc)
    {
        return BadRequest(new { error = "lottery has already ended", endedAt = campaign.EndUtc });
    }

    // ---- If valid, enqueue registration ----
    var requestId = Guid.NewGuid();

    var enqueueReq = new EnqueueRequest(
        requestId,
        dto.FirstName,
        dto.LastName,
        dto.Phone,
        dto.NationalCode,
        campaignId,
        LotteryStatus.Pending,
        DateTime.UtcNow
    );

    await _queue.EnqueueAsync(enqueueReq);

    // Best-effort status caching
    try
    {
        var statusKey = $"lottery:status:{enqueueReq.RequestId}";
        var entries = new[]
        {
            new KeyValuePair<string,string>("status", enqueueReq.Status.ToString()),
            new KeyValuePair<string,string>("createdAt", enqueueReq.CreatedAt.ToString("o"))
        };
        await _cache.HashSetAsync(statusKey, entries);
    }
    catch { }

    return CreatedAtAction(nameof(Status), new { requestId }, new { requestId });
}

    [HttpGet("status/{requestId:guid}")]
    public async Task<IActionResult> Status([FromRoute] Guid requestId)
    {
        var r = await _regRepo.GetAsync(requestId);
        if (r != null) return Ok(new { requestId = r.RequestId, status = r.Status.ToString(), processedAt = r.ProcessedAt });

        // fallback to redis cache
        try
        {
            var key = $"lottery:status:{requestId}";
            var entries = await _cache.HashGetAllAsync(key);
            if (entries != null && entries.Length > 0)
            {
                var dict = entries.ToDictionary(k => k.Key, v => v.Value);
                dict.TryGetValue("status", out var s);
                dict.TryGetValue("createdAt", out var c);
                return Ok(new { requestId, status = s, createdAt = c });
            }
        }
        catch { }

        return NotFound();
    }
}
