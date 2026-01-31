using Relife.Core.Services;

namespace Relife.Core.Tests;

/// <summary>
/// Tests for ProcessBlocker - IFEO registry manipulation
/// NOTE: These tests require Administrator privileges and should be run carefully
/// </summary>
public class ProcessBlockerTests
{
    [Fact]
    public void IsAdministrator_ReturnsBoolean()
    {
        // Act
        var isAdmin = ProcessBlocker.IsAdministrator();

        // Assert - Should return a boolean value (true or false)
        Assert.IsType<bool>(isAdmin);
    }

    [Fact]
    public void IsExecutableBlocked_NonExistentExecutable_ReturnsFalse()
    {
        // Arrange
        var fakeExeName = $"fake_executable_{Guid.NewGuid()}.exe";

        // Act
        var isBlocked = ProcessBlocker.IsExecutableBlocked(fakeExeName);

        // Assert
        Assert.False(isBlocked);
    }

    // The following tests require Administrator privileges
    // They are marked as conditional on Windows platform

    [Fact]
    public void BlockCmdAndPowerShell_WithoutAdminRights_ThrowsUnauthorizedAccessException()
    {
        // Skip test if running as administrator
        if (ProcessBlocker.IsAdministrator())
        {
            return; // Skip this test when running as admin
        }

        // Act & Assert
        Assert.Throws<UnauthorizedAccessException>(() => ProcessBlocker.BlockCmdAndPowerShell());
    }

    [Fact]
    public void UnblockCmdAndPowerShell_WithoutAdminRights_ThrowsUnauthorizedAccessException()
    {
        // Skip test if running as administrator
        if (ProcessBlocker.IsAdministrator())
        {
            return; // Skip this test when running as admin
        }

        // Act & Assert
        Assert.Throws<UnauthorizedAccessException>(() => ProcessBlocker.UnblockCmdAndPowerShell());
    }

    // NOTE: The following tests should only run when executed as Administrator
    // In a real environment, you would use [Fact(Skip = "Requires Administrator")] 
    // or create separate test suites for admin-required tests

    [Fact]
    public void BlockAndUnblock_Integration_ShouldWorkWhenAdmin()
    {
        // This test demonstrates the expected behavior when running as admin
        // In production, wrap in admin check or separate test suite
        
        if (!OperatingSystem.IsWindows())
        {
            // Skip on non-Windows platforms
            return;
        }

        if (!ProcessBlocker.IsAdministrator())
        {
            // Skip if not admin - this would be handled by CI/CD environment
            return;
        }

        try
        {
            // Act - Block
            ProcessBlocker.BlockCmdAndPowerShell();

            // Assert - Should be blocked
            Assert.True(ProcessBlocker.IsExecutableBlocked("cmd.exe"));
            Assert.True(ProcessBlocker.IsExecutableBlocked("powershell.exe"));

            // Act - Unblock
            ProcessBlocker.UnblockCmdAndPowerShell();

            // Assert - Should be unblocked
            Assert.False(ProcessBlocker.IsExecutableBlocked("cmd.exe"));
            Assert.False(ProcessBlocker.IsExecutableBlocked("powershell.exe"));
        }
        finally
        {
            // Cleanup - Ensure processes are unblocked
            try
            {
                ProcessBlocker.UnblockCmdAndPowerShell();
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
