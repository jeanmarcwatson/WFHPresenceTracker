# Install-DeskPresenceTracker.ps1
# Run as Administrator

$serviceName = "DeskPresenceTracker"
$installPath = "C:\Apps\DeskPresenceTracker"
$publishPath = "C:\Dev\DeskPresenceTracker\DeskPresenceService\bin\Release\net8.0\win-x64\publish"

Write-Host "Deploying service..." -ForegroundColor Cyan

# Stop service if exists
if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Write-Host "Stopping $serviceName service..."
    Stop-Service $serviceName -Force
    Start-Sleep -Seconds 2
}

# Ensure install directory exists
if (!(Test-Path $installPath)) {
    Write-Host "Creating install directory: $installPath"
    New-Item -ItemType Directory -Path $installPath | Out-Null
}

# Copy files
Write-Host "Copying published files to $installPath"
Copy-Item -Path "$publishPath\*" -Destination $installPath -Recurse -Force

# Install service if not already present
if (!(Get-Service -Name $serviceName -ErrorAction SilentlyContinue)) {
    Write-Host "Installing service..."

    $cred = Get-Credential -Message "Enter password for itsq\jwatson"

    New-Service -Name $serviceName `
        -BinaryPathName "$installPath\DeskPresenceService.exe" `
        -DisplayName "Desk Presence Tracker" `
        -Description "Monitors presence and logs WFH days + EOFY reports." `
        -StartupType Automatic `
        -Credential $cred

} else {
    Write-Host "Service already exists — skipping creation."
}

# Start service
Write-Host "Starting $serviceName service..."
Start-Service $serviceName

Write-Host "`nDeployment complete!" -ForegroundColor Green
Get-Service $serviceName
