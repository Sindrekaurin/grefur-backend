using Microsoft.EntityFrameworkCore;
using grefurBackend.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;

namespace grefurBackend.Context
{
    public class MySqlContext : DbContext
    {
        private readonly ILogger<MySqlContext> _logger;

        public MySqlContext(DbContextOptions<MySqlContext> Options, ILogger<MySqlContext> Logger) : base(Options)
        {
            _logger = Logger;

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("[MySqlContext] Database Context Initialized");
            Console.ResetColor();
        }

        public DbSet<GrefurCustomer> GrefurCustomers { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken CancellationToken = default)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"[MySqlContext] Initiating SaveChangesAsync at {DateTime.Now:HH:mm:ss}");
                Console.ResetColor();

                var Result = await base.SaveChangesAsync(CancellationToken);

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"[MySqlContext] Successfully persisted {Result} changes to MySQL");
                Console.ResetColor();

                return Result;
            }
            catch (Exception Ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[MySqlContext] CRITICAL ERROR: {Ex.Message}");
                Console.ResetColor();
                throw;
            }
        }

        protected override void OnModelCreating(ModelBuilder ModelBuilder)
        {
            base.OnModelCreating(ModelBuilder);

            ModelBuilder.Entity<GrefurCustomer>()
                .Property(C => C.RegisteredDevices)
                .HasConversion(
                    V => JsonSerializer.Serialize(V, (JsonSerializerOptions)null),
                    V => JsonSerializer.Deserialize<List<string>>(V, (JsonSerializerOptions)null) ?? new List<string>()
                );
        }

        public void TestConnection()
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("[MySqlContext] Testing database connection...");

                Database.OpenConnection();
                Database.CloseConnection();

                Console.WriteLine("[MySqlContext] Connection test successful");
                Console.ResetColor();
            }
            catch (Exception Ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[MySqlContext] Connection test failed: {Ex.Message}");
                Console.ResetColor();
            }
        }
    }
}