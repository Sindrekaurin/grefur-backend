using grefurBackend;
using grefurBackend.Engines;
using grefurBackend.Services;
using grefurBackend.Infrastructure;
using grefurBackend.Context;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DotNetEnv;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;


// =================================================
// PRELOAD ENVIRONMENT VARIABLES
// =================================================

Env.Load(".env.dev");

var mySqlConnectionStr = Env.GetString("MYSQL_CONNECTION");
var timescaleConnectionStr = Env.GetString("TIMESCALE_CONNECTION");



// =================================================
// 1.1 BUILD WEB APPLICATION
// =================================================

var builder = WebApplication.CreateBuilder(args);

// =================================================================
// 1.1 CORE INFRASTRUCTURE
// =================================================================
builder.Services.AddSingleton<EventBus>();
builder.Services.AddSingleton<MqttSettings>(new MqttSettings());
builder.Services.AddSingleton<EventLoggerService>();

// =================================================================
// 1.2 CORS POLICY
// =================================================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowGrefurDatalake", policy =>
    {
        policy.WithOrigins("http://localhost:3000") // Your React app URL
              .AllowAnyMethod()                     // Allow GET, POST, etc.
              .AllowAnyHeader()                     // Allow any headers
              .AllowCredentials();                  // Allow credentials if needed
    });
});





// =================================================
// 2.1 DATABASE REPOSITORIES
// =================================================

// MYSQL CONTEXT
if (!string.IsNullOrEmpty(mySqlConnectionStr))
{
    var connectionString = mySqlConnectionStr.Trim('"');
    var serverVersion = ServerVersion.AutoDetect(connectionString);

    builder.Services.AddDbContext<MySqlContext>((serviceProvider, options) =>
    {
        options.UseMySql(connectionString, serverVersion)
               .UseLoggerFactory(serviceProvider.GetRequiredService<ILoggerFactory>())
               .EnableDetailedErrors();
    });
}
else
{
    throw new Exception("MYSQL_CONNECTION is missing in .env.dev");
}






// =================================================================
// 3. DOMAIN SERVICES
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

builder.Services.AddScoped<CustomerService>(sp =>
    new CustomerService(
        sp.GetRequiredService<ILogger<CustomerService>>(),
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<MySqlContext>()
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
// 4. ENGINES (EVENT LISTENERS)
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

//builder.Services.AddSingleton<ChangeCustomerDataEngine>();
//builder.Services.AddSingleton<PredictionEngine>();

builder.Services.AddSingleton<PredictionEngine>(sp =>
    new PredictionEngine(
        sp.GetRequiredService<EventBus>(),                   // Arg 1: EventBus
        sp.GetRequiredService<MlTrainingService>(),          // Arg 2: MlTrainingService
        sp.GetRequiredService<ILogger<PredictionEngine>>()    // Arg 3: Logger
    ));

builder.Services.AddSingleton<ChangeCustomerDataEngine>(sp =>
    new ChangeCustomerDataEngine(
        sp.GetRequiredService<ILogger<ChangeCustomerDataEngine>>(), // Arg 1
        sp.GetRequiredService<CustomerService>(),                 // Arg 2 (Nå riktig)
        sp.GetRequiredService<EventBus>()                         // Arg 3 (Nå riktig)
    ));

builder.Services.AddSingleton<BootstrapEngine>(sp =>
    new BootstrapEngine(
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<ILogger<BootstrapEngine>>()
    ));

// =================================================================
// 5. WEB API & HOSTED WORKERS
// =================================================================
builder.Services.AddControllers();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

// =================================================================
// 6. MIDDLEWARE PIPELINE
// =================================================================
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    // Test MySQL connection at startup
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<MySqlContext>();
            context.TestConnection();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Startup] Failed to initialize MySqlContext: {ex.Message}");
            Console.ResetColor();
        }
    }
}

// Enable CORS defined in step 1.2
app.UseCors("AllowGrefurDatalake");

app.UseRouting();
app.MapControllers();

// Start application
await app.RunAsync();