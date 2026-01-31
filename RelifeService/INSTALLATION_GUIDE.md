# Relife Enforcer Service - Installation Guide

## Quick Start

### 1. Build the Service
```powershell
cd RelifeService
dotnet publish -c Release -r win-x64 --self-contained false
```

### 2. Install (as Administrator)
```powershell
# Navigate to publish folder
cd bin\Release\net10.0\win-x64\publish

# Create the service
sc create RelifeEnforcer binPath="%CD%\RelifeService.exe" start=auto DisplayName="Relife Enforcement Service"

# Configure unkillable recovery
.\RelifeService.exe configure-recovery

# Start the service
sc start RelifeEnforcer
```

### 3. Verify
```powershell
sc query RelifeEnforcer
.\RelifeService.exe show-recovery
```

---

## Detailed Installation Steps

### Prerequisites
- Windows 10/11 or Windows Server
- .NET 10.0 Runtime or SDK
- Administrator privileges

### Step 1: Build

```powershell
# Clone or navigate to the project
cd C:\path\to\Relife\RelifeService

# Build for release
dotnet publish -c Release -r win-x64 --self-contained false -o C:\RelifeEnforcer
```

This creates a deployment at `C:\RelifeEnforcer\`.

### Step 2: Create Windows Service

Open PowerShell as Administrator:

```powershell
sc create RelifeEnforcer `
  binPath="C:\RelifeEnforcer\RelifeService.exe" `
  start=auto `
  DisplayName="Relife Enforcement Service" `
  description="Tamper-proof time tracking and process enforcement service"
```

### Step 3: Configure Recovery (Unkillable Mode)

```powershell
cd C:\RelifeEnforcer
.\RelifeService.exe configure-recovery
```

This sets:
- Auto-restart on failure (1 second delay)
- Automatic (Delayed Start)
- Failure counter reset every 24 hours

### Step 4: Start the Service

```powershell
sc start RelifeEnforcer
```

### Step 5: Verify Installation

```powershell
# Check service status
sc query RelifeEnforcer

# View recovery configuration
.\RelifeService.exe show-recovery

# Check Event Logs
Get-EventLog -LogName Application -Source RelifeEnforcer -Newest 10
```

---

## Uninstallation

```powershell
# Stop the service
sc stop RelifeEnforcer

# Delete the service
sc delete RelifeEnforcer

# Clean up registry hijacks
Remove-Item -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\cmd.exe" -Force -ErrorAction SilentlyContinue
Remove-Item -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\Taskmgr.exe" -Force -ErrorAction SilentlyContinue
Remove-Item -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\powershell.exe" -Force -ErrorAction SilentlyContinue

# Remove files
Remove-Item -Path C:\RelifeEnforcer -Recurse -Force
```

---

## Configuration

### State Files Location
- TimeGuard state: `C:\ProgramData\Relife\timeguard.state`
- Enforcer state: `C:\ProgramData\Relife\enforcer.state`

### Encryption Key
⚠️ **SECURITY NOTE:** The default encryption key is hardcoded for development.

For production, modify [Program.cs](Program.cs) to load the key from:
- Azure Key Vault
- Windows Credential Manager
- Encrypted configuration file
- Environment variable (secured via DPAPI)

### Logging
Logs are written to:
- Console (when running interactively)
- Windows Event Log (Application → Source: RelifeEnforcer)

To view logs:
```powershell
Get-EventLog -LogName Application -Source RelifeEnforcer -Newest 50 | Format-List
```

---

## Service Commands

```powershell
# Start
sc start RelifeEnforcer

# Stop
sc stop RelifeEnforcer

# Query status
sc query RelifeEnforcer

# View configuration
sc qc RelifeEnforcer

# View failure/recovery settings
sc qfailure RelifeEnforcer

# Reconfigure recovery
C:\RelifeEnforcer\RelifeService.exe configure-recovery
```

---

## Troubleshooting

### Service fails to start
1. Check Event Viewer for errors
2. Verify .NET runtime is installed: `dotnet --version`
3. Check file permissions
4. Run interactively to see errors: `C:\RelifeEnforcer\RelifeService.exe`

### Access Denied
- Ensure running as Administrator
- Check that the service account has permissions

### Service doesn't auto-restart
- Verify recovery configuration: `sc qfailure RelifeEnforcer`
- Re-run: `.\RelifeService.exe configure-recovery`

---

## Advanced Configuration

### Change Service Account
```powershell
sc config RelifeEnforcer obj=DOMAIN\ServiceAccount password=Password123
```

### Change Start Mode
```powershell
# Manual start
sc config RelifeEnforcer start=demand

# Automatic (immediate)
sc config RelifeEnforcer start=auto

# Automatic (delayed)
sc config RelifeEnforcer start=delayed-auto

# Disabled
sc config RelifeEnforcer start=disabled
```

### Modify Recovery Actions
```powershell
# Reset failure count every 12 hours instead of 24
sc failure RelifeEnforcer reset=43200 actions=restart/1000/restart/1000/restart/1000

# Add a reboot action on 4th failure
sc failure RelifeEnforcer reset=86400 actions=restart/1000/restart/1000/restart/1000/reboot/5000
```
