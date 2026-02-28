using Microsoft.EntityFrameworkCore;
using grefurBackend.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using grefurBackend.Models.AlarmConfiguration;

namespace grefurBackend.Context
{
    public class MySqlContext : DbContext
    {
        private readonly ILogger<MySqlContext> _logger;

        public MySqlContext(DbContextOptions<MySqlContext> options, ILogger<MySqlContext> logger) : base(options)
        {
            _logger = logger;
            _logger.LogDebug("[MySqlContext] Database Context Initialized");
        }

        public DbSet<GrefurCustomer> GrefurCustomers { get; set; }
        public DbSet<GrefurUser> GrefurUsers { get; set; }
        public DbSet<GrefurDevice> GrefurDevices { get; set; }
        public DbSet<MlAlarmConfiguration> MlAlarmConfigurations { get; set; }

        public DbSet<VirtualGrefurDevice> VirtualGrefurDevice { get; set; }
        public DbSet<VirtualSensorValue> VirtualSensorValues { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var callerInfo = GetCallerInfo();
                _logger.LogDebug("[MySqlContext] SaveChangesAsync triggered by: {Caller} at {Time}", callerInfo, DateTime.Now.ToString("HH:mm:ss"));

                var result = await base.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("[MySqlContext] Persisted {Count} changes from {Caller}", result, callerInfo);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "[MySqlContext] CRITICAL ERROR in SaveChangesAsync");
                throw;
            }
        }

        private string GetCallerInfo()
        {
            var stackTrace = new StackTrace();
            var frames = stackTrace.GetFrames();

            // {Check if frames is null before accessing LINQ methods}
            if (frames == null)
            {
                return "Unknown Source";
            }

            var callerFrame = frames.FirstOrDefault(f =>
            {
                var method = f.GetMethod();
                var typeName = method?.DeclaringType?.FullName ?? "";
                // {Filters out any class within the Context namespace to find the actual service/controller}
                return typeName.StartsWith("grefurBackend") && !typeName.Contains(".Context.");
            });

            if (callerFrame != null)
            {
                var method = callerFrame.GetMethod();
                // {Explicitly check for method and DeclaringType to satisfy the compiler}
                if (method?.DeclaringType != null)
                {
                    return $"{method.DeclaringType.Name}.{method.Name}";
                }
            }

            return "Unknown Source";
        }

        /* Summary of function: Configures the database schema and inheritance strategy for grefur devices */
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // {Configures TPH (Table-per-Hierarchy) inheritance for devices}
            modelBuilder.Entity<GrefurDevice>()
                .HasDiscriminator<string>("DeviceCategory")
                .HasValue<GrefurDevice>("Physical")
                .HasValue<VirtualGrefurDevice>("Virtual");

            // {Optional: Ensures that the virtual sensor values are deleted if the device is removed}
            modelBuilder.Entity<VirtualSensorValue>()
                .HasOne<VirtualGrefurDevice>()
                .WithMany(d => d.SensorValues)
                .HasForeignKey(v => v.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        public void TestConnection()
        {
            try
            {
                _logger.LogInformation("[MySqlContext] Testing database connection...");

                if (!Database.CanConnect())
                {
                    throw new Exception("Cannot connect to MySQL database");
                }

                _logger.LogInformation("[MySqlContext] Connection test successful");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "[MySqlContext] Connection test failed");
            }
        }
    }
}