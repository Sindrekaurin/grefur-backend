using Microsoft.EntityFrameworkCore;
using grefurBackend.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

namespace grefurBackend.Context
{
    public class TimescaleContext : DbContext
    {
        private readonly ILogger<TimescaleContext> _logger;

        public TimescaleContext(DbContextOptions<TimescaleContext> options, ILogger<TimescaleContext> logger) : base(options)
        {
            _logger = logger;
            _logger.LogDebug("[TimescaleContext] Initialized");
        }

        public DbSet<SensorReading> SensorReadings { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var callerInfo = GetCallerInfo();
                _logger.LogDebug("[TimescaleContext] SaveChangesAsync triggered by: {Caller} at {Time}", callerInfo, DateTime.Now.ToString("HH:mm:ss"));

                var result = await base.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("[TimescaleContext] Persisted {Count} readings from {Caller}", result, callerInfo);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "[TimescaleContext] ERROR in SaveChangesAsync");
                throw;
            }
        }

        private string GetCallerInfo()
        {
            var stackTrace = new StackTrace();
            var frames = stackTrace.GetFrames();

            // {Check if frames is null before using FirstOrDefault}
            if (frames == null) return "Unknown Source";

            var callerFrame = frames.FirstOrDefault(f =>
            {
                var method = f.GetMethod();
                var typeName = method?.DeclaringType?.FullName ?? "";
                // {Matches both MySqlContext and TimescaleContext by checking for .Context}
                return typeName.StartsWith("grefurBackend") && !typeName.Contains(".Context");
            });

            // {Explicitly check if method and DeclaringType exist before accessing properties}
            if (callerFrame != null)
            {
                var method = callerFrame.GetMethod();
                if (method?.DeclaringType != null)
                {
                    return $"{method.DeclaringType.Name}.{method.Name}";
                }
            }

            return "Unknown Source";
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SensorReading>(entity =>
            {
                entity.ToTable("sensorReadings");
                entity.HasKey(e => new { e.Timestamp, e.Topic, e.CustomerId });

                entity.Property(e => e.CustomerId).HasColumnName("customerId");
                entity.Property(e => e.Timestamp).HasColumnName("timestamp");
                entity.Property(e => e.DeviceId).HasColumnName("deviceId");
                entity.Property(e => e.Property).HasColumnName("property");
                entity.Property(e => e.Value).HasColumnName("value");
            });
        }

        public void TestConnection()
        {
            try
            {
                _logger.LogInformation("[TimescaleContext] Testing database connection and ensuring schema...");


                Database.EnsureCreated();

                if (Database.CanConnect())
                {
                    _logger.LogInformation("[TimescaleContext] Connection verified");
                }
                else
                {
                    throw new Exception("Cannot connect to TimescaleDB");
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "[TimescaleContext] TimescaleDB connection failed");
            }
        }
    }
}