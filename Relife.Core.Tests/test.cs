using Xunit;
using Relife.Core;

public class TimeGuardTests : IDisposable
{
    private readonly string _testStateFile;

    public TimeGuardTests()
    {
        // Use unique file for each test instance
        _testStateFile = Path.Combine(Path.GetTempPath(), $"guard_state_{Guid.NewGuid()}.bin");
    }

    [Fact] 
    public void Test_SystemClockJump_DoesNotAffectRemainingTime()
    {
        // ARRANGE: Set a 1-hour block.
        using var guard = new TimeGuard(initialMinutes: 60);
        
        // ACT: Simulate the user changing the System Clock forward by 10 years.
        var fakeSystemTime = DateTime.Now.AddYears(10);
        var remaining = guard.GetRemainingTime(fakeSystemTime);

        // ASSERT: The remaining time should STILL be 60 mins because the 
        // Monotonic clock (TickCount) hasn't moved.
        Assert.Equal(60, remaining.TotalMinutes, precision: 0);
    }

    [Fact]
    public void Test_CorruptedState_TriggersLockdown()
    {
        // ARRANGE: Corrupt the encrypted state file manually.
        var corruptedFile = Path.Combine(Path.GetTempPath(), "guard_state.bin");
        File.WriteAllText(corruptedFile, "JUNK_DATA_FOR_HACKING");

        try
        {
            // ACT: Try to initialize the guard.
            using var guard = new TimeGuard();

            // ASSERT: It should default to "Maximum Security" (Full Block) 
            // rather than 0 minutes.
            Assert.True(guard.IsLockedDown);
        }
        finally
        {
            // Cleanup
            if (File.Exists(corruptedFile))
            {
                File.SetAttributes(corruptedFile, FileAttributes.Normal);
                File.Delete(corruptedFile);
            }
        }
    }

    [Fact]
    public void Test_Persistence_AfterProcessKill()
    {
        var stateFile = Path.Combine(Path.GetTempPath(), "guard_state_persist.bin");
        
        try
        {
            // ARRANGE: Start a 30-min block and save state.
            using (var guard = new TimeGuard(30, stateFile))
            {
                guard.SaveState();
                // Dispose saves the state
            }

            // Small delay to ensure file is written
            Thread.Sleep(100);

            // ACT: Simulate a new instance (as if the app was killed and restarted).
            using var newGuard = new TimeGuard(0, stateFile);

            // ASSERT: The new instance must remember approximately 30 minutes.
            // Allow small variance due to elapsed time during test
            Assert.True(newGuard.RemainingMinutes >= 29 && newGuard.RemainingMinutes <= 30,
                $"Expected ~30 minutes, got {newGuard.RemainingMinutes}");
        }
        finally
        {
            // Cleanup
            if (File.Exists(stateFile))
            {
                File.SetAttributes(stateFile, FileAttributes.Normal);
                File.Delete(stateFile);
            }
        }
    }

    public void Dispose()
    {
        // Cleanup any test files
        if (File.Exists(_testStateFile))
        {
            try
            {
                File.SetAttributes(_testStateFile, FileAttributes.Normal);
                File.Delete(_testStateFile);
            }
            catch { }
        }
    }
}
