using Lottery.Api.Workers;
using Lottery.Api.Startup;
using Lottery.Application.Interfaces;
using Lottery.Infra.Data;
using Lottery.Infra.kafka;
using Lottery.Infra.Repositories;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Lottery.Infra.redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connStr = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Host=localhost;Database=lottery;Username=postgres;Password=postgres";
builder.Services.AddDbContext<ApplicationDbContext>(opt => opt.UseNpgsql(connStr));

var redisConn = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
var redis = StackExchange.Redis.ConnectionMultiplexer.Connect(redisConn);
builder.Services.AddSingleton(redis);

builder.Services.AddScoped<IRedisService, RedisService>();
builder.Services.AddScoped<IRegistrationRepository, RegisterRepository>();
builder.Services.AddScoped<ICampaignRepository, CampaignRepository>();
builder.Services.AddScoped<IMessagingService, KafkaProducerService>();
// builder.Services.AddHostedService<KafkaConsumerHostedService>();
builder.Services.AddSingleton<KafkaConsumerHostedService>();
builder.Services.Configure<RegistrationProcessingOptions>(
    builder.Configuration.GetSection(RegistrationProcessingOptions.SectionName));
builder.Services.AddHostedService<RegistrationProcessingHostedService>();

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.UseDeveloperExceptionPage();
// reseed campaigns (moved to DatabaseSeeder)
if (app.Environment.IsDevelopment())
{
    app.EnsureCampaignSeed();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
var runTask = app.RunAsync();

// Start KafkaConsumerHostedService manually after the app is running
using (var scope = app.Services.CreateScope())
{
   var consumer = scope.ServiceProvider.GetRequiredService<KafkaConsumerHostedService>();

    // fire and forget background task
    _ = Task.Run(() => consumer.StartAsync(CancellationToken.None));
}


await runTask;