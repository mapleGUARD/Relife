using Relife.Core.Services;

namespace Relife.Core;

/// <summary>
/// Simplified facade for TimeGuard with minute-based API
/// This wrapper provides a simpler interface for the test scenarios
/// </summary>
public class TimeGuard : IDisposable
{
    private readonly Services.TimeGuard _innerGuard;
    private readonly string _stateFile;
    private readonly string _encryptionKey = "DefaultSecureKey-Relife-2026";
    private long _initialMinutes;

    /// <summary>
    /// Gets whether the system is in lockdown mode due to tampering
    /// </summary>
    public bool IsLockedDown => _innerGuard.IsTampered;

    /// <summary>
    /// Gets remaining time in minutes
    /// </summary>
    public double RemainingMinutes => _innerGuard.RemainingBlockTimeMs / 60000.0;

    /// <summary>
    /// Constructor for loading existing state
    /// </summary>
    public TimeGuard() : this(0, null)
    {
        // Will load from saved state if exists
    }

    /// <summary>
    /// Constructor with initial minutes
    /// </summary>
    public TimeGuard(int initialMinutes) : this(initialMinutes, null)
    {
    }

    /// <summary>
    /// Constructor with initial minutes and custom state file
    /// </summary>
    public TimeGuard(int initialMinutes, string? customStateFile)
    {
        _initialMinutes = initialMinutes;
        _stateFile = customStateFile ?? Path.Combine(Path.GetTempPath(), "guard_state.bin");
        
        var encryption = new EncryptionService();
        _innerGuard = new Services.TimeGuard(encryption, _stateFile, _encryptionKey);
        
        // Initialize with minutes converted to milliseconds
        var initialMs = initialMinutes * 60000;
        _innerGuard.Initialize(initialMs);
    }

    /// <summary>
    /// Gets remaining time, ignoring the fake system time parameter
    /// (demonstrating that system time doesn't affect monotonic tracking)
    /// </summary>
    public TimeSpan GetRemainingTime(DateTime fakeSystemTime)
    {
        // The fakeSystemTime parameter is ignored because our implementation
        // uses monotonic time tracking that's immune to system clock changes
        return TimeSpan.FromMilliseconds(_innerGuard.RemainingBlockTimeMs);
    }

    /// <summary>
    /// Saves current state to disk
    /// </summary>
    public void SaveState()
    {
        // State is automatically saved by the inner guard's heartbeat timer
        // Force an update to ensure current state is saved
        _innerGuard.UpdateRemainingTime();
        // The Dispose/finalization will save the state
    }

    public void Dispose()
    {
        _innerGuard?.Dispose();
    }
}
