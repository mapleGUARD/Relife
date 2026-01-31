using Relife.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Relife.Core;

class Program
{
    static void Main(string[] args)
    {
        // Check if running as Windows Service
        var isWindowsService = OperatingSystem.IsWindows() && WindowsServiceHelpers.IsWindowsService();

        var builder = Host.CreateApplicationBuilder(args);

        // Configure as Windows Service if running as one
        if (isWindowsService)
        {
#pragma warning disable CA1416 // Validate platform compatibility
            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = "RelifeEnforcer";
            });
#pragma warning restore CA1416
        }

        // Configure logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        
        if (OperatingSystem.IsWindows())
        {
#pragma warning disable CA1416 // Validate platform compatibility
            builder.Logging.AddEventLog(settings =>
            {
                settings.SourceName = "RelifeEnforcer";
            });
#pragma warning restore CA1416
        }

        // Register services
        builder.Services.AddSingleton<IEncryptionService, EncryptionService>();

        builder.Services.AddSingleton<Services.TimeGuard>(provider =>
        {
            var encryptionService = provider.GetRequiredService<IEncryptionService>();
            
            // State file location
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var relifeDataPath = Path.Combine(appDataPath, "Relife");
            Directory.CreateDirectory(relifeDataPath);
            var stateFilePath = Path.Combine(relifeDataPath, "timeguard.state");
            
            // Encryption key - In production, this should come from secure configuration
            // For now, using a hardcoded key (you should replace this with a secure key management solution)
            var encryptionKey = "RelifeEnforcer_SecureKey_2026_ChangeMe!";
            
            return new Services.TimeGuard(encryptionService, stateFilePath, encryptionKey);
        });

        builder.Services.AddSingleton<RegistryEnforcer>(provider =>
        {
            // The RegistryEnforcer will use the current executable path
            // When running as a service, this will be the service executable
            return new RegistryEnforcer();
        });

        builder.Services.AddHostedService<RelifeWorker>();

        var host = builder.Build();

        // Handle command-line arguments for service installation/configuration
        if (args.Length > 0)
        {
            var command = args[0].ToLowerInvariant();
            
            switch (command)
            {
                case "install":
                    Console.WriteLine("To install the service, run:");
                    Console.WriteLine("  sc create RelifeEnforcer binPath=\"{path-to-exe}\" start=auto");
                    Console.WriteLine("\nThen configure recovery settings:");
                    Console.WriteLine("  {path-to-exe} configure-recovery");
                    return;
                    
                case "configure-recovery":
                    try
                    {
                        RecoveryManager.ConfigureUnkillableService("RelifeEnforcer");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                        Console.WriteLine("Make sure you are running as Administrator and the service is installed.");
                        return;
                    }
                    return;
                    
                case "show-recovery":
                    RecoveryManager.DisplayRecoveryConfiguration("RelifeEnforcer");
                    return;
                    
                case "help":
                case "--help":
                case "-h":
                    Console.WriteLine("Relife Enforcer Service");
                    Console.WriteLine("=======================");
                    Console.WriteLine("\nCommands:");
                    Console.WriteLine("  (no args)           - Run the service (or start normally if installed)");
                    Console.WriteLine("  install             - Show installation instructions");
                    Console.WriteLine("  configure-recovery  - Configure service recovery (unkillable mode)");
                    Console.WriteLine("  show-recovery       - Display current recovery configuration");
                    Console.WriteLine("  help                - Show this help message");
                    Console.WriteLine("\nInstallation Steps:");
                    Console.WriteLine("1. Build the project: dotnet publish -c Release");
                    Console.WriteLine("2. Run as Administrator and install:");
                    Console.WriteLine("   sc create RelifeEnforcer binPath=\"C:\\path\\to\\Relife.Core.exe\"");
                    Console.WriteLine("3. Configure recovery (as Administrator):");
                    Console.WriteLine("   Relife.Core.exe configure-recovery");
                    Console.WriteLine("4. Start the service:");
                    Console.WriteLine("   sc start RelifeEnforcer");
                    return;
                    
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    Console.WriteLine("Run with 'help' for usage information.");
                    return;
            }
        }

        // Run the service
        host.Run();
    }
}
