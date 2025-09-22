using Confluent.Kafka;
using Lottery.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Lottery.Application.DTOs;
using System.Text.Json;

namespace Lottery.Infra.kafka;

public class KafkaProducerService : IMessagingService, IDisposable
{
    private readonly IProducer<Null, string> _producer;
    private readonly string _topic;

    public KafkaProducerService(IConfiguration config)
    {
        var broker = config["Kafka:BootstrapServers"] ?? "localhost:9092";
        _topic = config["Kafka:Topic"] ?? "lottery-events";
        var pconf = new ProducerConfig { BootstrapServers = broker };
        _producer = new ProducerBuilder<Null, string>(pconf).Build();
    }

    public async Task EnqueueAsync(EnqueueRequest req)
    {
        var json = JsonSerializer.Serialize(req);
        await _producer.ProduceAsync(_topic, new Message<Null, string> { Value = json });
    }

    public Task<KeyValuePair<string,string>[]> GetStatusAsync(Guid requestId)
    {
        // status storage remains in DB; return empty - consumer writes status to DB/Redis as before
        return Task.FromResult(Array.Empty<KeyValuePair<string,string>>());
    }

    public void Dispose() => _producer.Flush(TimeSpan.FromSeconds(5));
}
