# Desk Presence Tracker

Automatically logs Work-From-Home (WFH) days to your Google Calendar using secure webcam face detection and home network presence.

## Features

- Local webcam face detection
- Automatically creates WFH calendar entries
- Configurable presence sampling and detection
- Network geofence (home gateway validation)
- Works as a Windows Service with Tray control
- EOFY reporting (CSV + summary text)
- Local logging and audit history

## Components

| Project | Purpose |
|---------|---------|
| DeskPresenceService | Background detection and calendar logging |
| DeskPresenceTray | UI tray for start/stop/logs |
| DeskPresenceConsole | Manual test-auth and report execution |

All apps installed to:
C:\Apps\DeskPresenceTracker\

## Requirements

- Windows 11 with camera access enabled
- Webcam (built-in or USB)
- Access to Google Calendar API (non-Advanced Protection)
- Self-contained .NET publish included in deployment

## Configuration

File:
C:\Apps\DeskPresenceTracker\appsettings.json

Example:

{
  "GoogleCalendar": {
    "CalendarId": "YOUR_CALENDAR_ID@group.calendar.google.com"
  },
  "Presence": {
    "SampleIntervalMinutes": 5,
    "DetectionWindowSeconds": 10,
    "DailyPresenceThreshold": 3,
    "EnableNetworkGeofence": true,
    "HomeGateway": "192.168.1.1"
  },
  "Reporting": {
    "Enabled": true,
    "DailyScheduleTime": "02:00",
    "ReportOutputFolder": "C:\\Users\\Public\\Documents\\DeskPresenceReports"
  },
  "Logging": {
    "Path": "C:\\Apps\\DeskPresenceTracker\\Logs",
    "Enabled": true
  }
}

## Google Setup Summary

1. Create or use a Google user account for automation
2. Create a separate calendar for WFH logging
3. Enable Google Calendar API in Google Cloud Console
4. Download OAuth credentials.json  place into:
   C:\Apps\DeskPresenceTracker\credentials.json
5. First run will prompt for authentication

## Deployment Instructions

From source solution folder:

dotnet publish .\DeskPresenceService -c Release -o "C:\Apps\DeskPresenceTracker" --self-contained -r win-x64
dotnet publish .\DeskPresenceTray -c Release -o "C:\Apps\DeskPresenceTracker" --self-contained -r win-x64
dotnet publish .\DeskPresenceConsole -c Release -o "C:\Apps\DeskPresenceTracker" --self-contained -r win-x64

Install Windows Service:

cd "C:\Apps\DeskPresenceTracker"
New-Service -Name "DeskPresenceTracker" ^
  -BinaryPathName "C:\Apps\DeskPresenceTracker\DeskPresenceService.exe" ^
  -DisplayName "Desk Presence Tracker" ^
  -Description "Monitors desk presence and logs WFH days" ^
  -StartupType Automatic
Start-Service DeskPresenceTracker

Add Tray app to startup:

 = "C:\Users\tmuser\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\DeskPresenceTray.lnk"
 = "C:\Apps\DeskPresenceTracker\DeskPresenceTray.exe"
 = New-Object -ComObject WScript.Shell
 = .CreateShortcut()
.TargetPath = 
.Save()

## Reporting

Output folder:
C:\Users\Public\Documents\DeskPresenceReports\

Daily auto report based on configuration.

Manual report:
.\DeskPresenceConsole.exe report

Test Calendar access:
.\DeskPresenceConsole.exe test-auth

## Logs

Stored at:
C:\Apps\DeskPresenceTracker\Logs\

Accessible via Tray  "View Logs"

## Troubleshooting

| Issue | Fix |
|------|-----|
| No presence recorded | Check webcam + permission settings |
| Not on home network | Ensure HomeGateway matches ipconfig |
| No calendar entries | Re-run test-auth |
| Service will not start | Check publish was self-contained |

## Status

In active daily use for personal WFH tracking.
