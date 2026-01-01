using grefurBackend;
using grefurBackend.Engines;
using grefurBackend.Services;
using grefurBackend.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// =================================================================
// 1. CORE INFRASTRUCTURE
// =================================================================
builder.Services.AddSingleton<EventBus>();
builder.Services.AddSingleton<MqttSettings>(new MqttSettings());
builder.Services.AddSingleton<EventLoggerService>();

// =================================================================
// 2. DOMAIN SERVICES
// =================================================================
builder.Services.AddSingleton<LoggerService>(sp =>
    new LoggerService(
        sp.GetRequiredService<ILogger<LoggerService>>(),
        sp.GetRequiredService<EventBus>()
    ));

builder.Services.AddSingleton<AlarmService>(sp =>
    new AlarmService(
        sp.GetRequiredService<ILogger<AlarmService>>(),
        sp.GetRequiredService<EventBus>()
    ));

builder.Services.AddSingleton<CustomerService>(sp =>
    new CustomerService(
        sp.GetRequiredService<ILogger<CustomerService>>(),
        sp.GetRequiredService<EventBus>()
    ));

builder.Services.AddSingleton<CacheService>(sp =>
    new CacheService(
        sp.GetRequiredService<ILogger<CacheService>>(),
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<CustomerService>()
    ));

builder.Services.AddSingleton<MqttService>(sp =>
    new MqttService(
        sp.GetRequiredService<ILogger<MqttService>>(),
        sp.GetRequiredService<MqttSettings>(),
        sp.GetRequiredService<EventBus>()
    ));

builder.Services.AddSingleton<MlTrainingService>(sp =>
    new MlTrainingService(
        sp.GetRequiredService<AlarmService>(),
        sp.GetRequiredService<LoggerService>(),
        sp.GetRequiredService<ILogger<MlTrainingService>>()
    ));

// =================================================================
// 3. ENGINES (EVENT LISTENERS)
// =================================================================

builder.Services.AddSingleton<CustomerLoadEngine>(sp =>
    new CustomerLoadEngine(
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<CustomerService>(),
        sp.GetRequiredService<ILogger<CustomerLoadEngine>>()
    ));

builder.Services.AddSingleton<CacheWarmupEngine>(sp =>
    new CacheWarmupEngine(
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<CacheService>(),
        sp.GetRequiredService<CustomerService>(),
        sp.GetRequiredService<ILogger<CacheWarmupEngine>>()
    ));

builder.Services.AddSingleton<DeviceDiscoveryEngine>(sp =>
    new DeviceDiscoveryEngine(
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<CacheService>(),
        sp.GetRequiredService<MqttService>(),
        sp.GetRequiredService<ILogger<DeviceDiscoveryEngine>>()
    ));

builder.Services.AddSingleton<TopicTopologyEngine>(sp =>
    new TopicTopologyEngine(
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<CacheService>(),
        sp.GetRequiredService<MqttService>(),
        sp.GetRequiredService<ILogger<TopicTopologyEngine>>()
    ));

builder.Services.AddSingleton<ValueHandlerEngine>(sp =>
    new ValueHandlerEngine(
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<CacheService>(),
        sp.GetRequiredService<ILogger<ValueHandlerEngine>>()
    ));

builder.Services.AddSingleton<SubscriptionEngine>(sp =>
    new SubscriptionEngine(
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<LoggerService>(),
        sp.GetRequiredService<ILogger<SubscriptionEngine>>()
    ));

builder.Services.AddSingleton<AlarmEngine>(sp =>
    new AlarmEngine(
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<AlarmService>(),
        sp.GetRequiredService<ILogger<AlarmEngine>>()
    ));

builder.Services.AddSingleton<LoggerEngine>(sp =>
    new LoggerEngine(
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<LoggerService>(),
        sp.GetRequiredService<ILogger<LoggerEngine>>()
    ));

builder.Services.AddSingleton<PredictionEngine>(sp =>
    new PredictionEngine(
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<MlTrainingService>(),
        sp.GetRequiredService<ILogger<PredictionEngine>>()
    ));

builder.Services.AddSingleton<BootstrapEngine>(sp =>
    new BootstrapEngine(
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<ILogger<BootstrapEngine>>()
    ));

// =================================================================
// 4. WEB API & HOSTED WORKERS
// =================================================================
builder.Services.AddControllers();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

// =================================================================
// 5. MIDDLEWARE PIPELINE
// =================================================================
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();
app.MapControllers();

// Start application
await app.RunAsync();