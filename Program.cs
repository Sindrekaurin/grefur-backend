using grefurBackend;
using grefurBackend.Engines;
using grefurBackend.Services;
using grefurBackend.Infrastructure;
using grefurBackend.Context;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DotNetEnv;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using grefurBackend.Models.AlarmConfiguration;
using grefurBackend.Infrastructure.Persistence;
using grefurBackend.Types;
using grefurBackend.Workers;

// =================================================
// PRELOAD ENVIRONMENT VARIABLES
// =================================================

Env.Load(".env.dev");

var mySqlConnectionStr = Env.GetString("MYSQL_CONNECTION");
var timescaleConnectionStr = Env.GetString("TIMESCALE_CONNECTION");
var jwtKey = Env.GetString("JWT_KEY") ?? "E65F7D48ACA0E7F38053BD41020F10DD";


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
// 1.2 AUTHENTICATION 
// =================================================================

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Forteller .NET at den skal lete etter tokenet i cookien 'grefur_auth'
            context.Token = context.Request.Cookies["grefur_auth"];
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();



// =================================================================
// 1.3 CORS POLICY
// =================================================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("GrefurDevelopmentPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "http://127.0.0.1:3000",
                "http://192.168.10.108:3000" // IP-adressen til PC-en din
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});





// =================================================
// 2.1 DATABASE REPOSITORIES
// =================================================

// MYSQL CONTEXT
if (!string.IsNullOrEmpty(mySqlConnectionStr))
{
    var connectionString = mySqlConnectionStr.Trim('"');

    try
    {
        // Denne må være inne i try-blokken for å fange krasjen når DB er nede
        var serverVersion = ServerVersion.AutoDetect(connectionString);

        builder.Services.AddDbContextFactory<MySqlContext>((serviceProvider, options) =>
        {
            options.UseMySql(connectionString, serverVersion, b =>
                    b.MigrationsAssembly("grefurBackend"))
                   .UseLoggerFactory(serviceProvider.GetRequiredService<ILoggerFactory>())
                   .EnableDetailedErrors()
                   .EnableSensitiveDataLogging();
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine("************************************************************");
        Console.WriteLine($"CRITICAL: Could not connect to MySQL. Error: {ex.Message}");
        Console.WriteLine("GREFUR-BACKEND SHUTTING DOWN.");
        Console.WriteLine("************************************************************");
        throw;
    }
}
else
{
    throw new Exception("MYSQL_CONNECTION is missing in .env.dev");
}

// TIMESCALEDB CONTEXT
if (!string.IsNullOrEmpty(timescaleConnectionStr))
{
    var connectionString = timescaleConnectionStr.Trim('"');

    try
    {
        builder.Services.AddDbContextFactory<TimescaleContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString, b =>
                    b.MigrationsAssembly("grefurBackend"))
                   .UseLoggerFactory(serviceProvider.GetRequiredService<ILoggerFactory>())
                   .EnableDetailedErrors()
                   .EnableSensitiveDataLogging();
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine("************************************************************");
        Console.WriteLine($"CRITICAL: Could not connect to TimeScaleDB. Error: {ex.Message}");
        Console.WriteLine("GREFUR-BACKEND SHUTTING DOWN.");
        Console.WriteLine("************************************************************");
        throw;
    }
}
else
{
    throw new Exception("TIMESCALE_CONNECTION is missing in your environment configuration");
}

// =================================================
// 2.1 FILE REPOSITORIES
// =================================================

builder.Services.AddScoped<MlModelRepository, SqlMlModelRepository>();



// =================================================
// COORDINATOR & TASK REGISTRATION
// =================================================

/* Summary: Automatic registration of all ITask implementations. */
var taskInterfaces = new[] {
    typeof(ILevel1Task), typeof(ILevel2Task), typeof(ILevel3Task),
    typeof(ILevel4Task), typeof(ILevel5Task)
};

var taskTypes = Assembly.GetExecutingAssembly().GetTypes()
    .Where(t => t.IsClass && !t.IsAbstract && typeof(ITask).IsAssignableFrom(t));

foreach (var type in taskTypes)
{
    // Register the implementation itself
    builder.Services.AddSingleton(type);

    // Map all applicable Level-interfaces to the singleton instance
    var implementedLevels = taskInterfaces.Where(i => i.IsAssignableFrom(type));
    foreach (var iface in implementedLevels)
    {
        builder.Services.AddSingleton(iface, sp => sp.GetRequiredService(type));
    }
}

builder.Services.AddSingleton<ScheduleService>();
builder.Services.AddHostedService<EngineWorker>();


// =================================================================
// 3. SERVICES
// =================================================================


builder.Services.AddSingleton<AlarmService>(sp =>
    new AlarmService(
        sp.GetRequiredService<ILogger<AlarmService>>(),
        sp.GetRequiredService<IDbContextFactory<MySqlContext>>() // SQL Factory
    ));

builder.Services.AddSingleton<UserService>(sp =>
    new UserService(
        sp.GetRequiredService<IDbContextFactory<MySqlContext>>(),
        sp.GetRequiredService<ILogger<UserService>>()
    ));

builder.Services.AddSingleton<CustomerService>(sp =>
    new CustomerService(
        sp.GetRequiredService<ILogger<CustomerService>>(),
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<IDbContextFactory<MySqlContext>>(),
        sp.GetRequiredService<UserService>()
    )
);

builder.Services.AddSingleton<DeviceService>(sp =>
    new DeviceService(
        sp.GetRequiredService<IDbContextFactory<MySqlContext>>(),
        sp.GetRequiredService<ILogger<DeviceService>>(),
        sp.GetRequiredService<EventBus>()
    ));

builder.Services.AddSingleton<CacheService>(sp =>
    new CacheService(
        sp.GetRequiredService<ILogger<CacheService>>(),
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<CustomerService>(),
        sp.GetRequiredService<IDbContextFactory<MySqlContext>>()
    ));

builder.Services.AddSingleton<MqttService>(sp =>
    new MqttService(
        sp.GetRequiredService<ILogger<MqttService>>(),
        sp.GetRequiredService<MqttSettings>(),
        sp.GetRequiredService<EventBus>()
    ));

builder.Services.AddSingleton<LoggerService>(sp =>
    new LoggerService(
        sp.GetRequiredService<ILogger<LoggerService>>(),
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<IDbContextFactory<TimescaleContext>>(),
        sp.GetRequiredService<CustomerUsageCoordinator>(),
        sp.GetRequiredService<IHostApplicationLifetime>()
    ));

builder.Services.AddSingleton<MlTrainingService>(sp =>
    new MlTrainingService(
        sp.GetRequiredService<AlarmService>(),
        sp.GetRequiredService<LoggerService>(),
        sp.GetRequiredService<ILogger<MlTrainingService>>(),
        sp.GetRequiredService<IDbContextFactory<MySqlContext>>()
    ));

builder.Services.AddSingleton<VirtualSensorService>(sp =>
    new VirtualSensorService(
        sp.GetRequiredService<ILogger<VirtualSensorService>>(),
        sp.GetRequiredService<IDbContextFactory<MySqlContext>>(),
        sp.GetRequiredService<UserService>(),
        sp.GetRequiredService<MqttService>()
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
        sp.GetRequiredService<IDbContextFactory<MySqlContext>>(),
        sp.GetRequiredService<ILogger<CacheWarmupEngine>>()
    ));

builder.Services.AddSingleton<DeviceDiscoveryEngine>(sp =>
    new DeviceDiscoveryEngine(
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<CacheService>(),
        sp.GetRequiredService<MqttService>(),
        sp.GetRequiredService<IDbContextFactory<MySqlContext>>(),
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
        sp.GetRequiredService<AlarmService>(),
        sp.GetRequiredService<ILogger<PredictionEngine>>()    
    ));

builder.Services.AddSingleton<ChangeCustomerDataEngine>(sp =>
    new ChangeCustomerDataEngine(
        sp.GetRequiredService<ILogger<ChangeCustomerDataEngine>>(),
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<CustomerService>()
    ));

builder.Services.AddSingleton<BootstrapEngine>(sp =>
    new BootstrapEngine(
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<ILogger<BootstrapEngine>>()
    ));

builder.Services.AddSingleton<DeviceAuthEngine>(sp =>
    new DeviceAuthEngine(
        sp.GetRequiredService<EventBus>(),
        sp.GetRequiredService<MqttService>(),
        sp.GetRequiredService<DeviceService>(),
        sp.GetRequiredService<IDbContextFactory<MySqlContext>>(),
        sp.GetRequiredService<ILogger<DeviceAuthEngine>>()
    ));

// =================================================================
// 5. WEB API & HOSTED WORKERS
// =================================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
//builder.Services.AddHostedService(sp => sp.GetRequiredService<GrefurUsageCoordinator>());
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

// =================================================================
// 6. MIDDLEWARE PIPELINE
// =================================================================
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    app.Use(async (Context, Next) =>
    {
        // Vi filtrerer ut OPTIONS slik at du bare ser de faktiske forespørslene
        if (Context.Request.Method != "OPTIONS")
        {
            var logger = Context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("[HTTP] {Method} {Path}{Query}",
                Context.Request.Method,
                Context.Request.Path,
                Context.Request.QueryString);
        }

        await Next();
    });

}

// Test MySQL connection at startup
using (var scope = app.Services.CreateScope())
{
    // MYSQL TEST
    try
    {
        var mySqlFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MySqlContext>>();
        using var mySqlContext = mySqlFactory.CreateDbContext();
        mySqlContext.TestConnection();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[Startup] Failed to initialize MySqlContext: {ex.Message}");
        Console.ResetColor();
    }

    // TIMESCALE TEST
    try
    {
        var timescaleFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TimescaleContext>>();
        using var timescaleContext = timescaleFactory.CreateDbContext();
        timescaleContext.TestConnection();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[Startup] Failed to initialize TimescaleContext: {ex.Message}");
        Console.ResetColor();
    }
}

// Analysing incomming requests and routing them to controllers (api interface switch)
app.UseRouting();

// app.UseHttpsRedirection();

// Enable CORS defined in step 1.2
app.UseCors("GrefurDevelopmentPolicy");
app.Urls.Add("http://0.0.0.0:5000"); // Lytt på alle adresser

// Verify JWT token "grefur_auth" cookie
app.UseAuthentication();

// Verifies correct rights for the incoming user token
app.UseAuthorization();

// Connects pre-defined routes to controllers
app.MapControllers();

// Start application and webserver
await app.RunAsync();