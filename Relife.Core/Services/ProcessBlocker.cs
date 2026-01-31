using Microsoft.Win32;
using System.Security.Principal;

namespace Relife.Core.Services;

/// <summary>
/// Service for blocking executables using Image File Execution Options (IFEO)
/// </summary>
public class ProcessBlocker
{
    private const string IfeoRegistryPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";

    /// <summary>
    /// Blocks cmd.exe and powershell.exe using IFEO registry keys
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Thrown when not running as administrator</exception>
    public static void BlockCmdAndPowerShell()
    {
        EnsureAdministrator();

        BlockExecutable("cmd.exe");
        BlockExecutable("powershell.exe");
        BlockExecutable("powershell_ise.exe");
    }

    /// <summary>
    /// Unblocks cmd.exe and powershell.exe
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Thrown when not running as administrator</exception>
    public static void UnblockCmdAndPowerShell()
    {
        EnsureAdministrator();

        UnblockExecutable("cmd.exe");
        UnblockExecutable("powershell.exe");
        UnblockExecutable("powershell_ise.exe");
    }

    /// <summary>
    /// Blocks a specific executable using IFEO
    /// </summary>
    private static void BlockExecutable(string executableName)
    {
        try
        {
            using var baseKey = Registry.LocalMachine.OpenSubKey(IfeoRegistryPath, writable: true);
            if (baseKey == null)
            {
                throw new InvalidOperationException($"Cannot open registry key: {IfeoRegistryPath}");
            }

            // Create subkey for the executable
            using var execKey = baseKey.CreateSubKey(executableName);
            if (execKey == null)
            {
                throw new InvalidOperationException($"Cannot create registry key for: {executableName}");
            }

            // Set debugger to a non-existent path to prevent execution
            execKey.SetValue("Debugger", "relife_blocked.exe", RegistryValueKind.String);
        }
        catch (UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException(
                $"Access denied when blocking {executableName}. Administrator privileges required.");
        }
    }

    /// <summary>
    /// Unblocks a specific executable by removing IFEO entry
    /// </summary>
    private static void UnblockExecutable(string executableName)
    {
        try
        {
            using var baseKey = Registry.LocalMachine.OpenSubKey(IfeoRegistryPath, writable: true);
            if (baseKey == null)
            {
                return; // Registry key doesn't exist
            }

            // Delete the subkey if it exists
            baseKey.DeleteSubKey(executableName, throwOnMissingSubKey: false);
        }
        catch (UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException(
                $"Access denied when unblocking {executableName}. Administrator privileges required.");
        }
    }

    /// <summary>
    /// Checks if the current process is running with administrator privileges
    /// </summary>
    public static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Ensures the process is running as administrator
    /// </summary>
    private static void EnsureAdministrator()
    {
        if (!IsAdministrator())
        {
            throw new UnauthorizedAccessException(
                "This operation requires administrator privileges. Please run the application as administrator.");
        }
    }

    /// <summary>
    /// Checks if an executable is currently blocked
    /// </summary>
    public static bool IsExecutableBlocked(string executableName)
    {
        try
        {
            using var baseKey = Registry.LocalMachine.OpenSubKey(IfeoRegistryPath, writable: false);
            if (baseKey == null)
            {
                return false;
            }

            using var execKey = baseKey.OpenSubKey(executableName);
            if (execKey == null)
            {
                return false;
            }

            var debugger = execKey.GetValue("Debugger") as string;
            return !string.IsNullOrEmpty(debugger);
        }
        catch
        {
            return false;
        }
    }
}
