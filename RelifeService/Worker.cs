using Relife.Core.Services;

namespace RelifeService;

/// <summary>
/// Background worker service that continuously enforces Relife policies.
/// Integrates TimeGuard for tamper-proof time tracking and RegistryEnforcer for process blocking.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly TimeGuard _timeGuard;
    private readonly RegistryEnforcer _registryEnforcer;
    private readonly string _stateFilePath;
    private bool _enforcementEnabled;

    public Worker(
        ILogger<Worker> logger,
        TimeGuard timeGuard,
        RegistryEnforcer registryEnforcer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeGuard = timeGuard ?? throw new ArgumentNullException(nameof(timeGuard));
        _registryEnforcer = registryEnforcer ?? throw new ArgumentNullException(nameof(registryEnforcer));
        
        // Store state in a secure location
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var relifeDataPath = Path.Combine(appDataPath, "Relife");
        Directory.CreateDirectory(relifeDataPath);
        _stateFilePath = Path.Combine(relifeDataPath, "enforcer.state");
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== Relife Enforcer Service Starting ===");
        _logger.LogInformation("State file: {StateFile}", _stateFilePath);

        // Subscribe to TimeGuard events
        _timeGuard.TamperDetected += OnTamperDetected;
        _timeGuard.HeartbeatSaved += OnHeartbeatSaved;

        // Initialize TimeGuard
        // Start with 24 hours of enforcement (in milliseconds) if no previous state exists
        const long initialBlockTimeMs = 24L * 60L * 60L * 1000L;
        _timeGuard.Initialize(initialBlockTimeMs);

        _logger.LogInformation("TimeGuard initialized. Remaining block time: {TimeMs}ms", 
            _timeGuard.RemainingBlockTimeMs);

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Relife Enforcer Worker executing...");

        // Main enforcement loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Update remaining time based on elapsed time
                _timeGuard.UpdateRemainingTime();

                var remainingTimeMs = _timeGuard.RemainingBlockTimeMs;
                var isTampered = _timeGuard.IsTampered;

                // Determine if enforcement should be active
                var shouldEnforce = remainingTimeMs > 0 || isTampered;

                // Apply or remove enforcement based on current state
                if (shouldEnforce && !_enforcementEnabled)
                {
                    EnableEnforcement();
                }
                else if (!shouldEnforce && _enforcementEnabled)
                {
                    DisableEnforcement();
                }

                // Log status periodically (every 60 seconds)
                if (DateTime.UtcNow.Second == 0)
                {
                    var remainingHours = TimeSpan.FromMilliseconds(remainingTimeMs).TotalHours;
                    _logger.LogInformation(
                        "Status - Remaining: {Hours:F2}h, Tampered: {Tampered}, Enforcement: {Enabled}",
                        remainingHours, isTampered, _enforcementEnabled);
                }

                // Check every second
                await Task.Delay(1000, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in enforcement loop");
                
                // On error, enable enforcement as a safety measure
                if (!_enforcementEnabled)
                {
                    try
                    {
                        EnableEnforcement();
                    }
                    catch (Exception enfEx)
                    {
                        _logger.LogError(enfEx, "Failed to enable enforcement after error");
                    }
                }

                // Wait before retrying
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning("=== Relife Enforcer Service Stopping ===");
        
        // Unsubscribe from events
        _timeGuard.TamperDetected -= OnTamperDetected;
        _timeGuard.HeartbeatSaved -= OnHeartbeatSaved;

        // Note: We intentionally DO NOT disable enforcement here
        // This ensures that even if the service is stopped, the registry hijacks remain active
        // The service will self-recover and continue enforcement
        _logger.LogWarning("Service stopping but enforcement remains ACTIVE (unkillable mode)");

        return base.StopAsync(cancellationToken);
    }

    private void EnableEnforcement()
    {
        _logger.LogWarning("ENABLING enforcement - blocking target processes");
        
        try
        {
            _registryEnforcer.EnableAllHijacks();
            _enforcementEnabled = true;
            _logger.LogInformation("Enforcement ENABLED successfully");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Failed to enable enforcement - Administrator privileges required");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable enforcement");
        }
    }

    private void DisableEnforcement()
    {
        _logger.LogInformation("DISABLING enforcement - unblocking target processes");
        
        try
        {
            _registryEnforcer.DisableAllHijacks();
            _enforcementEnabled = false;
            _logger.LogInformation("Enforcement DISABLED successfully");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Failed to disable enforcement - Administrator privileges required");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable enforcement");
        }
    }

    private void OnTamperDetected(object? sender, TamperDetectedEventArgs e)
    {
        _logger.LogCritical(
            "🚨 TAMPER DETECTED! Monotonic: {MonotonicMs}ms, System: {SystemMs}ms, Diff: {DiffMs}ms",
            e.MonotonicElapsedMs, e.SystemElapsedMs, e.Difference);
        
        if (e.CorruptionException != null)
        {
            _logger.LogCritical(e.CorruptionException, "State file corruption detected");
        }

        // Force enable enforcement on tamper detection
        if (!_enforcementEnabled)
        {
            EnableEnforcement();
        }
    }

    private void OnHeartbeatSaved(object? sender, HeartbeatEventArgs e)
    {
        // Verbose logging - can be reduced in production
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Heartbeat #{Count} saved", e.HeartbeatCount);
        }
    }
}
