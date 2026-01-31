using System.Diagnostics;
using System.Security.Principal;

namespace RelifeService;

/// <summary>
/// Manages Windows Service recovery configuration to create "unkillable" behavior.
/// Uses the Windows SC (Service Control) utility to configure automatic restart on failure.
/// </summary>
public static class RecoveryManager
{
    /// <summary>
    /// Configures the service to automatically restart on failure, making it virtually "unkillable".
    /// Must be run with Administrator privileges after the service is installed.
    /// </summary>
    /// <param name="serviceName">The name of the Windows Service</param>
    /// <exception cref="UnauthorizedAccessException">Thrown when not running as Administrator</exception>
    /// <exception cref="PlatformNotSupportedException">Thrown when not running on Windows</exception>
    public static void ConfigureServiceRecovery(string serviceName)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Service recovery configuration is only supported on Windows.");
        }

        if (!IsRunningAsAdmin())
        {
            throw new UnauthorizedAccessException(
                "Administrator privileges required to configure service recovery settings.");
        }

        Console.WriteLine($"Configuring recovery settings for service: {serviceName}");

        // Configure service failure actions
        // reset= 86400 : Reset failure count after 24 hours (86400 seconds)
        // actions= restart/1000/restart/1000/restart/1000 : Restart after 1 second on 1st, 2nd, and subsequent failures
        var scCommand = $"failure \"{serviceName}\" reset= 86400 actions= restart/1000/restart/1000/restart/1000";
        
        ExecuteScCommand(scCommand, "Service recovery configured: Auto-restart on failure with 1-second delay");
    }

    /// <summary>
    /// Configures the service to start automatically with delayed start.
    /// This ensures the service gets priority during system boot even if resources are constrained.
    /// </summary>
    /// <param name="serviceName">The name of the Windows Service</param>
    /// <exception cref="UnauthorizedAccessException">Thrown when not running as Administrator</exception>
    /// <exception cref="PlatformNotSupportedException">Thrown when not running on Windows</exception>
    public static void ConfigureDelayedAutoStart(string serviceName)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Service configuration is only supported on Windows.");
        }

        if (!IsRunningAsAdmin())
        {
            throw new UnauthorizedAccessException(
                "Administrator privileges required to configure service startup settings.");
        }

        Console.WriteLine($"Configuring delayed auto-start for service: {serviceName}");

        // Set service to automatic start
        var scCommandAuto = $"config \"{serviceName}\" start= auto";
        ExecuteScCommand(scCommandAuto, "Service set to Automatic start");

        // Enable delayed start (requires automatic start to be set first)
        var scCommandDelayed = $"config \"{serviceName}\" start= delayed-auto";
        ExecuteScCommand(scCommandDelayed, "Service set to Automatic (Delayed Start)");
    }

    /// <summary>
    /// Applies all recommended recovery and startup configurations.
    /// This creates the "unkillable" service behavior.
    /// </summary>
    /// <param name="serviceName">The name of the Windows Service</param>
    public static void ConfigureUnkillableService(string serviceName)
    {
        Console.WriteLine("=== Configuring Unkillable Service ===");
        ConfigureServiceRecovery(serviceName);
        ConfigureDelayedAutoStart(serviceName);
        Console.WriteLine("=== Configuration Complete ===");
        Console.WriteLine($"Service '{serviceName}' is now configured for maximum resilience:");
        Console.WriteLine("  ✓ Auto-restart on failure (1 second delay)");
        Console.WriteLine("  ✓ Automatic (Delayed Start) for boot priority");
        Console.WriteLine("  ✓ Failure counter resets every 24 hours");
    }

    /// <summary>
    /// Displays the current recovery configuration for a service
    /// </summary>
    /// <param name="serviceName">The name of the Windows Service</param>
    public static void DisplayRecoveryConfiguration(string serviceName)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("Not supported on this platform.");
            return;
        }

        Console.WriteLine($"\n=== Recovery Configuration for '{serviceName}' ===");
        
        var scCommand = $"qfailure \"{serviceName}\"";
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = scCommand,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrWhiteSpace(output))
        {
            Console.WriteLine(output);
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            Console.WriteLine($"Error: {error}");
        }
    }

    /// <summary>
    /// Checks if the current process is running with Administrator privileges
    /// </summary>
    private static bool IsRunningAsAdmin()
    {
        try
        {
#pragma warning disable CA1416 // Validate platform compatibility
            using var identity = WindowsIdentity.GetCurrent();
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
            var principal = new WindowsPrincipal(identity);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning restore CA1416 // Validate platform compatibility
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Executes an SC command and validates the result
    /// </summary>
    private static void ExecuteScCommand(string arguments, string successMessage)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            Console.WriteLine($"  ✓ {successMessage}");
        }
        else
        {
            var errorMsg = $"Failed to execute SC command. Exit code: {process.ExitCode}\n" +
                          $"Output: {output}\n" +
                          $"Error: {error}";
            throw new InvalidOperationException(errorMsg);
        }
    }
}
