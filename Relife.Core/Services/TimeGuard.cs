using System.Diagnostics;
using System.Text.Json;
using Relife.Core.Models;

namespace Relife.Core.Services;

/// <summary>
/// Tamper-proof time tracking service using monotonic clocks
/// </summary>
public class TimeGuard : IDisposable
{
    private readonly IEncryptionService _encryptionService;
    private readonly string _stateFilePath;
    private readonly string _encryptionKey;
    private readonly Timer _heartbeatTimer;
    
    private TimeGuardState _state;
    private long _sessionStartTimestamp;
    private bool _disposed;

    // Events
    public event EventHandler<TamperDetectedEventArgs>? TamperDetected;
    public event EventHandler<HeartbeatEventArgs>? HeartbeatSaved;

    /// <summary>
    /// Gets whether tamper has been detected
    /// </summary>
    public bool IsTampered => _state.TamperFlagged;

    /// <summary>
    /// Gets remaining block time in milliseconds
    /// </summary>
    public long RemainingBlockTimeMs => _state.RemainingBlockTimeMs;

    public TimeGuard(
        IEncryptionService encryptionService,
        string stateFilePath,
        string encryptionKey)
    {
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _stateFilePath = stateFilePath ?? throw new ArgumentNullException(nameof(stateFilePath));
        _encryptionKey = encryptionKey ?? throw new ArgumentNullException(nameof(encryptionKey));

        _state = new TimeGuardState();
        _sessionStartTimestamp = Stopwatch.GetTimestamp();

        // Initialize heartbeat timer (save every 10 seconds)
        _heartbeatTimer = new Timer(HeartbeatCallback, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Initializes the TimeGuard service and performs handshake validation
    /// </summary>
    /// <param name="initialBlockTimeMs">Initial block time if no saved state exists</param>
    public void Initialize(long initialBlockTimeMs = 0)
    {
        _sessionStartTimestamp = Stopwatch.GetTimestamp();

        if (File.Exists(_stateFilePath))
        {
            try
            {
                // Perform handshake check
                PerformHandshakeCheck();
            }
            catch (Exception ex)
            {
                // Corrupted state - default to full block mode
                HandleCorruptedState(ex, initialBlockTimeMs);
            }
        }
        else
        {
            // No saved state - initialize fresh
            InitializeFreshState(initialBlockTimeMs);
        }

        SaveState();
    }

    /// <summary>
    /// Sets a new block time (e.g., user starts a new focus session)
    /// </summary>
    public void SetBlockTime(long blockTimeMs)
    {
        if (_state.TamperFlagged)
        {
            throw new InvalidOperationException("Cannot set block time - tamper detected");
        }

        _state.RemainingBlockTimeMs = blockTimeMs;
        SaveState();
    }

    /// <summary>
    /// Gets current elapsed time in the current session (monotonic)
    /// </summary>
    public long GetElapsedSessionTimeMs()
    {
        var currentTimestamp = Stopwatch.GetTimestamp();
        var elapsedTicks = currentTimestamp - _sessionStartTimestamp;
        return (long)((elapsedTicks * 1000.0) / Stopwatch.Frequency);
    }

    /// <summary>
    /// Updates the remaining block time based on elapsed time
    /// </summary>
    public void UpdateRemainingTime()
    {
        if (_state.TamperFlagged)
        {
            // Don't reduce time if tamper detected
            return;
        }

        var elapsedMs = GetElapsedSessionTimeMs();
        _state.RemainingBlockTimeMs = Math.Max(0, _state.RemainingBlockTimeMs - elapsedMs);
        _sessionStartTimestamp = Stopwatch.GetTimestamp(); // Reset session start
    }

    private void PerformHandshakeCheck()
    {
        // Read encrypted state
        var encryptedData = File.ReadAllBytes(_stateFilePath);
        var decryptedData = _encryptionService.Decrypt(encryptedData, _encryptionKey);
        var json = System.Text.Encoding.UTF8.GetString(decryptedData);
        var savedState = JsonSerializer.Deserialize<TimeGuardState>(json);

        if (savedState == null)
        {
            throw new InvalidOperationException("Failed to deserialize state");
        }

        // Calculate elapsed time using monotonic clock
        var currentTimestamp = Stopwatch.GetTimestamp();
        var monotonicElapsedTicks = currentTimestamp - savedState.LastMonotonicTimestamp;
        var monotonicElapsedMs = (long)((monotonicElapsedTicks * 1000.0) / Stopwatch.Frequency);

        // Calculate elapsed time using system clock
        var currentSystemTime = DateTime.UtcNow.Ticks;
        var systemElapsedTicks = currentSystemTime - savedState.LastSystemTimeTicks;
        var systemElapsedMs = systemElapsedTicks / TimeSpan.TicksPerMillisecond;

        // Tolerance: 30 seconds (system clock can drift, especially in VMs/containers)
        const long toleranceMs = 30000;

        // Check if system time jump doesn't align with monotonic time
        var timeDifference = Math.Abs(systemElapsedMs - monotonicElapsedMs);

        if (timeDifference > toleranceMs)
        {
            // TAMPER DETECTED!
            savedState.TamperFlagged = true;
            
            var args = new TamperDetectedEventArgs
            {
                MonotonicElapsedMs = monotonicElapsedMs,
                SystemElapsedMs = systemElapsedMs,
                Difference = timeDifference
            };

            TamperDetected?.Invoke(this, args);
        }
        else
        {
            // Valid - reduce remaining time by monotonic elapsed time
            savedState.RemainingBlockTimeMs = Math.Max(0, savedState.RemainingBlockTimeMs - monotonicElapsedMs);
        }

        _state = savedState;
    }

    private void HandleCorruptedState(Exception ex, long initialBlockTimeMs)
    {
        // Default to full block mode
        _state = new TimeGuardState
        {
            RemainingBlockTimeMs = initialBlockTimeMs,
            TamperFlagged = true, // Flag as tampered due to corruption
            LastMonotonicTimestamp = Stopwatch.GetTimestamp(),
            LastSystemTimeTicks = DateTime.UtcNow.Ticks,
            TimestampFrequency = Stopwatch.Frequency,
            HeartbeatCount = 0
        };

        var args = new TamperDetectedEventArgs
        {
            MonotonicElapsedMs = 0,
            SystemElapsedMs = 0,
            Difference = 0,
            CorruptionException = ex
        };

        TamperDetected?.Invoke(this, args);
    }

    private void InitializeFreshState(long initialBlockTimeMs)
    {
        _state = new TimeGuardState
        {
            RemainingBlockTimeMs = initialBlockTimeMs,
            TamperFlagged = false,
            LastMonotonicTimestamp = Stopwatch.GetTimestamp(),
            LastSystemTimeTicks = DateTime.UtcNow.Ticks,
            TimestampFrequency = Stopwatch.Frequency,
            HeartbeatCount = 0
        };
    }

    private void HeartbeatCallback(object? state)
    {
        try
        {
            // Update remaining time before saving
            UpdateRemainingTime();
            SaveState();

            HeartbeatSaved?.Invoke(this, new HeartbeatEventArgs
            {
                RemainingTimeMs = _state.RemainingBlockTimeMs,
                HeartbeatCount = _state.HeartbeatCount
            });
        }
        catch (Exception ex)
        {
            // Log error but don't crash
            Debug.WriteLine($"Heartbeat error: {ex.Message}");
        }
    }

    private void SaveState()
    {
        // Update state with current timestamps
        _state.LastMonotonicTimestamp = Stopwatch.GetTimestamp();
        _state.LastSystemTimeTicks = DateTime.UtcNow.Ticks;
        _state.TimestampFrequency = Stopwatch.Frequency;
        _state.HeartbeatCount++;

        // Serialize to JSON
        var json = JsonSerializer.Serialize(_state, new JsonSerializerOptions
        {
            WriteIndented = false
        });
        var data = System.Text.Encoding.UTF8.GetBytes(json);

        // Encrypt
        var encryptedData = _encryptionService.Encrypt(data, _encryptionKey);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(_stateFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write with hidden attribute
        File.WriteAllBytes(_stateFilePath, encryptedData);
        File.SetAttributes(_stateFilePath, FileAttributes.Hidden | FileAttributes.System);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _heartbeatTimer?.Dispose();
        
        // Final save before shutdown
        try
        {
            UpdateRemainingTime();
            SaveState();
        }
        catch
        {
            // Best effort
        }

        _disposed = true;
    }
}

/// <summary>
/// Event arguments for tamper detection
/// </summary>
public class TamperDetectedEventArgs : EventArgs
{
    public long MonotonicElapsedMs { get; set; }
    public long SystemElapsedMs { get; set; }
    public long Difference { get; set; }
    public Exception? CorruptionException { get; set; }
}

/// <summary>
/// Event arguments for heartbeat events
/// </summary>
public class HeartbeatEventArgs : EventArgs
{
    public long RemainingTimeMs { get; set; }
    public long HeartbeatCount { get; set; }
}
