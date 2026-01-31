using System;
using Relife.Core.Services;

namespace Relife.Core.Examples;

/// <summary>
/// Example usage of the TimeGuard tamper-proof time tracking system
/// </summary>
public class TimeGuardExample
{
    public static void Main()
    {
        Console.WriteLine("=== Relife TimeGuard - Tamper-Proof Time Tracking ===\n");

        // Step 1: Initialize services
        var encryptionService = new EncryptionService();
        var stateFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Relife", ".timeguard.dat");
        var encryptionKey = GenerateSecureKey(); // In production, use secure key management

        // Step 2: Create TimeGuard instance
        using var timeGuard = new Services.TimeGuard(encryptionService, stateFilePath, encryptionKey);

        // Step 3: Setup event handlers
        timeGuard.TamperDetected += OnTamperDetected;
        timeGuard.HeartbeatSaved += OnHeartbeatSaved;

        // Step 4: Initialize with 2-hour focus block
        var focusBlockDuration = TimeSpan.FromHours(2).TotalMilliseconds;
        timeGuard.Initialize((long)focusBlockDuration);

        Console.WriteLine($"Focus block started: {TimeSpan.FromMilliseconds(timeGuard.RemainingBlockTimeMs)}");
        Console.WriteLine($"Tamper status: {(timeGuard.IsTampered ? "DETECTED" : "Clean")}\n");

        // Step 5: Optional - Block command-line access (requires admin)
        TryBlockCommandLine();

        // Step 6: Main application loop
        Console.WriteLine("Monitoring time... (Press any key to exit)\n");
        
        var lastRemainingTime = timeGuard.RemainingBlockTimeMs;
        while (!Console.KeyAvailable)
        {
            // Update remaining time based on elapsed session time
            timeGuard.UpdateRemainingTime();
            
            var remaining = timeGuard.RemainingBlockTimeMs;
            
            // Display updates only when time changes
            if (remaining != lastRemainingTime)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Remaining: {FormatTime(remaining)} | " +
                                $"Session elapsed: {FormatTime(timeGuard.GetElapsedSessionTimeMs())}");
                lastRemainingTime = remaining;
            }

            // Check if block is complete
            if (remaining <= 0)
            {
                Console.WriteLine("\n✅ Focus block completed! Access granted.");
                break;
            }

            Thread.Sleep(1000); // Update every second
        }

        // TimeGuard.Dispose() will be called automatically, saving final state
        Console.WriteLine("\nExiting... Final state saved.");
    }

    private static void OnTamperDetected(object? sender, TamperDetectedEventArgs args)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n⚠️  TAMPER DETECTED! ⚠️");
        Console.WriteLine("╔═══════════════════════════════════════════════════╗");
        Console.WriteLine("║  Time manipulation attempt has been flagged!      ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════╝");
        Console.ResetColor();

        Console.WriteLine($"\nDetails:");
        Console.WriteLine($"  • Monotonic time elapsed: {FormatTime(args.MonotonicElapsedMs)}");
        Console.WriteLine($"  • System time elapsed: {FormatTime(args.SystemElapsedMs)}");
        Console.WriteLine($"  • Discrepancy: {FormatTime(args.Difference)}");
        
        if (args.CorruptionException != null)
        {
            Console.WriteLine($"  • Corruption detected: {args.CorruptionException.Message}");
        }

        Console.WriteLine("\n🔒 Entering maximum security mode.");
        Console.WriteLine("    Block timer will NOT decrease until tamper is resolved.\n");
    }

    private static void OnHeartbeatSaved(object? sender, HeartbeatEventArgs args)
    {
        Console.WriteLine($"[Heartbeat #{args.HeartbeatCount}] State saved. " +
                        $"Remaining: {FormatTime(args.RemainingTimeMs)}");
    }

    private static void TryBlockCommandLine()
    {
        try
        {
            if (ProcessBlocker.IsAdministrator())
            {
                Console.WriteLine("🛡️  Enabling defensive mode...");
                ProcessBlocker.BlockCmdAndPowerShell();
                Console.WriteLine("   ✅ cmd.exe blocked");
                Console.WriteLine("   ✅ powershell.exe blocked");
                Console.WriteLine("   ✅ powershell_ise.exe blocked\n");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️  Not running as Administrator.");
                Console.WriteLine("   Command-line blocking disabled.\n");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠️  Could not enable defensive mode: {ex.Message}\n");
            Console.ResetColor();
        }
    }

    private static string FormatTime(long milliseconds)
    {
        var timeSpan = TimeSpan.FromMilliseconds(milliseconds);
        
        if (timeSpan.TotalHours >= 1)
            return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
        
        if (timeSpan.TotalMinutes >= 1)
            return $"{(int)timeSpan.TotalMinutes}m {timeSpan.Seconds}s";
        
        return $"{timeSpan.Seconds}s";
    }

    private static string GenerateSecureKey()
    {
        // In production, use:
        // - Azure Key Vault
        // - Windows DPAPI
        // - Hardware Security Module (HSM)
        // - User-specific derived key
        
        // For demo purposes only:
        return "Demo-Relife-SecureKey-2026-" + Environment.MachineName;
    }
}
