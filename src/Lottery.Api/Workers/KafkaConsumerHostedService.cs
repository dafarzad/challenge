using Confluent.Kafka;
using Lottery.Application.DTOs;
using Lottery.Domain.Models;
using Lottery.Infra.Data;
using System.Text.Json;
using System.Threading.Channels;

public class KafkaConsumerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConsumer<Ignore, string> _consumer;
    private readonly ILogger<KafkaConsumerHostedService> _logger;
    private readonly string _topic;

    private readonly Channel<ConsumeResult<Ignore, string>> _channel;
    private readonly int _batchSize = 50;
    private readonly TimeSpan _batchInterval = TimeSpan.FromSeconds(5);

    public KafkaConsumerHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<KafkaConsumerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var broker = config["Kafka:BootstrapServers"];
        _topic = config["Kafka:Topic"] ?? "lottery-requests";
        var group = config["Kafka:Group"] ?? "lottery-consumers";

        var conf = new ConsumerConfig
        {
            BootstrapServers = broker,
            GroupId = group,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            SecurityProtocol = SecurityProtocol.Plaintext,
            EnableAutoCommit = false // we commit manually after DB save
        };

        _consumer = new ConsumerBuilder<Ignore, string>(conf).Build();

        // Bounded channel: max 100k messages in memory
        _channel = Channel.CreateBounded<ConsumeResult<Ignore, string>>(
            new BoundedChannelOptions(100_000)
            {
                FullMode = BoundedChannelFullMode.Wait, // block producer if full
                SingleReader = true,
                SingleWriter = false
            });
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Kafka Consumer is starting.");

        _consumer.Subscribe(_topic);

        var consumeTask = ConsumeLoopAsync(stoppingToken);
        var flushTask = FlushLoopAsync(stoppingToken);

         await Task.WhenAll(consumeTask, flushTask);
    }

    private async Task ConsumeLoopAsync(CancellationToken ct)
    {
        _logger.LogInformation("Kafka consumer loop starting");

        try
        {
            while (!ct.IsCancellationRequested)

            {
                var consumeResult = _consumer.Consume(ct);

                if (consumeResult?.Message == null) continue;

                 _logger.LogInformation("Consumed message: {Message}", consumeResult?.Message);

                // Push into channel (will backpressure if full)
                await _channel.Writer.WriteAsync(consumeResult!, ct);
            }
        }
        catch (OperationCanceledException ex)
        {
         _logger.LogInformation(ex, "Consumer loop cancelled");
        }
        catch (Exception ex)
        {

            _logger.LogError(ex.Message);
        }
        finally
        {
             _logger.LogInformation("channel produce complete");
            _channel.Writer.Complete();
        }
    }

    private async Task FlushLoopAsync(CancellationToken ct)
    {
        var buffer = new List<ConsumeResult<Ignore, string>>();
        var lastFlush = DateTime.UtcNow;

        await foreach (var cr in _channel.Reader.ReadAllAsync(ct))
        {
            buffer.Add(cr);

            bool shouldFlush =
                buffer.Count >= _batchSize ||
                (DateTime.UtcNow - lastFlush) >= _batchInterval;

            if (shouldFlush)
            {
                await FlushBatchToDbAsync(buffer, ct);
                buffer.Clear();
                lastFlush = DateTime.UtcNow;
            }
        }

        // Flush remaining on shutdown
        if (buffer.Count > 0)
        {
            await FlushBatchToDbAsync(buffer, CancellationToken.None);
        }
    }

    private async Task FlushBatchToDbAsync(
        List<ConsumeResult<Ignore, string>> items,
        CancellationToken ct)
    {
        if (items.Count == 0) return;

        try
        {
            var registrations = new List<Registration>();

            foreach (var cr in items)
            {
                try
                {
                    var request = JsonSerializer.Deserialize<EnqueueRequest>(cr.Message.Value);

                    if (request is null) continue;

                    // Map record → EF entity
                    var reg = new Registration
                    {
                        RequestId = request.RequestId,
                        FirstName = request.FirstName,
                        LastName = request.LastName,
                        Phone = request.Phone,
                        NationalId = request.NationalId,
                        CampaignId = request.CampaignId,
                        Status = request.Status,
                        CreatedAt = request.CreatedAt
                    };
                    registrations.Add(reg);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex,
                        "Invalid JSON. Skipping. Offset: {Offset}",
                        cr.TopicPartitionOffset);
                }
            }

            if (registrations.Count == 0) return;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            db.LotteryRequests.AddRange(registrations);
            await db.SaveChangesAsync(ct);

            var last = items.Last();
            _consumer.Commit(last);

            _logger.LogInformation(
                "Flushed {Count} records. Committed offset {Offset}.",
                registrations.Count,
                last.TopicPartitionOffset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing batch to DB. Messages may be retried.");
            // TODO: Push failed items to DLQ instead of dropping
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _consumer.Close();
            _consumer.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error closing Kafka consumer");
        }

        await base.StopAsync(cancellationToken);
    }

    //protected override void Dispose(bool disposing)
    //{
    //    if (disposing)
    //    {
    //        _consumer?.Close();
    //        _consumer?.Dispose();
    //    }
    //    base.Dispose(disposing);
    //}
}