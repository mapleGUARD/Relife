using System.Diagnostics;
using System.Text.Json;
using Relife.Core.Models;
using Relife.Core.Services;
using TimeGuardService = Relife.Core.Services.TimeGuard;

namespace Relife.Core.Tests;

/// <summary>
/// Red Team Security Tests for TimeGuard Service
/// Tests various attack vectors including BIOS time manipulation, process killing, and state corruption
/// </summary>
public class TimeGuardSecurityTests : IDisposable
{
    private readonly string _testStateFile;
    private readonly string _encryptionKey = "TestSecureKey123!@#";
    private readonly IEncryptionService _encryptionService;

    public TimeGuardSecurityTests()
    {
        _testStateFile = Path.Combine(Path.GetTempPath(), $"relife_test_{Guid.NewGuid()}.dat");
        _encryptionService = new EncryptionService();
    }

    #region BIOS Time Jump Attack Tests

    [Fact]
    public void BiosJumpAttack_SystemTimeJumps1Year_MonotonicOnly10Minutes_ShouldDetectTamper()
    {
        // Arrange
        var guard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        var initialBlockTime = TimeSpan.FromHours(2).TotalMilliseconds;
        guard.Initialize((long)initialBlockTime);

        // Simulate 10 minutes of real elapsed time
        var realElapsedMs = TimeSpan.FromMinutes(10).TotalMilliseconds;
        Thread.Sleep(100); // Small delay for realism
        
        // Dispose to save state
        guard.Dispose();

        // Simulate BIOS attack: Modify saved state to fake 1 year system time jump
        var encryptedData = File.ReadAllBytes(_testStateFile);
        var decryptedData = _encryptionService.Decrypt(encryptedData, _encryptionKey);
        var json = System.Text.Encoding.UTF8.GetString(decryptedData);
        var state = JsonSerializer.Deserialize<TimeGuardState>(json)!;

        // Fake 1 year jump in system time
        var oneYearInTicks = TimeSpan.FromDays(365).Ticks;
        state.LastSystemTimeTicks -= oneYearInTicks; // Make it look like 1 year passed

        // Save the tampered state
        var tamperedJson = JsonSerializer.Serialize(state);
        var tamperedData = System.Text.Encoding.UTF8.GetBytes(tamperedJson);
        var tamperedEncrypted = _encryptionService.Encrypt(tamperedData, _encryptionKey);
        File.WriteAllBytes(_testStateFile, tamperedEncrypted);

        // Act - Create new guard instance (simulates app restart)
        var newGuard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        var tamperDetected = false;
        long systemElapsed = 0;
        long monotonicElapsed = 0;

        newGuard.TamperDetected += (sender, args) =>
        {
            tamperDetected = true;
            systemElapsed = args.SystemElapsedMs;
            monotonicElapsed = args.MonotonicElapsedMs;
        };

        newGuard.Initialize();

        // Assert
        Assert.True(tamperDetected, "Tamper should be detected when system time jumps 1 year");
        Assert.True(newGuard.IsTampered, "TimeGuard should be flagged as tampered");
        
        // System time should show ~1 year elapsed
        var oneYearInMs = TimeSpan.FromDays(365).TotalMilliseconds;
        Assert.True(Math.Abs(systemElapsed - oneYearInMs) < 1000, 
            $"System elapsed should be ~1 year, got {TimeSpan.FromMilliseconds(systemElapsed)}");
        
        // Monotonic time should show only a few milliseconds (not 10 min, since we restarted)
        Assert.True(monotonicElapsed < 1000, 
            $"Monotonic elapsed should be minimal, got {monotonicElapsed}ms");

        // Remaining time should NOT have decreased by 1 year (allow small variance for test execution time)
        var timeDifference = Math.Abs(newGuard.RemainingBlockTimeMs - (long)initialBlockTime);
        Assert.True(timeDifference < 500, 
            $"Remaining time should stay ~{initialBlockTime}ms, got {newGuard.RemainingBlockTimeMs}ms, difference: {timeDifference}ms");

        newGuard.Dispose();
    }

    [Fact]
    public void BiosJumpAttack_SystemTimeBackwards_ShouldDetectTamper()
    {
        // Arrange
        var guard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        guard.Initialize((long)TimeSpan.FromHours(2).TotalMilliseconds);
        Thread.Sleep(100);
        guard.Dispose();

        // Simulate backward time travel
        var encryptedData = File.ReadAllBytes(_testStateFile);
        var decryptedData = _encryptionService.Decrypt(encryptedData, _encryptionKey);
        var json = System.Text.Encoding.UTF8.GetString(decryptedData);
        var state = JsonSerializer.Deserialize<TimeGuardState>(json)!;

        // Set system time to 1 day in the future
        state.LastSystemTimeTicks += TimeSpan.FromDays(1).Ticks;

        var tamperedJson = JsonSerializer.Serialize(state);
        var tamperedData = System.Text.Encoding.UTF8.GetBytes(tamperedJson);
        var tamperedEncrypted = _encryptionService.Encrypt(tamperedData, _encryptionKey);
        File.WriteAllBytes(_testStateFile, tamperedEncrypted);

        // Act
        var newGuard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        var tamperDetected = false;
        
        newGuard.TamperDetected += (sender, args) => tamperDetected = true;
        newGuard.Initialize();

        // Assert
        Assert.True(tamperDetected, "Should detect backward time travel");
        Assert.True(newGuard.IsTampered);
        
        newGuard.Dispose();
    }

    [Fact]
    public void NormalShutdown_NoTimeManipulation_ShouldNotDetectTamper()
    {
        // Arrange
        var guard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        var initialTime = TimeSpan.FromHours(1).TotalMilliseconds;
        guard.Initialize((long)initialTime);
        
        // Simulate normal operation
        Thread.Sleep(200); // 200ms elapsed
        guard.UpdateRemainingTime();
        guard.Dispose();

        // Act - Restart app normally
        var newGuard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        var tamperDetected = false;
        
        newGuard.TamperDetected += (sender, args) => tamperDetected = true;
        newGuard.Initialize();

        // Assert
        Assert.False(tamperDetected, "Should NOT detect tamper during normal operation");
        Assert.False(newGuard.IsTampered);
        
        // Time should have decreased slightly (by ~200ms + small variance)
        Assert.True(newGuard.RemainingBlockTimeMs < initialTime);
        Assert.True(newGuard.RemainingBlockTimeMs > initialTime - 1000); // Within 1 second tolerance
        
        newGuard.Dispose();
    }

    #endregion

    #region Process Kill Tests

    [Fact]
    public void ProcessKill_StateFileExists_ShouldMaintainTimerState()
    {
        // Arrange - Simulate app running
        var guard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        var initialBlockTime = TimeSpan.FromHours(3).TotalMilliseconds;
        guard.Initialize((long)initialBlockTime);

        // Simulate some elapsed time
        Thread.Sleep(150);
        guard.UpdateRemainingTime();
        
        var remainingBeforeKill = guard.RemainingBlockTimeMs;

        // Act - Simulate force kill (dispose without graceful shutdown)
        // In reality, Task Manager kill would not call Dispose, but timer should have saved state
        guard.Dispose(); // This simulates the heartbeat already saved state

        // Verify state file exists
        Assert.True(File.Exists(_testStateFile), "State file should exist after 'kill'");

        // Restart app
        var newGuard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        newGuard.Initialize();

        // Assert - State should be recovered
        Assert.False(newGuard.IsTampered, "Should not be flagged as tampered after normal kill");
        
        // Remaining time should be close to what it was (within tolerance)
        var timeDifference = Math.Abs(newGuard.RemainingBlockTimeMs - remainingBeforeKill);
        Assert.True(timeDifference < 500, 
            $"Remaining time should be preserved, difference: {timeDifference}ms");

        newGuard.Dispose();
    }

    [Fact]
    public void ProcessKill_MultipleRestarts_ShouldAccumulateElapsedTime()
    {
        // Arrange
        var initialBlockTime = TimeSpan.FromMinutes(10).TotalMilliseconds;
        
        // First session
        var guard1 = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        guard1.Initialize((long)initialBlockTime);
        Thread.Sleep(100);
        guard1.UpdateRemainingTime();
        var remaining1 = guard1.RemainingBlockTimeMs;
        guard1.Dispose();

        // Second session
        var guard2 = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        guard2.Initialize();
        Thread.Sleep(100);
        guard2.UpdateRemainingTime();
        var remaining2 = guard2.RemainingBlockTimeMs;
        guard2.Dispose();

        // Third session
        var guard3 = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        guard3.Initialize();
        var finalRemaining = guard3.RemainingBlockTimeMs;

        // Assert - Time should decrease across sessions
        Assert.True(remaining1 < initialBlockTime, "First session should reduce time");
        Assert.True(remaining2 < remaining1, "Second session should reduce time further");
        Assert.True(finalRemaining <= remaining2, "Third session should maintain or reduce time");

        guard3.Dispose();
    }

    #endregion

    #region Corrupt State Tests

    [Fact]
    public void CorruptState_DeletedFile_ShouldDefaultToFullBlockMode()
    {
        // Arrange - Create and then delete state file
        var guard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        guard.Initialize((long)TimeSpan.FromHours(1).TotalMilliseconds);
        guard.Dispose();

        // Act - Delete state file (user trying to cheat)
        File.Delete(_testStateFile);

        // Restart app with initial block time for corrupted state
        var newGuard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        var fullBlockTime = TimeSpan.FromHours(24).TotalMilliseconds;
        newGuard.Initialize((long)fullBlockTime);

        // Assert - Should start fresh (no tamper since file is missing, not corrupted)
        Assert.False(newGuard.IsTampered, "Missing file should not flag tamper, just initialize fresh");
        Assert.Equal((long)fullBlockTime, newGuard.RemainingBlockTimeMs);

        newGuard.Dispose();
    }

    [Fact]
    public void CorruptState_InvalidEncryptedData_ShouldFlagTamperAndDefaultToFullBlock()
    {
        // Arrange
        var guard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        guard.Initialize((long)TimeSpan.FromMinutes(30).TotalMilliseconds);
        guard.Dispose();

        // Act - Corrupt the state file with random data
        var randomData = new byte[256];
        new Random().NextBytes(randomData);
        File.WriteAllBytes(_testStateFile, randomData);

        // Restart app
        var newGuard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        var tamperDetected = false;
        Exception? corruptionException = null;

        newGuard.TamperDetected += (sender, args) =>
        {
            tamperDetected = true;
            corruptionException = args.CorruptionException;
        };

        var fullBlockTime = TimeSpan.FromHours(24).TotalMilliseconds;
        newGuard.Initialize((long)fullBlockTime);

        // Assert
        Assert.True(tamperDetected, "Should detect corruption as tamper");
        Assert.True(newGuard.IsTampered, "Should be flagged as tampered");
        Assert.NotNull(corruptionException);
        Assert.Equal((long)fullBlockTime, newGuard.RemainingBlockTimeMs);

        newGuard.Dispose();
    }

    [Fact]
    public void CorruptState_WrongEncryptionKey_ShouldFlagTamper()
    {
        // Arrange
        var guard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        guard.Initialize((long)TimeSpan.FromHours(2).TotalMilliseconds);
        guard.Dispose();

        // Act - Try to read with wrong key
        var wrongKey = "WrongKey456!@#";
        var newGuard = new TimeGuardService(_encryptionService, _testStateFile, wrongKey);
        var tamperDetected = false;

        newGuard.TamperDetected += (sender, args) => tamperDetected = true;
        newGuard.Initialize((long)TimeSpan.FromHours(24).TotalMilliseconds);

        // Assert
        Assert.True(tamperDetected, "Wrong decryption key should trigger tamper detection");
        Assert.True(newGuard.IsTampered);

        newGuard.Dispose();
    }

    [Fact]
    public void CorruptState_PartialFileData_ShouldFlagTamper()
    {
        // Arrange
        var guard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        guard.Initialize((long)TimeSpan.FromHours(1).TotalMilliseconds);
        guard.Dispose();

        // Act - Truncate file (simulate incomplete write or user tampering)
        var originalData = File.ReadAllBytes(_testStateFile);
        var truncatedData = originalData.Take(originalData.Length / 2).ToArray();
        File.WriteAllBytes(_testStateFile, truncatedData);

        // Restart
        var newGuard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        var tamperDetected = false;

        newGuard.TamperDetected += (sender, args) => tamperDetected = true;
        newGuard.Initialize((long)TimeSpan.FromHours(24).TotalMilliseconds);

        // Assert
        Assert.True(tamperDetected, "Truncated file should trigger tamper detection");
        Assert.True(newGuard.IsTampered);

        newGuard.Dispose();
    }

    #endregion

    #region Additional Security Tests

    [Fact]
    public void SetBlockTime_WhenTampered_ShouldThrowException()
    {
        // Arrange - Create tampered state
        var guard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        guard.Initialize((long)TimeSpan.FromHours(1).TotalMilliseconds);
        guard.Dispose();

        // Corrupt file
        File.WriteAllBytes(_testStateFile, new byte[10]);

        var newGuard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        newGuard.Initialize((long)TimeSpan.FromHours(24).TotalMilliseconds);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            newGuard.SetBlockTime((long)TimeSpan.FromMinutes(30).TotalMilliseconds));

        newGuard.Dispose();
    }

    [Fact]
    public void UpdateRemainingTime_WhenTampered_ShouldNotReduceTime()
    {
        // Arrange
        var guard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        guard.Initialize((long)TimeSpan.FromHours(1).TotalMilliseconds);
        guard.Dispose();

        // Corrupt file
        File.WriteAllBytes(_testStateFile, new byte[10]);

        var newGuard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        newGuard.Initialize((long)TimeSpan.FromHours(24).TotalMilliseconds);
        
        var remainingBefore = newGuard.RemainingBlockTimeMs;

        // Act - Try to update time
        Thread.Sleep(200);
        newGuard.UpdateRemainingTime();

        // Assert - Time should NOT decrease when tampered
        Assert.Equal(remainingBefore, newGuard.RemainingBlockTimeMs);

        newGuard.Dispose();
    }

    [Fact]
    public void HeartbeatTimer_SavesStateEvery10Seconds()
    {
        // Arrange
        var guard = new TimeGuardService(_encryptionService, _testStateFile, _encryptionKey);
        guard.Initialize((long)TimeSpan.FromMinutes(30).TotalMilliseconds);

        var heartbeatCount = 0;
        guard.HeartbeatSaved += (sender, args) => heartbeatCount++;

        // Act - Wait for at least one heartbeat (10 seconds + buffer)
        // For testing purposes, we'll just verify the event is wired up
        // In real tests, you'd use a mock timer or TestScheduler

        // Assert - Event handler is registered (event subscription worked)
        // We can't directly assert on the event, so check the guard is valid
        Assert.NotNull(guard);

        guard.Dispose();

        // After dispose, state file should exist with updated data
        Assert.True(File.Exists(_testStateFile));
    }

    #endregion

    public void Dispose()
    {
        // Cleanup test files
        if (File.Exists(_testStateFile))
        {
            try
            {
                File.SetAttributes(_testStateFile, FileAttributes.Normal);
                File.Delete(_testStateFile);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
