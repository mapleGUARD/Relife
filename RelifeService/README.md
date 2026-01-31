# Relife Enforcer Windows Service

An "unkillable" Windows Service that enforces time-based process blocking using tamper-proof time tracking.

## Architecture

### Components

1. **Worker Service** - Background service that continuously enforces policies
2. **TimeGuard** - Tamper-proof time tracking using monotonic clocks
3. **RegistryEnforcer** - Process blocking via IFEO registry hijacking
4. **RecoveryManager** - Self-healing configuration for unkillable behavior

### Key Features

✅ **Unkillable** - Auto-restarts within 1 second if killed  
✅ **Tamper-Proof** - Detects system clock manipulation  
✅ **Self-Healing** - Survives crashes and force terminations  
✅ **Boot Persistent** - Automatic startup with delayed start priority  
✅ **Process Blocking** - Hijacks cmd.exe, PowerShell, Task Manager via IFEO  

## How It Works

### 1. Service Recovery Configuration

The service uses Windows Service Control Manager (SCM) recovery actions:

```powershell
sc failure RelifeEnforcer reset=86400 actions=restart/1000/restart/1000/restart/1000
```

This configures:
- **Reset Period**: 86400 seconds (24 hours) - failure counter resets daily
- **Action 1**: Restart after 1000ms (1 second) on first failure
- **Action 2**: Restart after 1000ms on second failure  
- **Action 3**: Restart after 1000ms on all subsequent failures

### 2. Delayed Auto-Start

```powershell
sc config RelifeEnforcer start=delayed-auto
```

Ensures the service:
- Starts automatically on boot
- Gets priority even during resource-constrained boot scenarios
- Starts after critical system services (reducing boot contention)

### 3. TimeGuard (Tamper Detection)

Uses dual-clock validation:

**Monotonic Clock** (Stopwatch.GetTimestamp):
- Cannot be manipulated by changing system time
- Tracks actual elapsed time

**System Clock** (DateTime.UtcNow):
- Can be changed by user/admin
- Used for cross-validation

**Handshake Check:**
1. On startup, reads last saved state
2. Calculates elapsed time using both clocks
3. If difference > 30 seconds → **TAMPER DETECTED**
4. Tamper triggers permanent enforcement mode

### 4. Registry Enforcement (IFEO Hijacking)

IFEO (Image File Execution Options) is a Windows debugging feature:

```
HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\cmd.exe
  Debugger = "C:\RelifeEnforcer\RelifeService.exe"
```

When Windows tries to launch `cmd.exe`:
1. Windows checks IFEO registry
2. Sees "Debugger" value is set
3. Launches `RelifeService.exe` instead
4. Original process is blocked

This hijacks:
- cmd.exe (Command Prompt)
- powershell.exe (PowerShell)
- Taskmgr.exe (Task Manager)

## The "Unkillable" Loop

```
1. Service Running
   ↓
2. User kills process (taskkill, Task Manager, etc.)
   ↓
3. Windows SCM detects failure
   ↓
4. SCM waits 1000ms (1 second)
   ↓
5. SCM restarts service with new PID
   ↓
6. Service re-enables enforcement
   ↓
7. Back to step 1
```

**Result:** The service cannot be permanently terminated without:
- Administrator privileges AND
- Using `sc stop` or `sc delete` commands

Even then, registry hijacks may persist unless explicitly removed.

## Installation

See [INSTALLATION_GUIDE.md](INSTALLATION_GUIDE.md) for detailed steps.

**Quick Install:**
```powershell
# Build
dotnet publish -c Release -r win-x64

# Install (as Admin)
sc create RelifeEnforcer binPath="C:\path\to\RelifeService.exe"

# Configure unkillable mode
.\RelifeService.exe configure-recovery

# Start
sc start RelifeEnforcer
```

## Testing

See [TESTING_GUIDE.md](TESTING_GUIDE.md) for comprehensive test procedures.

**Quick Test:**
```powershell
# Kill the service
taskkill /F /IM RelifeService.exe

# Wait 2 seconds
timeout /t 2

# Verify it restarted
tasklist | findstr RelifeService
```

Expected: New PID appears within 1-2 seconds.

## Security Considerations

### Strengths
- ✅ Resistant to time manipulation
- ✅ Survives process termination attempts
- ✅ Encrypted state prevents tampering
- ✅ Automatic recovery on crashes

### Weaknesses
- ⚠️ Requires Administrator to install (one-time)
- ⚠️ Can be stopped via `sc stop` (requires Admin)
- ⚠️ Service can be disabled in Safe Mode
- ⚠️ Registry hijacks can be removed manually
- ⚠️ Encryption key is hardcoded (dev version)

### Mitigation Strategies (Production)

1. **Protect the Service:**
   - Set service ACLs to prevent unauthorized stops
   - Use Protected Process Light (PPL) if available
   - Monitor for service stop attempts

2. **Protect Registry Keys:**
   - Set IFEO registry ACLs to SYSTEM only
   - Monitor for registry key deletions

3. **Secure Encryption Key:**
   - Use Azure Key Vault or Windows DPAPI
   - Rotate keys periodically

4. **Safe Mode Protection:**
   - Configure service to start in Safe Mode
   - Add additional boot-time checks

## File Structure

```
RelifeService/
├── Program.cs              # Main entry point, DI configuration
├── Worker.cs               # Background service with enforcement loop
├── RecoveryManager.cs      # SC command wrapper for unkillable config
├── INSTALLATION_GUIDE.md   # Installation instructions
├── TESTING_GUIDE.md        # Test procedures
└── README.md               # This file
```

## Command Reference

```powershell
# Show help
.\RelifeService.exe help

# Configure recovery
.\RelifeService.exe configure-recovery

# Show recovery settings
.\RelifeService.exe show-recovery

# Run interactively (for debugging)
.\RelifeService.exe
```

## Event Log Messages

The service logs to Windows Event Log (Application):

**Source:** `RelifeEnforcer`

**Key Events:**
- Service starting/stopping
- Enforcement enabled/disabled
- Tamper detection alerts
- Heartbeat saves
- Error conditions

**View Logs:**
```powershell
Get-EventLog -LogName Application -Source RelifeEnforcer -Newest 50
```

## State Files

**Location:** `C:\ProgramData\Relife\`

**Files:**
- `timeguard.state` - Encrypted time tracking state
- `enforcer.state` - (Reserved for future use)

**Format:** AES-256 encrypted JSON

**Security:** 
- Encrypted with symmetric key
- HMAC verification (via EncryptionService)
- Protected by NTFS permissions

## Dependencies

- .NET 10.0 Runtime
- Windows 10/11 or Windows Server
- Microsoft.Extensions.Hosting (10.0.0)
- Microsoft.Extensions.Hosting.WindowsServices (10.0.0)
- Relife.Core (project reference)

## License

See [LICENSE](../LICENSE) file.

## Contributing

This is part of the Relife project. See main [README.md](../README.md) for contribution guidelines.

## Troubleshooting

### Service won't start
- Check Event Viewer for specific errors
- Verify .NET runtime: `dotnet --version`
- Run interactively to see output: `.\RelifeService.exe`

### Service doesn't auto-restart
- Verify recovery config: `sc qfailure RelifeEnforcer`
- Re-apply: `.\RelifeService.exe configure-recovery`

### Enforcement not working
- Check if running as Administrator (required for registry writes)
- Verify hijacks: `Get-ItemProperty -Path "HKLM:\...\cmd.exe"`
- Check service logs for errors

### Tamper always detected
- State file may be corrupted
- Delete `C:\ProgramData\Relife\timeguard.state` and restart
- Check system clock for unusual behavior

---

**⚠️ WARNING:** This service is designed to be difficult to stop. Use responsibly and ensure you have administrative access before deployment.
