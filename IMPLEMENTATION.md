# Relife - Tamper-Proof Time Tracking System

## Overview

**Relife** is an extreme productivity application with advanced security features designed to prevent time-traveling exploits where users manipulate BIOS/System clocks to bypass time-based restrictions.

## Architecture

### Core Components

#### 1. **TimeGuard Service** ([TimeGuard.cs](Relife.Core/Services/TimeGuard.cs))

The heart of the anti-tamper system. Key features:

- **Monotonic Time Tracking**: Uses `Stopwatch.GetTimestamp()` for high-resolution, tamper-proof time measurement
- **Persistent State**: Saves encrypted state every 10 seconds with AES-256 encryption
- **Handshake Validation**: On startup, compares monotonic elapsed time vs. system clock elapsed time
- **Tamper Detection**: Flags discrepancies between monotonic and system time (>5 second tolerance)
- **Defensive Mode**: Refuses to reduce block timer when tamper is detected

#### 2. **EncryptionService** ([EncryptionService.cs](Relife.Core/Services/EncryptionService.cs))

Secure AES-256 encryption for state persistence:

- **AES-256-CBC**: Industry-standard encryption
- **Random IV**: Each encryption uses a unique Initialization Vector
- **SHA-256 Key Derivation**: Converts passphrase to 256-bit key
- **IV Prepending**: IV stored with ciphertext for decryption

#### 3. **ProcessBlocker** ([ProcessBlocker.cs](Relife.Core/Services/ProcessBlocker.cs))

Defensive execution via IFEO (Image File Execution Options):

- **Registry Manipulation**: Blocks `cmd.exe`, `powershell.exe`, `powershell_ise.exe`
- **Admin Elevation**: Detects and requires administrator privileges
- **Graceful Handling**: Clear error messages for permission issues

## Security Model

### Attack Vector 1: BIOS Time Manipulation

**Scenario**: User changes system clock forward 1 year to bypass a 2-hour block timer.

**Defense**:
1. TimeGuard tracks elapsed time using `Stopwatch.GetTimestamp()` (monotonic, hardware-based)
2. On restart, compares:
   - Monotonic elapsed: ~10ms (app just restarted)
   - System time elapsed: ~365 days
3. Detects massive discrepancy → Flags tamper
4. **Result**: Block timer remains at original value

**Test**: `BiosJumpAttack_SystemTimeJumps1Year_MonotonicOnly10Minutes_ShouldDetectTamper()`

### Attack Vector 2: Process Kill

**Scenario**: User kills app via Task Manager to reset timer.

**Defense**:
1. Heartbeat timer saves state every 10 seconds
2. State includes remaining block time + monotonic timestamp
3. On restart, resumes from last saved state
4. **Result**: Timer continues from where it left off (±10 second variance)

**Test**: `ProcessKill_StateFileExists_ShouldMaintainTimerState()`

### Attack Vector 3: State File Corruption

**Scenario**: User deletes or corrupts the encrypted state file.

**Defense**:
1. On startup, attempts to decrypt state file
2. If decryption fails → Flags tamper
3. Defaults to "Full Block Mode" (maximum restriction)
4. **Result**: Corruption makes situation worse for attacker

**Tests**: 
- `CorruptState_DeletedFile_ShouldDefaultToFullBlockMode()`
- `CorruptState_InvalidEncryptedData_ShouldFlagTamperAndDefaultToFullBlock()`

## Implementation Details

### Monotonic Clock Strategy

```csharp
// Get current high-resolution timestamp (monotonic)
var timestamp = Stopwatch.GetTimestamp();

// Calculate elapsed milliseconds
var elapsedTicks = currentTimestamp - savedTimestamp;
var elapsedMs = (long)((elapsedTicks * 1000.0) / Stopwatch.Frequency);
```

**Why it works**: `Stopwatch.GetTimestamp()` uses hardware performance counters that are immune to system clock changes.

### Handshake Validation

```csharp
// Calculate both elapsed times
var monotonicElapsedMs = /* from Stopwatch timestamps */;
var systemElapsedMs = /* from DateTime.UtcNow */;

// Check for discrepancy
var timeDifference = Math.Abs(systemElapsedMs - monotonicElapsedMs);

if (timeDifference > 5000) // 5 second tolerance
{
    // TAMPER DETECTED!
    state.TamperFlagged = true;
    TamperDetected?.Invoke(this, eventArgs);
}
```

### State Persistence Format

```json
{
  "RemainingBlockTimeMs": 7200000,
  "LastMonotonicTimestamp": 123456789012,
  "LastSystemTimeTicks": 638412345678901234,
  "TimestampFrequency": 10000000,
  "TamperFlagged": false,
  "HeartbeatCount": 42
}
```

Encrypted with AES-256-CBC, saved to hidden file with system attributes.

## Usage Example

```csharp
// Initialize
var encryption = new EncryptionService();
var guard = new TimeGuard(
    encryption, 
    "C:\\ProgramData\\Relife\\.state", 
    "YourSecureKey123!");

// Handle tamper events
guard.TamperDetected += (sender, args) => 
{
    Console.WriteLine($"TAMPER DETECTED!");
    Console.WriteLine($"Monotonic elapsed: {args.MonotonicElapsedMs}ms");
    Console.WriteLine($"System elapsed: {args.SystemElapsedMs}ms");
    Console.WriteLine($"Difference: {args.Difference}ms");
    
    // Lock down the application
    ShowWarningToUser();
};

// Initialize with 2-hour block time
guard.Initialize(TimeSpan.FromHours(2).TotalMilliseconds);

// Block dangerous executables (requires admin)
if (ProcessBlocker.IsAdministrator())
{
    ProcessBlocker.BlockCmdAndPowerShell();
}

// During app lifecycle
while (appRunning)
{
    // Check remaining time
    var remaining = guard.RemainingBlockTimeMs;
    
    if (remaining <= 0)
    {
        // Block expired - allow access
        break;
    }
    
    Thread.Sleep(1000);
}

// Cleanup
guard.Dispose(); // Saves final state
```

## Testing Suite

### Red Team Tests ([TimeGuardSecurityTests.cs](Relife.Core.Tests/TimeGuardSecurityTests.cs))

**BIOS Jump Attack Tests**:
- ✅ 1 year system time jump vs. 10 minutes monotonic
- ✅ Backward time travel detection
- ✅ Normal operation without false positives

**Process Kill Tests**:
- ✅ State persistence after force kill
- ✅ Multiple restart accumulation
- ✅ Recovery from ungraceful shutdown

**Corrupt State Tests**:
- ✅ Deleted state file handling
- ✅ Invalid encrypted data
- ✅ Wrong decryption key
- ✅ Truncated/partial file data

**Additional Security Tests**:
- ✅ SetBlockTime rejection when tampered
- ✅ UpdateRemainingTime freeze when tampered
- ✅ Heartbeat timer validation

### Run Tests

```bash
# Run all tests
dotnet test Relife.Core.Tests/Relife.Core.Tests.csproj

# Run specific test category
dotnet test --filter "FullyQualifiedName~BiosJumpAttack"

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"
```

### Test Coverage

- **Encryption**: 8 tests covering AES-256 operations
- **TimeGuard Security**: 12+ red team attack scenarios
- **ProcessBlocker**: 5 tests (some require admin)

## Security Considerations

### Strengths

✅ **Monotonic Clock**: Cannot be manipulated by system clock changes  
✅ **Encrypted State**: AES-256 with unique IVs prevents tampering  
✅ **Handshake Validation**: Multi-factor time verification  
✅ **Defensive Default**: Corruption = more restriction, not less  
✅ **IFEO Blocking**: Prevents command-line exploits  

### Known Limitations

⚠️ **Hardware Debugging**: Advanced users with kernel debuggers could potentially bypass  
⚠️ **VM Time Manipulation**: Virtual machine hosts can manipulate time at hypervisor level  
⚠️ **Physical Access**: User with admin rights can disable service entirely  
⚠️ **Heartbeat Granularity**: 10-second save interval = potential ±10s exploit window  

### Recommended Mitigations

1. **Run as System Service**: Prevent user termination
2. **Kernel Driver**: Move time validation to kernel mode
3. **Remote Validation**: Periodic server-side timestamp verification
4. **Hardware TPM**: Use Trusted Platform Module for state attestation
5. **Reduce Heartbeat**: Lower to 1-second intervals (trade performance)

## Requirements

- **.NET 10.0** or later
- **Windows OS** (for IFEO registry features)
- **Administrator privileges** (for ProcessBlocker)
- **xUnit** for testing
- **Moq** for mocking (test project)

## File Structure

```
Relife/
├── Relife.Core/
│   ├── Models/
│   │   └── TimeGuardState.cs
│   ├── Services/
│   │   ├── IEncryptionService.cs
│   │   ├── EncryptionService.cs
│   │   ├── TimeGuard.cs
│   │   └── ProcessBlocker.cs
│   └── Relife.Core.csproj
├── Relife.Core.Tests/
│   ├── TimeGuardSecurityTests.cs
│   ├── EncryptionServiceTests.cs
│   ├── ProcessBlockerTests.cs
│   └── Relife.Core.Tests.csproj
└── README.md
```

## License

See [LICENSE](LICENSE) file.

## Contributing

This is a security-focused project. If you discover vulnerabilities, please report them responsibly.

### Red Team Contributions Welcome

We actively encourage security researchers to:
1. Find and report bypasses
2. Submit attack scenario tests
3. Propose stronger defensive mechanisms

---

**Built for extreme productivity. Secured against extreme manipulation.**
