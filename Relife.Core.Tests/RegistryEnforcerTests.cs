using Microsoft.Win32;
using Relife.Core.Services;
using Xunit;

namespace Relife.Core.Tests;

/// <summary>
/// Tests for RegistryEnforcer - IFEO hijacking functionality
/// WARNING: These tests modify HKEY_LOCAL_MACHINE and require Administrator privileges
/// </summary>
public class RegistryEnforcerTests : IDisposable
{
    private const string TEST_PROCESS = "test_relife_hijack.exe";
    private const string TEST_EXE_PATH = @"C:\TestPath\Relife.exe";
    private readonly RegistryEnforcer _enforcer;
    private readonly List<string> _testKeysToCleanup = new();

    public RegistryEnforcerTests()
    {
        _enforcer = new RegistryEnforcer(TEST_EXE_PATH);
        _testKeysToCleanup.Add(TEST_PROCESS);
    }

    [Fact]
    public void IsRunningAsAdmin_ShouldReturnBooleanValue()
    {
        // Act
        var isAdmin = RegistryEnforcer.IsRunningAsAdmin();

        // Assert - Should return true or false without throwing
        Assert.True(isAdmin == true || isAdmin == false);
    }

    [Fact]
    public void SetHijack_WithoutAdminRights_ShouldThrowUnauthorizedException()
    {
        // Arrange
        if (RegistryEnforcer.IsRunningAsAdmin())
        {
            // Skip test if running as admin
            return;
        }

        // Act & Assert
        var exception = Assert.Throws<UnauthorizedAccessException>(() => 
            _enforcer.SetHijack(TEST_PROCESS, true));
        
        Assert.Contains("Administrator", exception.Message);
        Assert.Contains("restart", exception.Message.ToLower());
    }

    [Fact]
    public void Test_Hijack_Writes_Correct_Debugger_Key()
    {
        // Arrange
        if (!RegistryEnforcer.IsRunningAsAdmin())
        {
            // Skip test - requires admin privileges
            return;
        }

        var testProcess = TEST_PROCESS;
        _testKeysToCleanup.Add(testProcess);

        try
        {
            // Act - Enable hijack
            _enforcer.SetHijack(testProcess, true);

            // Assert - Read the registry key back directly
            var ifeoPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
            var processKeyPath = $@"{ifeoPath}\{testProcess}";
            
            using var key = Registry.LocalMachine.OpenSubKey(processKeyPath);
            
            Assert.NotNull(key);
            
            var debuggerValue = key.GetValue("Debugger") as string;
            
            Assert.NotNull(debuggerValue);
            Assert.Equal(TEST_EXE_PATH, debuggerValue);
        }
        finally
        {
            // Cleanup - Always remove the test key
            CleanupTestKey(testProcess);
        }
    }

    [Fact]
    public void SetHijack_Enable_ThenDisable_ShouldRemoveKey()
    {
        // Arrange
        if (!RegistryEnforcer.IsRunningAsAdmin())
        {
            return;
        }

        var testProcess = TEST_PROCESS;

        try
        {
            // Act - Enable then disable
            _enforcer.SetHijack(testProcess, true);
            Assert.True(_enforcer.IsHijackEnabled(testProcess), "Hijack should be enabled");

            _enforcer.SetHijack(testProcess, false);

            // Assert - Key should be removed or Debugger value should be gone
            Assert.False(_enforcer.IsHijackEnabled(testProcess), "Hijack should be disabled");
            
            var debuggerPath = _enforcer.GetHijackDebuggerPath(testProcess);
            Assert.Null(debuggerPath);
        }
        finally
        {
            CleanupTestKey(testProcess);
        }
    }

    [Fact]
    public void IsHijackEnabled_WhenNotSet_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentProcess = "nonexistent_process_12345.exe";

        // Act
        var isEnabled = _enforcer.IsHijackEnabled(nonExistentProcess);

        // Assert
        Assert.False(isEnabled);
    }

    [Fact]
    public void EnableAllHijacks_ShouldSetAllTargetProcesses()
    {
        // Arrange
        if (!RegistryEnforcer.IsRunningAsAdmin())
        {
            return;
        }

        try
        {
            // Act
            _enforcer.EnableAllHijacks();

            // Assert - All target processes should have hijack enabled
            foreach (var process in RegistryEnforcer.TargetProcesses)
            {
                Assert.True(_enforcer.IsHijackEnabled(process), 
                    $"Hijack should be enabled for {process}");
                
                var debuggerPath = _enforcer.GetHijackDebuggerPath(process);
                Assert.Equal(TEST_EXE_PATH, debuggerPath);
            }
        }
        finally
        {
            // Cleanup all target processes
            _enforcer.DisableAllHijacks();
        }
    }

    [Fact]
    public void DisableAllHijacks_ShouldRemoveAllTargetProcesses()
    {
        // Arrange
        if (!RegistryEnforcer.IsRunningAsAdmin())
        {
            return;
        }

        try
        {
            // Setup - Enable all hijacks first
            _enforcer.EnableAllHijacks();

            // Act - Disable all
            _enforcer.DisableAllHijacks();

            // Assert - All should be disabled
            foreach (var process in RegistryEnforcer.TargetProcesses)
            {
                Assert.False(_enforcer.IsHijackEnabled(process), 
                    $"Hijack should be disabled for {process}");
            }
        }
        finally
        {
            // Extra cleanup
            _enforcer.DisableAllHijacks();
        }
    }

    [Fact]
    public void GetHijackDebuggerPath_WhenSet_ReturnsCorrectPath()
    {
        // Arrange
        if (!RegistryEnforcer.IsRunningAsAdmin())
        {
            return;
        }

        try
        {
            // Act
            _enforcer.SetHijack(TEST_PROCESS, true);
            var debuggerPath = _enforcer.GetHijackDebuggerPath(TEST_PROCESS);

            // Assert
            Assert.Equal(TEST_EXE_PATH, debuggerPath);
        }
        finally
        {
            CleanupTestKey(TEST_PROCESS);
        }
    }

    [Fact]
    public void TargetProcesses_ShouldContainExpectedProcesses()
    {
        // Assert
        Assert.Contains("cmd.exe", RegistryEnforcer.TargetProcesses);
        Assert.Contains("powershell.exe", RegistryEnforcer.TargetProcesses);
        Assert.Contains("Taskmgr.exe", RegistryEnforcer.TargetProcesses);
    }

    public void Dispose()
    {
        // Cleanup all test keys
        foreach (var processName in _testKeysToCleanup)
        {
            CleanupTestKey(processName);
        }

        // Extra safety: ensure all target processes are cleaned up
        if (RegistryEnforcer.IsRunningAsAdmin())
        {
            try
            {
                _enforcer.DisableAllHijacks();
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    private void CleanupTestKey(string processName)
    {
        if (!RegistryEnforcer.IsRunningAsAdmin())
        {
            return;
        }

        try
        {
            _enforcer.SetHijack(processName, false);
        }
        catch
        {
            // Best effort cleanup - try direct registry deletion
            try
            {
                var ifeoPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
                using var ifeoKey = Registry.LocalMachine.OpenSubKey(ifeoPath, writable: true);
                ifeoKey?.DeleteSubKey(processName, throwOnMissingSubKey: false);
            }
            catch
            {
                // Ignore cleanup failures
            }
        }
    }
}
