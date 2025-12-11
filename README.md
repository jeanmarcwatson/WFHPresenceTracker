@"
# Desk Presence Tracker

Automated Work-From-Home (WFH) presence tracking for Windows 11.  
Uses webcam-based presence detection, network geofencing, Google Calendar automation, timeline logging, and EOFY reporting.

## Features

- Webcam presence detection (Haar cascade + camera-busy fallback)
- Calendar automation (creates daily WFH event once presence threshold reached)
- Presence sampling every N minutes
- Network geofence using home gateway IP (VPN-proof)
- Daily timeline logging (Present / Away / Camera Busy)
- EOFY reporting with totals
- Manual report execution using DeskPresenceConsole.exe
- Optional system tray app
- Runs via Scheduled Task (no admin, no PowerShell execution policy issues)

---

## Installation Folder Structure

Place everything in:

C:\Apps\DeskPresenceTracker\

Files:

DeskPresenceService.exe  
DeskPresenceConsole.exe  
DeskPresenceTray.exe  
haarcascade_frontalface_default.xml  
GoogleCalendarServiceAccount.json  
appsettings.json  
Logs\  
Reports\  

---

## Example appsettings.json

{
  "GoogleCalendar": {
    "CalendarId": "YOUR_CALENDAR_ID@group.calendar.google.com"
  },
  "Presence": {
    "SampleIntervalMinutes": 5,
    "DetectionWindowSeconds": 10,
    "DailyPresenceThreshold": 3,
    "EnableNetworkGeofence": true,
    "HomeGateway": "192.168.1.1",
    "FaceDetectionHits": 1,
    "FaceMinSizeRatio": 0.08,
    "FaceUseCenterCrop": false,
    "CameraBusyCountsAsPresent": true,
    "CameraBusyMaxGrabFailures": 5,
    "CameraBusyMinFps": 5.0
  },
  "Logging": {
    "Enabled": true,
    "LogFolder": "C:\\Apps\\DeskPresenceTracker\\Logs"
  },
  "Reporting": {
    "Enabled": true,
    "DailyScheduleTime": "02:00",
    "ReportOutputFolder": "C:\\Users\\Public\\Documents\\DeskPresenceReports",
    "WeekdaysOnly": true
  },
  "Email": {
    "Enabled": false,
    "SmtpServer": "smtp.gmail.com",
    "Port": 587,
    "Username": "YOUR_EMAIL@gmail.com",
    "Password": "APP_PASSWORD",
    "To": "YOUR_EMAIL@gmail.com"
  }
}

---

## Build Instructions

cd C:\Dev\WFHPresenceTracker  
dotnet build  

### Publish all executables

dotnet publish .\DeskPresenceService\DeskPresenceService.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "C:\Apps\DeskPresenceTracker"

dotnet publish .\DeskPresenceConsole\DeskPresenceConsole.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "C:\Apps\DeskPresenceTracker"

dotnet publish .\DeskPresenceTray\DeskPresenceTray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "C:\Apps\DeskPresenceTracker"

Then copy:
haarcascade_frontalface_default.xml  
GoogleCalendarServiceAccount.json  
appsettings.json  

---

## Running Manually

cd C:\Apps\DeskPresenceTracker  
.\DeskPresenceService.exe

Expected output:

"Face detected" → Present  
"No confirmed face" → Away  
"Camera appears busy" → Present (meeting mode)

---

## Scheduled Task Setup (Recommended)

1. Open Task Scheduler  
2. Create Task (not basic task)  
3. Name: DeskPresenceTracker  
4. Trigger: "At logon"  
5. Action: Start Program → C:\Apps\DeskPresenceTracker\DeskPresenceService.exe  
6. Run whether user is logged on or not  
7. Do NOT store password  
8. User Account = your normal login account  
9. Save

Start it manually:

schtasks /run /tn "DeskPresenceTracker"

Stop it:

schtasks /end /tn "DeskPresenceTracker"

---

## Tray Application (Optional)

To auto-start:

Win + R  
shell:startup  
Add shortcut to:

C:\Apps\DeskPresenceTracker\DeskPresenceTray.exe

Tray functions:
- View logs  
- View reports  
- See today's presence timeline  
- Quit  

---

## EOFY Report

### Automatic daily generation
Runs at Reporting:DailyScheduleTime.

### Manual run
cd C:\Apps\DeskPresenceTracker  
.\DeskPresenceConsole.exe report

Output written to:

C:\Users\Public\Documents\DeskPresenceReports

Report example:

WFH Report 2025-07-01 → 2026-06-30  
Total WFH Days: 114  
2025-12-03 WFH  
2025-12-04 WFH  
...

---

## Presence Detection Logic Summary

1. Every SampleIntervalMinutes → run detection window.
2. During DetectionWindowSeconds:
   - Try to read webcam frames.
   - If frame-grab repeatedly fails → camera in use → Present.
   - If FPS < CameraBusyMinFps → Present.
   - Run face cascade → count faces meeting min-size requirement.
3. If hits ≥ FaceDetectionHits → Present.
4. Otherwise → Away.
5. Timeline log is updated:
   09:05 Present  
   09:10 Away  
   09:15 Present (camera busy)  
6. If DailyPresenceThreshold reached → create WFH calendar event.

---

## Logs

Stored in:
C:\Apps\DeskPresenceTracker\Logs

Example:

DeskPresence-2025-12-11.log

Contains:

10:21 Present  
10:26 Away  
10:31 Present (camera busy)

---

## Troubleshooting

### Detection always shows Away
- Reduce FaceMinSizeRatio  
- Disable FaceUseCenterCrop  
- Increase DetectionWindowSeconds  

### Meetings incorrectly show Away
- Reduce CameraBusyMinFps  
- Increase CameraBusyMaxGrabFailures  

### Calendar not updating
- Ensure service account has "Make changes to events" permission on the calendar.  
- Ensure GoogleCalendar.CalendarId is correct.  

---

## License
Internal personal automation tool.

"@
