namespace ConstructOPX.Domain.Features.Workforce;

/// <summary>
/// Where a worker stands. The desktop app listed active, resigned, retired, suspended and on-leave as unconstrained text (any value
/// could replace any other) and had no way to record a dismissal, although its older version did.
/// </summary>
public enum WorkerStatus
{
    Active = 0,
    OnLeave = 1,
    Suspended = 2,
    Resigned = 3,
    Retired = 4,
    Terminated = 5
}

public enum RateUnit
{
    /// <summary>A pay per working day (the usual construction wage). Converted to hourly with the company's hours per day.</summary>
    Daily = 0,

    /// <summary>A pay per hour worked.</summary>
    Hourly = 1
}

public enum RateChangeCategory
{
    /// <summary>The rate a worker was hired at.</summary>
    Initial = 0,
    Promotion = 1,
    Demotion = 2,
    RateAdjustment = 3
}

public static class WorkerStatusRules
{
    public static bool IsTerminal(WorkerStatus status) => status is WorkerStatus.Resigned or WorkerStatus.Retired or WorkerStatus.Terminated;

    public static string Label(WorkerStatus status) => status switch
    {
        WorkerStatus.OnLeave => "On leave",
        _ => status.ToString()
    };
}
