using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using grefurBackend.Events;
using grefurBackend.Infrastructure;
using grefurBackend.Workers;
using grefurBackend.Types;

namespace grefurBackend.Services;

/* Summary of class: Domain service that manages event scheduling and 
   orchestrates execution of tiered task groups (Level 1-5) and specific planned tasks. */
public class ScheduleService
{
    private readonly ILogger<ScheduleService> _logger;
    private readonly EventBus _eventBus;
    private readonly CustomerUsageCoordinator _usageCoordinator;

    private readonly IEnumerable<ILevel1Task> _level1Tasks;
    private readonly IEnumerable<ILevel2Task> _level2Tasks;
    private readonly IEnumerable<ILevel3Task> _level3Tasks;
    private readonly IEnumerable<ILevel4Task> _level4Tasks;
    private readonly IEnumerable<ILevel5Task> _level5Tasks;
    private readonly IEnumerable<IPlannedTask> _plannedTasks;

    private readonly List<PlannedEvent> _plannedEvents = new();
    private readonly object _lock = new();

    private DateTime _last1s = DateTime.MinValue;
    private DateTime _last15s = DateTime.MinValue;
    private DateTime _last1m = DateTime.MinValue;
    private DateTime _last5m = DateTime.MinValue;

    public ScheduleService(
        ILogger<ScheduleService> logger,
        EventBus eventBus,
        CustomerUsageCoordinator usageCoordinator,
        IEnumerable<ILevel1Task> level1Tasks,
        IEnumerable<ILevel2Task> level2Tasks,
        IEnumerable<ILevel3Task> level3Tasks,
        IEnumerable<ILevel4Task> level4Tasks,
        IEnumerable<ILevel5Task> level5Tasks,
        IEnumerable<IPlannedTask> plannedTasks)
    {
        _logger = logger;
        _eventBus = eventBus;
        _usageCoordinator = usageCoordinator;
        _level1Tasks = level1Tasks;
        _level2Tasks = level2Tasks;
        _level3Tasks = level3Tasks;
        _level4Tasks = level4Tasks;
        _level5Tasks = level5Tasks;
        _plannedTasks = plannedTasks;
    }

    public void AddPlannedEvent(DateTime scheduledDate, Event evt)
    {
        lock (_lock)
        {
            _plannedEvents.Add(new PlannedEvent(Guid.NewGuid().ToString(), scheduledDate, evt));
        }
    }

    /* Summary of function: The main engine tick. Now processes declarative IPlannedTask logic. */
    public async Task RunPendingTasksAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // 1. Check complex planned tasks (Daily, Weekly, Monthly)
        foreach (var task in _plannedTasks)
        {
            if (IsTaskReady(task, now))
            {
                try
                {
                    await task.ExecuteAsync(ct);
                    task.LastRunDate = now; // Mark as done for today
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing planned task {TaskType}", task.GetType().Name);
                }
            }
        }

        // 2. Regular tiered interval tasks
        await RunTaskGroup(_level1Tasks, ct);

        if (now - _last1s >= TimeSpan.FromSeconds(1))
        {
            await RunTaskGroup(_level2Tasks, ct);
            await ProcessPlannedEventsAsync(now);
            _last1s = now;
        }

        if (now - _last15s >= TimeSpan.FromSeconds(15))
        {
            await RunTaskGroup(_level3Tasks, ct);
            _last15s = now;
        }

        if (now - _last1m >= TimeSpan.FromMinutes(1))
        {
            await RunTaskGroup(_level4Tasks, ct);
            _last1m = now;
        }

        if (now - _last5m >= TimeSpan.FromMinutes(5))
        {
            await RunTaskGroup(_level5Tasks, ct);
            _last5m = now;
        }
    }

    /* Summary of function: Validates if a planned task should run based on its declarative properties. */
    private bool IsTaskReady(IPlannedTask task, DateTime now)
    {
        // Avoid double execution on the same day
        if (task.LastRunDate.Date == now.Date) return false;

        // Check Time (Hour/Minute)
        if (now.Hour < task.ExecutionHour) return false;
        if (now.Hour == task.ExecutionHour && now.Minute < task.ExecutionMinute) return false;

        // Check Day of Week
        if (task.ExecutionDayOfWeek.HasValue && now.DayOfWeek != task.ExecutionDayOfWeek.Value) return false;

        // Check Day of Month
        if (task.ExecutionDayOfMonth.HasValue && now.Day != task.ExecutionDayOfMonth.Value) return false;

        // Check Last Day of Month
        if (task.RunOnLastDayOfMonth == true)
        {
            int lastDay = DateTime.DaysInMonth(now.Year, now.Month);
            if (now.Day != lastDay) return false;
        }

        return true;
    }

    private async Task RunTaskGroup<T>(IEnumerable<T> tasks, CancellationToken ct) where T : ITask
    {
        if (tasks == null) return;
        var taskList = tasks.Select(async t =>
        {
            try { await t.ExecuteAsync(ct); }
            catch (Exception ex) { _logger.LogError(ex, "Error executing task {TaskType}", t.GetType().Name); }
        });
        await Task.WhenAll(taskList);
    }

    private async Task ProcessPlannedEventsAsync(DateTime now)
    {
        List<PlannedEvent> toPublish;
        lock (_lock)
        {
            toPublish = _plannedEvents.Where(e => e.PublishTime <= now).ToList();
            foreach (var pe in toPublish) _plannedEvents.Remove(pe);
        }
        foreach (var pe in toPublish) await _eventBus.Publish(pe.EventPayload).ConfigureAwait(false);
    }
}