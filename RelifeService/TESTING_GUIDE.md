# Relife Enforcer Service - "Unkillable" Testing Guide

## Overview
This guide provides step-by-step instructions to test the self-healing, "unkillable" nature of the Relife Enforcer Windows Service.

---

## Prerequisites

### 1. System Requirements
- Windows 10/11 or Windows Server
- Administrator privileges
- .NET 10.0 Runtime installed
- PowerShell or Command Prompt

### 2. Build the Service
```powershell
# Navigate to the service project
cd RelifeService

# Build in Release mode
dotnet publish -c Release -r win-x64 --self-contained false

# Note the output path (usually: bin\Release\net10.0\win-x64\publish\)
```

---

## Installation & Configuration

### Step 1: Install the Service

Open PowerShell or Command Prompt **as Administrator**:

```powershell
# Navigate to the published folder
cd C:\path\to\RelifeService\bin\Release\net10.0\win-x64\publish

# Install the service
sc create RelifeEnforcer binPath="C:\path\to\RelifeService\bin\Release\net10.0\win-x64\publish\RelifeService.exe" start=auto

# Verify installation
sc query RelifeEnforcer
```

**Expected Output:**
```
SERVICE_NAME: RelifeEnforcer
        TYPE               : 10  WIN32_OWN_PROCESS
        STATE              : 1  STOPPED
        ...
```

### Step 2: Configure Recovery (Unkillable Mode)

Still as Administrator:

```powershell
# Run the recovery configuration command
.\RelifeService.exe configure-recovery
```

**Expected Output:**
```
=== Configuring Unkillable Service ===
Configuring recovery settings for service: RelifeEnforcer
  ✓ Service recovery configured: Auto-restart on failure with 1-second delay
Configuring delayed auto-start for service: RelifeEnforcer
  ✓ Service set to Automatic start
  ✓ Service set to Automatic (Delayed Start)
=== Configuration Complete ===
Service 'RelifeEnforcer' is now configured for maximum resilience:
  ✓ Auto-restart on failure (1 second delay)
  ✓ Automatic (Delayed Start) for boot priority
  ✓ Failure counter resets every 24 hours
```

### Step 3: Verify Recovery Configuration

```powershell
# Display current recovery settings
.\RelifeService.exe show-recovery

# OR use native SC command
sc qfailure RelifeEnforcer
```

**Expected Output:**
```
[SC] QueryServiceConfig2 SUCCESS

SERVICE_NAME: RelifeEnforcer
        RESET_PERIOD (in seconds)    : 86400
        REBOOT_MESSAGE               :
        COMMAND_LINE                 :

        FAILURE_ACTIONS              :
                RESTART -- Delay = 1000 milliseconds.
                RESTART -- Delay = 1000 milliseconds.
                RESTART -- Delay = 1000 milliseconds.
```

### Step 4: Start the Service

```powershell
# Start the service
sc start RelifeEnforcer

# Verify it's running
sc query RelifeEnforcer
```

**Expected Output:**
```
SERVICE_NAME: RelifeEnforcer
        STATE              : 4  RUNNING
        ...
```

---

## Test 1: Task Manager Kill Test

### Objective
Verify that killing the service process via Task Manager results in automatic restart within 1 second.

### Procedure

1. **Open Task Manager**
   - Press `Ctrl + Shift + Esc`
   - Go to the "Details" tab
   - Find `RelifeService.exe` in the process list
   - **Note the PID (Process ID)** - Write it down

2. **Kill the Process (Attempt 1)**
   - Right-click on `RelifeService.exe`
   - Select "End Task"
   - **Observe**: The process should disappear momentarily

3. **Verify Auto-Restart**
   - **Within 1-2 seconds**, refresh the Details tab or watch closely
   - A **new instance** of `RelifeService.exe` should appear
   - **The PID will be different** (this proves it's a new process)
   - Time the restart with a stopwatch if needed

4. **Repeat Multiple Times**
   - Kill the process 3-5 times in succession
   - Each time, verify it restarts with a new PID
   - The restart should always occur within ~1 second

### Expected Results
✅ **PASS Criteria:**
- Service restarts within 1 second after being killed
- New PID appears each time
- No manual intervention required
- Service can be killed and restarted indefinitely

❌ **FAIL Criteria:**
- Service does not restart
- Restart takes longer than 5 seconds
- Service enters a stopped state

### Troubleshooting
- Check Event Viewer → Windows Logs → Application for error messages
- Verify recovery configuration is still applied: `sc qfailure RelifeEnforcer`
- Ensure you're running as Administrator

---

## Test 2: Command Line Kill Test (TASKKILL)

### Objective
Verify that using the `taskkill` command (even with /F force flag) cannot permanently stop the service.

### Procedure

1. **Open PowerShell or Command Prompt as Administrator**

2. **Find the Service Process**
   ```powershell
   # List all RelifeService processes
   tasklist | findstr RelifeService
   ```
   
   **Expected Output:**
   ```
   RelifeService.exe            1234 Services                   0     15,432 K
   ```
   
   Note the PID (e.g., 1234 in this example)

3. **Attempt Standard Kill**
   ```powershell
   taskkill /PID 1234
   ```
   
   **Expected Output:**
   ```
   ERROR: The process "RelifeService.exe" with PID 1234 could not be terminated.
   Reason: This process can only be terminated forcefully (with /F option).
   ```

4. **Attempt Forced Kill**
   ```powershell
   taskkill /F /PID 1234
   ```
   
   **Expected Output:**
   ```
   SUCCESS: The process with PID 1234 has been terminated.
   ```

5. **Verify Immediate Restart**
   ```powershell
   # Immediately check if the service restarted (within 1 second)
   timeout /t 2 /nobreak
   tasklist | findstr RelifeService
   ```
   
   **Expected Output:**
   ```
   RelifeService.exe            5678 Services                   0     15,432 K
   ```
   
   ✅ **Note the PID has changed** (5678 vs 1234) - this proves it's a new process

6. **Repeat Force Kill 5 Times**
   ```powershell
   # Script to kill 5 times and verify restart
   for ($i=1; $i -le 5; $i++) {
       Write-Host "Kill attempt $i"
       $pid = (Get-Process -Name RelifeService -ErrorAction SilentlyContinue).Id
       if ($pid) {
           taskkill /F /PID $pid
           Start-Sleep -Seconds 2
           $newPid = (Get-Process -Name RelifeService -ErrorAction SilentlyContinue).Id
           Write-Host "Old PID: $pid | New PID: $newPid"
       }
   }
   ```

### Expected Results
✅ **PASS Criteria:**
- Each `taskkill /F` successfully kills the process
- Service restarts within 1-2 seconds every time
- PID changes with each restart
- No error states in Event Viewer

❌ **FAIL Criteria:**
- Service fails to restart
- Service enters ERROR or STOPPED state
- Restart delay exceeds 5 seconds

---

## Test 3: Service Control Manager Stop Test

### Objective
Verify behavior when stopping the service via `sc stop` or Services GUI.

### Procedure

1. **Stop via Command Line**
   ```powershell
   # As Administrator
   sc stop RelifeEnforcer
   ```
   
   **Expected Output:**
   ```
   SERVICE_NAME: RelifeEnforcer
           TYPE               : 10  WIN32_OWN_PROCESS
           STATE              : 3  STOP_PENDING
           ...
   ```

2. **Check Status After 2 Seconds**
   ```powershell
   timeout /t 2 /nobreak
   sc query RelifeEnforcer
   ```
   
   **Expected Behavior:**
   - The service **may** restart automatically depending on how Windows interprets the stop
   - Recovery actions **may not** trigger for a graceful `sc stop` command
   - This is expected behavior: recovery is for **failures**, not manual stops

3. **Alternative: Stop via Services GUI**
   - Press `Win + R`, type `services.msc`, press Enter
   - Find "RelifeEnforcer" in the list
   - Right-click → Stop
   - Observe the service status

### Expected Results
✅ **IMPORTANT NOTE:**
- Windows Service recovery actions **do not trigger on manual stops**
- Recovery only triggers on **unexpected failures** (crashes, kills)
- This is by design in Windows Service Control Manager
- The service will **not** auto-restart after `sc stop` (this is normal)

To restart after manual stop:
```powershell
sc start RelifeEnforcer
```

---

## Test 4: Reboot Persistence Test

### Objective
Verify that the service automatically starts after system reboot.

### Procedure

1. **Verify Service is Configured for Auto-Start**
   ```powershell
   sc qc RelifeEnforcer
   ```
   
   Look for:
   ```
   START_TYPE             : 2   AUTO_START (DELAYED)
   ```

2. **Reboot the System**
   ```powershell
   shutdown /r /t 5 /c "Testing Relife Enforcer auto-start"
   ```

3. **After Reboot - Check Service Status**
   ```powershell
   # Wait ~30 seconds after boot (delayed start)
   sc query RelifeEnforcer
   ```
   
   **Expected Output:**
   ```
   STATE              : 4  RUNNING
   ```

4. **Check Event Logs**
   - Open Event Viewer
   - Navigate to: Windows Logs → Application
   - Filter by Source: "RelifeEnforcer"
   - Verify startup messages

### Expected Results
✅ **PASS Criteria:**
- Service starts automatically within 30-60 seconds of boot
- Service state is RUNNING
- No errors in Event Viewer
- Enforcement is active (registry hijacks are in place)

---

## Test 5: Crash Simulation Test

### Objective
Verify that if the service crashes due to an exception, it auto-restarts.

### Procedure

**Note:** This requires modifying the Worker code temporarily to simulate a crash.

1. **Add Crash Trigger to Worker.cs** (for testing only)
   
   In the `ExecuteAsync` method, add a time-based crash:
   ```csharp
   // Simulate crash after 10 seconds
   if (DateTime.UtcNow.Second == 10)
   {
       throw new Exception("SIMULATED CRASH FOR TESTING");
   }
   ```

2. **Rebuild and Reinstall**
   ```powershell
   dotnet publish -c Release
   sc stop RelifeEnforcer
   # Copy new files
   sc start RelifeEnforcer
   ```

3. **Monitor Event Viewer**
   - Open Event Viewer → Application
   - Watch for the crash exception at :10 seconds
   - Observe the restart happening automatically

4. **Verify Restart Count**
   ```powershell
   # Check how many times the service has failed
   # (This resets every 24 hours as configured)
   sc qfailure RelifeEnforcer
   ```

### Expected Results
✅ **PASS Criteria:**
- Service crashes when exception is thrown
- Service automatically restarts within 1 second
- Event Viewer shows the exception AND the restart
- Service continues running normally after restart

---

## Test 6: Enforcement Verification Test

### Objective
Verify that the service is actually enforcing process blocks via IFEO registry hijacks.

### Procedure

1. **Check Registry Hijacks**
   
   Open PowerShell as Administrator:
   ```powershell
   # Check if cmd.exe is hijacked
   Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\cmd.exe" -Name Debugger -ErrorAction SilentlyContinue
   
   # Check Task Manager hijack
   Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\Taskmgr.exe" -Name Debugger -ErrorAction SilentlyContinue
   ```
   
   **Expected Output (if enforcement is active):**
   ```
   Debugger : C:\path\to\RelifeService.exe
   ```

2. **Test Process Block**
   - Try to open Command Prompt (Win + R → cmd)
   - If enforcement is active, you should either:
     - Not be able to open it, OR
     - See the Relife enforcer intercept it

3. **Monitor Service Logs**
   ```powershell
   # View recent service logs
   Get-EventLog -LogName Application -Source "RelifeEnforcer" -Newest 50
   ```

### Expected Results
✅ **PASS Criteria:**
- Registry hijacks are present in IFEO for target processes
- Blocked processes cannot run normally
- Service logs show enforcement activity

---

## Summary Checklist

After completing all tests, verify:

- [ ] Service installs successfully
- [ ] Recovery configuration applies without errors
- [ ] Service auto-starts on system boot
- [ ] Task Manager kill results in 1-second restart
- [ ] `taskkill /F` results in 1-second restart
- [ ] Multiple consecutive kills all result in restarts
- [ ] Event Viewer shows no critical errors
- [ ] Registry hijacks are in place when enforcement is active
- [ ] Service survives 5+ consecutive force kills

---

## Cleanup (Optional)

To remove the service:

```powershell
# Stop the service
sc stop RelifeEnforcer

# Delete the service
sc delete RelifeEnforcer

# Manually clean up registry hijacks if needed
Remove-Item -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\cmd.exe" -ErrorAction SilentlyContinue
Remove-Item -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\Taskmgr.exe" -ErrorAction SilentlyContinue
Remove-Item -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\powershell.exe" -ErrorAction SilentlyContinue
```

---

## Troubleshooting Common Issues

### Issue: Service doesn't restart after kill
**Solution:**
- Verify recovery configuration: `sc qfailure RelifeEnforcer`
- Re-run: `RelifeService.exe configure-recovery`

### Issue: Access Denied errors
**Solution:**
- Ensure you're running PowerShell/CMD as Administrator
- Check UAC settings

### Issue: Service fails to start
**Solution:**
- Check Event Viewer for specific error messages
- Verify .NET 10.0 runtime is installed
- Check file permissions on the executable

### Issue: PID doesn't change after kill
**Solution:**
- You might be looking at the wrong process
- Use `Get-Process -Name RelifeService` to verify
- Check Task Manager → Details tab

---

## Expected Recovery Behavior Summary

| Action | Expected Result | Recovery Time |
|--------|----------------|---------------|
| `taskkill /F` | Auto-restart | 1 second |
| Task Manager → End Task | Auto-restart | 1 second |
| Process crash/exception | Auto-restart | 1 second |
| `sc stop` | Manual restart needed | N/A |
| System reboot | Auto-start on boot | 30-60 sec (delayed) |
| 3rd consecutive failure | Still auto-restart | 1 second |

The service is designed to be **unkillable** through normal task management tools, requiring administrative action via Service Control Manager to truly stop it.
