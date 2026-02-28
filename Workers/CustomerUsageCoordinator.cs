using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using grefurBackend.Infrastructure;
using grefurBackend.Context;
using grefurBackend.Types;
using grefurBackend.Services;

namespace grefurBackend.Workers;

/* Summary of class: specialized worker component responsible for high-speed 
   usage buffering and periodic MySQL persistence via ILevel5Task. */
public class CustomerUsageCoordinator : ILevel5Task
{
    private readonly ILogger<CustomerUsageCoordinator> _logger;
    private readonly IDbContextFactory<MySqlContext> _mySqlContext;
    private ConcurrentDictionary<string, long> _pendingLogPoints = new();

    public CustomerUsageCoordinator(
        ILogger<CustomerUsageCoordinator> logger,
        IDbContextFactory<MySqlContext> mySqlContext)
    {
        _logger = logger;
        _mySqlContext = mySqlContext;
    }

    /* Summary of function: Triggered by the ScheduleService loop every 5 minutes. */
    public async Task ExecuteAsync(CancellationToken ct)
    {
        await FlushUsageToDatabaseAsync();
    }

    /* Summary of function: Increments the usage log counter for a specific customer in memory. */
    public void BufferCustomerLogPointForUsage(string customerId)
    {
        _pendingLogPoints.AddOrUpdate(customerId, 1, (key, oldValue) => oldValue + 1);
    }

    /* Summary of function: Persists buffered log points to the MySQL database using safe dictionary swapping. */
    private async Task FlushUsageToDatabaseAsync()
    {
        if (_pendingLogPoints.IsEmpty) return;

        // Atomically replace the dictionary to ensure no log points are lost during flush
        var snapshot = Interlocked.Exchange(ref _pendingLogPoints, new ConcurrentDictionary<string, long>());

        try
        {
            using var context = await _mySqlContext.CreateDbContextAsync();
            var customerIds = snapshot.Keys.ToList();

            // Batch fetch all customers in this snapshot to minimize DB roundtrips
            var customers = await context.GrefurCustomers
                .Where(c => customerIds.Contains(c.CustomerId))
                .ToListAsync();

            _logger.LogInformation("[UsageCoordinator]: Starting flush of {Count} customer usage records.", snapshot.Count);

            foreach (var customer in customers)
            {
                if (snapshot.TryGetValue(customer.CustomerId, out var usageCount))
                {
                    customer.AddLogPoint((int)usageCount);
                }
            }

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UsageCoordinator]: Critical failure during database flush. Data might be lost.");
            // Optional: Consider implementing a retry mechanism or a dead-letter-log here
        }
    }

    /* Summary of function: returns current buffer size for health monitoring. */
    public int GetBufferSize() => _pendingLogPoints.Count;
}