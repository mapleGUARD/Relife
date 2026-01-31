using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Principal;

namespace Relife.Core.Services;

/// <summary>
/// Enforces process execution control by hijacking target processes via IFEO debugger keys.
/// This redirects blocked processes to launch Relife.exe instead.
/// </summary>
public class RegistryEnforcer
{
    private const string IFEO_PATH = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
    private readonly string _relifeExecutablePath;
    
    /// <summary>
    /// Processes that should be hijacked when enforcement is enabled
    /// </summary>
    public static readonly string[] TargetProcesses = 
    {
        "cmd.exe",
        "powershell.exe",
        "Taskmgr.exe"
    };

    /// <summary>
    /// Creates a new RegistryEnforcer instance
    /// </summary>
    /// <param name="relifeExecutablePath">Full path to Relife.exe. If null, uses current process path.</param>
    public RegistryEnforcer(string? relifeExecutablePath = null)
    {
        _relifeExecutablePath = relifeExecutablePath ?? GetCurrentExecutablePath();
    }

    /// <summary>
    /// Checks if the current process is running with Administrator privileges
    /// </summary>
    public static bool IsRunningAsAdmin()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sets or removes IFEO hijack for a specific process
    /// </summary>
    /// <param name="processName">Name of the process (e.g., "cmd.exe")</param>
    /// <param name="enabled">True to enable hijack, false to remove it</param>
    /// <exception cref="UnauthorizedAccessException">Thrown when not running as Administrator</exception>
    /// <exception cref="PlatformNotSupportedException">Thrown when not running on Windows</exception>
    public void SetHijack(string processName, bool enabled)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Registry enforcement is only supported on Windows.");
        }

        if (!IsRunningAsAdmin())
        {
            throw new UnauthorizedAccessException(
                "Administrator privileges required. Please restart the application as Administrator to modify registry enforcement settings.");
        }

        var processKeyPath = $@"{IFEO_PATH}\{processName}";

        if (enabled)
        {
            EnableHijack(processKeyPath);
        }
        else
        {
            DisableHijack(processKeyPath);
        }
    }

    /// <summary>
    /// Enables hijacking for all target processes
    /// </summary>
    public void EnableAllHijacks()
    {
        foreach (var process in TargetProcesses)
        {
            SetHijack(process, true);
        }
    }

    /// <summary>
    /// Disables hijacking for all target processes
    /// </summary>
    public void DisableAllHijacks()
    {
        foreach (var process in TargetProcesses)
        {
            SetHijack(process, false);
        }
    }

    /// <summary>
    /// Checks if a specific process has hijacking enabled
    /// </summary>
    /// <param name="processName">Name of the process to check</param>
    /// <returns>True if hijack is active, false otherwise</returns>
    public bool IsHijackEnabled(string processName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var processKeyPath = $@"{IFEO_PATH}\{processName}";
            using var key = Registry.LocalMachine.OpenSubKey(processKeyPath);
            
            if (key == null)
            {
                return false;
            }

            var debuggerValue = key.GetValue("Debugger") as string;
            return !string.IsNullOrEmpty(debuggerValue);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the debugger path currently set for a process, if any
    /// </summary>
    /// <param name="processName">Name of the process to check</param>
    /// <returns>The debugger path, or null if not set</returns>
    public string? GetHijackDebuggerPath(string processName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var processKeyPath = $@"{IFEO_PATH}\{processName}";
            using var key = Registry.LocalMachine.OpenSubKey(processKeyPath);
            return key?.GetValue("Debugger") as string;
        }
        catch
        {
            return null;
        }
    }

    private void EnableHijack(string processKeyPath)
    {
        try
        {
            // Open or create the IFEO key for the target process
            using var key = Registry.LocalMachine.CreateSubKey(processKeyPath, writable: true);
            
            if (key == null)
            {
                throw new InvalidOperationException($"Failed to create registry key: {processKeyPath}");
            }

            // Set the Debugger value to our executable path
            // When Windows tries to launch the process, it will launch our app instead
            key.SetValue("Debugger", _relifeExecutablePath, RegistryValueKind.String);
        }
        catch (UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException(
                "Access denied while writing to registry. Ensure you are running as Administrator.");
        }
    }

    private void DisableHijack(string processKeyPath)
    {
        try
        {
            using var ifeoKey = Registry.LocalMachine.OpenSubKey(IFEO_PATH, writable: true);
            
            if (ifeoKey == null)
            {
                return; // IFEO key doesn't exist, nothing to clean up
            }

            // Check if the process-specific key exists
            using var processKey = ifeoKey.OpenSubKey(Path.GetFileName(processKeyPath), writable: true);
            
            if (processKey != null)
            {
                // Remove the Debugger value
                try
                {
                    processKey.DeleteValue("Debugger", throwOnMissingValue: false);
                }
                catch
                {
                    // Ignore if value doesn't exist
                }

                // If the key is now empty, delete it entirely
                if (processKey.ValueCount == 0 && processKey.SubKeyCount == 0)
                {
                    processKey.Close();
                    ifeoKey.DeleteSubKey(Path.GetFileName(processKeyPath), throwOnMissingSubKey: false);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException(
                "Access denied while modifying registry. Ensure you are running as Administrator.");
        }
    }

    private static string GetCurrentExecutablePath()
    {
        // Try to get the main module path (works for compiled apps)
        var process = Process.GetCurrentProcess();
        var mainModule = process.MainModule;
        
        if (mainModule?.FileName != null)
        {
            return mainModule.FileName;
        }

        // Fallback to entry assembly location (works for .NET apps)
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        if (assembly?.Location != null && !string.IsNullOrEmpty(assembly.Location))
        {
            return assembly.Location;
        }

        // Last resort: use executing assembly
        return System.Reflection.Assembly.GetExecutingAssembly().Location;
    }
}
