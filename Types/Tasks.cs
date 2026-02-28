using System;
using System.Threading;
using System.Threading.Tasks;

namespace grefurBackend.Types;

/* Summary of interface: Base contract for all background tasks. */
public interface ITask
{
    Task ExecuteAsync(CancellationToken ct);
}

/* Summary of interface: Flexible contract for tasks that run at specific intervals (daily, weekly, monthly). */
public interface IPlannedTask : ITask
{
    int ExecutionHour { get; }
    int ExecutionMinute { get; }
    DateTime LastRunDate { get; set; }

    DayOfWeek? ExecutionDayOfWeek { get; } // Exs. DayOfWeek.Monday
    int? ExecutionDayOfMonth { get; }      // Exs same day each month, e.g., 15 for the 15th of every month
    bool? RunOnLastDayOfMonth { get; }     // Exs montly reporting on the last day of the month
}

/* Summary of interfaces: Tiered contracts inheriting from ITask. */
public interface ILevel1Task : ITask { }
public interface ILevel2Task : ITask { }
public interface ILevel3Task : ITask { }
public interface ILevel4Task : ITask { }
public interface ILevel5Task : ITask { }