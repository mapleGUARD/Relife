namespace Relife.Core.Models;

/// <summary>
/// Persistent state for TimeGuard service
/// </summary>
public class TimeGuardState
{
    /// <summary>
    /// Remaining block time in milliseconds
    /// </summary>
    public long RemainingBlockTimeMs { get; set; }

    /// <summary>
    /// Last monotonic timestamp (from Stopwatch.GetTimestamp())
    /// </summary>
    public long LastMonotonicTimestamp { get; set; }

    /// <summary>
    /// Last system time (UTC ticks) - used for tamper detection
    /// </summary>
    public long LastSystemTimeTicks { get; set; }

    /// <summary>
    /// Frequency of the high-resolution timer
    /// </summary>
    public long TimestampFrequency { get; set; }

    /// <summary>
    /// Indicates if tamper was previously detected
    /// </summary>
    public bool TamperFlagged { get; set; }

    /// <summary>
    /// Number of heartbeats saved
    /// </summary>
    public long HeartbeatCount { get; set; }
}
