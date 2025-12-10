# Desk Presence Tracker

Monitors whether you are present at your desk while working from home and automatically logs days into a Google Calendar.

This system:
- Captures presence via webcam motion/face detection
- Confirms home location using network gateway detection (avoids VPN mis-classification)
- Automatically creates *all-day* “WFH” events in a dedicated Google calendar
- Generates EOFY reporting of WFH days as CSV
- Logs diagnostic entries to daily files (NO Event Log usage)
- Includes a Tray app for visibility and manual controls (optional)

---

## 1️⃣ Installation

### 1.1 Download / Deploy the Build
Copy the published build to:

```
C:\Apps\DeskPresenceTracker\
```

Ensure the folder contains:
- `DeskPresenceService.exe` (background worker)
- `DeskPresenceTray.exe` (system tray UI)
- `appsettings.json`
- `Logs\` (auto-created after first run)
- `Reports\` (auto-created if Reporting enabled)

---

### 1.2 Required Files You Must Provide
```
appsettings.json
credentials.json  ← Google OAuth credentials file
```

Both go next to the EXE(s) in:
```
C:\Apps\DeskPresenceTracker\
```

---

### 1.3 Configure Google OAuth (one-time)
Follow Google Cloud OAuth instructions to create:
- Desktop user client credentials
- Enable **Google Calendar API**
- Download credentials → rename: `credentials.json`

First run will open browser → authorize → token stored locally.

---

## 2️⃣ Scheduled Task Setup (instead of Windows Service)

The worker should start:
✔️ Automatically  
✔️ Under current logged-in user (permissions for camera + Wi-Fi + email)

### Create Task UI Steps
Run: **Task Scheduler** → Create Task

General tab:
- Name: **Desk Presence Tracker**
- Run only when user is logged on *(required for camera access)*
- Run with highest privileges
- User: *(your domain or laptop user)*

Triggers tab:
- **At logon**
- *(Optional)* also **At startup**

Actions tab:
- **Start a program**
- Program/script:
```
C:\Apps\DeskPresenceTracker\DeskPresenceService.exe
```

Conditions tab:
- ✔️ “Start only if network connection is available”

Settings tab:
- ✔️ Allow task to be run on demand
- ✔️ Restart every 1 minute if failed

Click **OK** → enter Windows credentials if prompted.

---

## 3️⃣ System Tray App (optional UI)

To show logging status and allow exit:
- Create another scheduled task OR shortcut in Startup folder for:

```
C:\Apps\DeskPresenceTracker\DeskPresenceTray.exe
```

Startup folder path:
```
shell:startup
```

---

## 4️⃣ Logging

Logs are written to:
```
C:\Apps\DeskPresenceTracker\Logs
```

File format:
```
DeskPresence-YYYY-MM-DD.log
Timeline-YYYY-MM-DD.log
```

### Daily Log Contents Example
```
2025-12-10 09:25:15 [INFO] Sampling presence
2025-12-10 09:25:20 [INFO] Face detected. Positive samples today = 3.
2025-12-10 09:25:20 [INFO] WFH event recorded for today.
```

### Timeline Log Example
```
09:00 Present
09:05 Away
09:10 Present
09:15 Present
```

---

## 5️⃣ Reporting (EOFY Compliance)

Automatically runs daily at the configured time.

Manual run:
```
DeskPresenceConsole.exe report
```

Output:
- CSV written to:
```
C:\Users\Public\Documents\DeskPresenceReports
```
- Shows:
  - Date
  - “Present” (✓)
  - “WFH event recorded” flag
  - **Total WFH days count** summary row

---

## 6️⃣ Presence Tracking Logic

| Config | Meaning |
|--------|---------|
| `SampleIntervalMinutes` | Frequency of sampling. Default: 5 → ~12 samples/hour |
| `DetectionWindowSeconds` | Webcam active time per sample |
| `DailyPresenceThreshold` | Required positive samples to count a WFH day |
| `EnableNetworkGeofence` | If true, only samples if on home network |
| `HomeGateway` | Expected default gateway (Wi-Fi interface) |

### Daily workflow:
1. Every `SampleIntervalMinutes`, webcam checks if user is present
2. If present → positive sample count++
3. If count ≥ `DailyPresenceThreshold` → WFH day recorded once
4. Next day → counters reset at midnight

---

## 7️⃣ Configuration File

📌 Save as:
`C:\Apps\DeskPresenceTracker\appsettings.json`

```json
{
  "GoogleCalendar": {
    "CalendarId": "YOUR_WFH_CALENDAR_ID@group.calendar.google.com"
  },
  "Presence": {
    "SampleIntervalMinutes": 5,
    "DetectionWindowSeconds": 10,
    "DailyPresenceThreshold": 3,
    "EnableNetworkGeofence": true,
    "HomeGateway": "192.168.1.1"
  },
  "Logging": {
         "Enabled": true,
          "Path": "C:\\Apps\\DeskPresenceTracker\\Logs"
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
    "Password": "REPLACE_WITH_APP_PASSWORD",
    "To": "YOUR_EMAIL@gmail.com"
  }
}
```

---

## 8️⃣ Build + Publish Instructions (Developer Only)

From solution root:

Self-contained deployment:
```
dotnet publish DeskPresenceService -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "C:\Apps\DeskPresenceTracker"
```

And console:
```
dotnet publish DeskPresenceConsole -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "C:\Apps\DeskPresenceTracker"
```

Tray UI:
```
dotnet publish DeskPresenceTray -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "C:\Apps\DeskPresenceTracker"
```

---

## 9️⃣ Daily Operation

✔️ Runs automatically at login  
✔️ Logs presence silently  
✔️ Updates calendar only when threshold met  
✔️ Generate reports as needed

To manually start:
```
C:\Apps\DeskPresenceTracker\DeskPresenceService.exe
```

To view logs:
```
C:\Apps\DeskPresenceTracker\Logs\
```

To stop if needed:
- Exit Tray app
- End task in Task Manager

---

## 10️⃣ Support & Known Issues

| Issue | Fix |
|------|-----|
| VPN changes default route → wrong gateway | Use Wi-Fi gateway only (fixed) |
| First OAuth login fails | Run manually once outside Scheduled Task |
| Camera access denied | Ensure “Run only when user is logged on” |

---
