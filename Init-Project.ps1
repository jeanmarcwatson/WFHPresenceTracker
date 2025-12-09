# Init-WFHPresenceTracker.ps1
# Run from the root of your WFHPresenceTracker repo
# It will create DeskPresenceTracker.sln, DeskPresenceService, DeskPresenceConsole with all source.

$root = Get-Location

function New-File {
    param(
        [string]$Path,
        [string]$Content
    )
    $full = Join-Path $root $Path
    $dir = Split-Path $full -Parent
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir | Out-Null
    }
    Set-Content -Path $full -Value $Content -Encoding UTF8
    Write-Host "Created $Path"
}

# 1) Solution file
New-File "DeskPresenceTracker.sln" @'
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31912.275
MinimumVisualStudioVersion = 10.0.40219.1
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "DeskPresenceService", "DeskPresenceService\\DeskPresenceService.csproj", "{D5A27E86-53EC-4722-B4D3-5B81D95E3C35}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "DeskPresenceConsole", "DeskPresenceConsole\\DeskPresenceConsole.csproj", "{1D1D0FCB-90ED-4F10-B10A-861AF87FE556}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{D5A27E86-53EC-4722-B4D3-5B81D95E3C35}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{D5A27E86-53EC-4722-B4D3-5B81D95E3C35}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{D5A27E86-53EC-4722-B4D3-5B81D95E3C35}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{D5A27E86-53EC-4722-B4D3-5B81D95E3C35}.Release|Any CPU.Build.0 = Release|Any CPU
		{1D1D0FCB-90ED-4F10-B10A-861AF87FE556}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{1D1D0FCB-90ED-4F10-B10A-861AF87FE556}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{1D1D0FCB-90ED-4F10-B10A-861AF87FE556}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{1D1D0FCB-90ED-4F10-B10A-861AF87FE556}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
'@

# 2) DeskPresenceService project files

New-File "DeskPresenceService/DeskPresenceService.csproj" @'
<Project Sdk="Microsoft.NET.Sdk.Worker">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Google.Apis.Auth" Version="1.68.0" />
    <PackageReference Include="Google.Apis.Calendar.v3" Version="1.68.0.3445" />
    <PackageReference Include="OpenCvSharp4" Version="4.10.0" />
    <PackageReference Include="OpenCvSharp4.runtime.win" Version="4.10.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="8.0.0" />
    <PackageReference Include="MailKit" Version="4.5.0" />
    <PackageReference Include="MimeKit" Version="4.5.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <None Include="appsettings.json" CopyToOutputDirectory="PreserveNewest" />
    <None Include="credentials.json" CopyToOutputDirectory="PreserveNewest" />
    <None Include="haarcascade_frontalface_default.xml" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
'@

New-File "DeskPresenceService/appsettings.json" @'
{
  "GoogleCalendar": {
    "CalendarId": "6ad90cf75c1406aa66cb415ea3fdb735630b693c0ed880d7ae1583731919faeb@group.calendar.google.com"
  },
  "Presence": {
    "SampleIntervalMinutes": 5,
    "DetectionWindowSeconds": 10,
    "DailyPresenceThreshold": 3,
    "EnableWifiGeofence": true,
    "HomeSsid": "JAYS-NET-5G"
  },
  "Reporting": {
    "Enabled": true,
    "DailyScheduleTime": "02:00",
    "ReportOutputFolder": "C:\\\\Users\\\\Public\\\\Documents\\\\DeskPresenceReports"
  },
  "Email": {
    "Enabled": true,
    "SmtpServer": "smtp.gmail.com",
    "Port": 587,
    "Username": "YOUR_EMAIL@gmail.com",
    "Password": "REPLACE_WITH_APP_PASSWORD",
    "To": "YOUR_EMAIL@gmail.com"
  }
}
'@

New-File "DeskPresenceService/Program.cs" @'
using DeskPresenceService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options => options.ServiceName = "Desk Presence Tracker")
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<CalendarClient>();
        services.AddSingleton<WebcamPresenceDetector>();
        services.AddSingleton<WifiHelper>();
        services.AddSingleton<EofyReporter>();
        services.AddSingleton<EmailNotifier>();
        services.AddHostedService<PresenceWorker>();
    })
    .Build();

await host.RunAsync();
'@

New-File "DeskPresenceService/CalendarClient.cs" @'
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;

namespace DeskPresenceService;

public class CalendarClient
{
    private readonly CalendarService _service;
    private readonly string _calendarId;

    public CalendarClient()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        _calendarId = config["GoogleCalendar:CalendarId"]
            ?? throw new Exception("GoogleCalendar:CalendarId missing from appsettings.json");

        _service = CreateServiceAsync().GetAwaiter().GetResult();
    }

    private async Task<CalendarService> CreateServiceAsync()
    {
        string baseDir = AppContext.BaseDirectory;
        string credPath = Path.Combine(baseDir, "credentials.json");
        if (!File.Exists(credPath))
            throw new FileNotFoundException("credentials.json not found", credPath);

        using var stream = new FileStream(credPath, FileMode.Open, FileAccess.Read);
        string tokenDir = Path.Combine(baseDir, "token");
        Directory.CreateDirectory(tokenDir);

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            new[] { CalendarService.Scope.Calendar },
            "user",
            CancellationToken.None,
            new FileDataStore(tokenDir, true));

        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "DeskPresenceService"
        });
    }

    public async Task EnsureHomeDayEventAsync(DateTime date)
    {
        var listRequest = _service.Events.List(_calendarId);
        listRequest.TimeMin = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        listRequest.TimeMax = DateTime.SpecifyKind(date.Date.AddDays(1), DateTimeKind.Utc);
        listRequest.SingleEvents = true;

        var events = await listRequest.ExecuteAsync();
        if (events.Items != null && events.Items.Count > 0)
            return;

        var startDate = date.ToString("yyyy-MM-dd");
        var endDate = date.AddDays(1).ToString("yyyy-MM-dd");

        var ev = new Event
        {
            Summary = "WFH",
            Description = "Desk Presence Tracker",
            Start = new EventDateTime { Date = startDate },
            End = new EventDateTime { Date = endDate }
        };

        await _service.Events.Insert(ev, _calendarId).ExecuteAsync();
    }

    public async Task<int> CountHomeDaysAsync(DateTime fromInclusive, DateTime toExclusive)
    {
        var request = _service.Events.List(_calendarId);
        request.TimeMin = DateTime.SpecifyKind(fromInclusive, DateTimeKind.Utc);
        request.TimeMax = DateTime.SpecifyKind(toExclusive, DateTimeKind.Utc);
        request.SingleEvents = true;

        var uniqueDates = new HashSet<string>();
        Events events;
        do
        {
            events = await request.ExecuteAsync();
            if (events.Items != null)
            {
                foreach (var ev in events.Items)
                {
                    var day = ev.Start.Date ?? ev.Start.DateTime?.ToString("yyyy-MM-dd");
                    if (!string.IsNullOrEmpty(day))
                        uniqueDates.Add(day);
                }
            }
            request.PageToken = events.NextPageToken;
        } while (!string.IsNullOrEmpty(events.NextPageToken));

        return uniqueDates.Count;
    }
}
'@

New-File "DeskPresenceService/PresenceWorker.cs" @'
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeskPresenceService;

public class PresenceWorker : BackgroundService
{
    private readonly ILogger<PresenceWorker> _logger;
    private readonly CalendarClient _calendarClient;
    private readonly WebcamPresenceDetector _webcam;
    private readonly WifiHelper _wifi;
    private readonly EofyReporter _reporter;
    private readonly TimeSpan _sampleInterval;
    private readonly TimeSpan _detectionWindow;
    private readonly int _dailyPresenceThreshold;
    private readonly bool _enableWifiGeofence;
    private readonly string _homeSsid;
    private readonly TimeSpan _reportTime;

    private DateTime _today = DateTime.Today;
    private int _positiveSamplesToday = 0;

    public PresenceWorker(
        ILogger<PresenceWorker> logger,
        CalendarClient calendarClient,
        WebcamPresenceDetector webcam,
        WifiHelper wifi,
        EofyReporter reporter,
        IConfiguration config)
    {
        _logger = logger;
        _calendarClient = calendarClient;
        _webcam = webcam;
        _wifi = wifi;
        _reporter = reporter;

        var presenceSection = config.GetSection("Presence");
        _sampleInterval = TimeSpan.FromMinutes(presenceSection.GetValue("SampleIntervalMinutes", 5));
        _detectionWindow = TimeSpan.FromSeconds(presenceSection.GetValue("DetectionWindowSeconds", 10));
        _dailyPresenceThreshold = presenceSection.GetValue("DailyPresenceThreshold", 3);
        _enableWifiGeofence = presenceSection.GetValue("EnableWifiGeofence", true);
        _homeSsid = presenceSection.GetValue("HomeSsid", "JAYS-NET-5G");

        _reportTime = TimeSpan.Parse(config.GetValue("Reporting:DailyScheduleTime", "02:00"));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Desk Presence Tracker service starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (DateTime.Today != _today)
                {
                    _today = DateTime.Today;
                    _positiveSamplesToday = 0;
                    _logger.LogInformation("New day {Date}, counters reset.", _today);
                }

                if (_enableWifiGeofence && !_wifi.IsOnHomeNetwork(_homeSsid))
                {
                    _logger.LogInformation("Not on home Wi-Fi ({Ssid}); skipping presence sample.", _homeSsid);
                }
                else
                {
                    _logger.LogInformation("Sampling presence at {Time}.", DateTime.Now);
                    bool present = _webcam.IsUserPresent(_detectionWindow);
                    if (present)
                    {
                        _positiveSamplesToday++;
                        _logger.LogInformation("Face detected. Positive samples today = {Count}.", _positiveSamplesToday);
                    }
                    else
                    {
                        _logger.LogInformation("No face detected this sample.");
                    }

                    if (_positiveSamplesToday >= _dailyPresenceThreshold)
                    {
                        _logger.LogInformation("Threshold reached; ensuring calendar event for today {Date}.", _today);
                        await _calendarClient.EnsureHomeDayEventAsync(_today);
                        _positiveSamplesToday = int.MinValue / 2;
                    }

                    if (Math.Abs((DateTime.Now.TimeOfDay - _reportTime).TotalMinutes) < 1)
                        await _reporter.RunIfTodayIsReportDayAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in presence loop.");
            }

            try
            {
                await Task.Delay(_sampleInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // stopping
            }
        }

        _logger.LogInformation("Desk Presence Tracker service stopping.");
    }
}
'@

New-File "DeskPresenceService/WebcamPresenceDetector.cs" @'
using OpenCvSharp;

namespace DeskPresenceService;

public class WebcamPresenceDetector
{
    private readonly CascadeClassifier _faceCascade;

    public WebcamPresenceDetector()
    {
        string baseDir = AppContext.BaseDirectory;
        string cascadePath = Path.Combine(baseDir, "haarcascade_frontalface_default.xml");
        if (!File.Exists(cascadePath))
            throw new FileNotFoundException("Cascade file not found", cascadePath);

        _faceCascade = new CascadeClassifier(cascadePath);
    }

    public bool IsUserPresent(TimeSpan duration)
    {
        using var capture = new VideoCapture(0);
        if (!capture.IsOpened())
            return false;

        DateTime endTime = DateTime.UtcNow + duration;

        using var frame = new Mat();
        using var gray = new Mat();

        while (DateTime.UtcNow < endTime)
        {
            if (!capture.Read(frame) || frame.Empty())
            {
                Thread.Sleep(100);
                continue;
            }

            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.EqualizeHist(gray, gray);

            var faces = _faceCascade.DetectMultiScale(
                gray,
                scaleFactor: 1.1,
                minNeighbors: 3,
                flags: HaarDetectionTypes.ScaleImage,
                minSize: new Size(60, 60));

            if (faces.Length > 0)
                return true;

            Thread.Sleep(200);
        }

        return false;
    }
}
'@

New-File "DeskPresenceService/WifiHelper.cs" @'
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DeskPresenceService;

public class WifiHelper
{
    public bool IsOnHomeNetwork(string expectedSsid)
    {
        string? ssid = GetCurrentSsid();
        if (ssid == null) return false;
        return string.Equals(ssid, expectedSsid, StringComparison.OrdinalIgnoreCase);
    }

    private string? GetCurrentSsid()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = "wlan show interfaces",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc == null) return null;

        string output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();

        var match = Regex.Match(output, @"SSID\s*:\s*(.+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
'@

New-File "DeskPresenceService/EofyReporter.cs" @'
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DeskPresenceService;

public class EofyReporter
{
    private readonly CalendarClient _calendarClient;
    private readonly EmailNotifier _emailNotifier;
    private readonly ILogger<EofyReporter> _logger;
    private readonly IConfiguration _config;

    public EofyReporter(CalendarClient calendarClient, EmailNotifier emailNotifier, ILogger<EofyReporter> logger, IConfiguration config)
    {
        _calendarClient = calendarClient;
        _emailNotifier = emailNotifier;
        _logger = logger;
        _config = config;
    }

    public async Task RunIfTodayIsReportDayAsync()
    {
        var reportingSection = _config.GetSection("Reporting");
        if (!reportingSection.GetValue("Enabled", true))
        {
            _logger.LogInformation("Reporting disabled; skipping EOFY report.");
            return;
        }

        var now = DateTime.Now;
        if (now.Month != 7 || now.Day != 1)
            return;

        int fyStartYear = now.Year - 1;
        var from = new DateTime(fyStartYear, 7, 1);
        var to   = new DateTime(fyStartYear + 1, 7, 1);

        _logger.LogInformation("Running EOFY report for FY {Start}-{End}.", fyStartYear, fyStartYear + 1);

        int totalDays = await _calendarClient.CountHomeDaysAsync(from, to);

        string defaultFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "DeskPresenceReports");
        string folder = reportingSection.GetValue("ReportOutputFolder", defaultFolder);
        Directory.CreateDirectory(folder);

        string stem = $"WFHReport_FY{fyStartYear}-{fyStartYear + 1}";
        string txtPath = Path.Combine(folder, stem + ".txt");
        string csvPath = Path.Combine(folder, stem + ".csv");

        File.WriteAllText(txtPath,
            $"WFH Report for FY {fyStartYear}-{fyStartYear + 1}{Environment.NewLine}" +
            $"Total Days at Home: {totalDays}{Environment.NewLine}" +
            $"Generated: {DateTime.Now}{Environment.NewLine}");

        File.WriteAllText(csvPath,
            "Range,DaysAtHome" + Environment.NewLine +
            $"{from:yyyy-MM-dd} to {to.AddDays(-1):yyyy-MM-dd},{totalDays}");

        _logger.LogInformation("EOFY reports written: {Txt} and {Csv}.", txtPath, csvPath);

        await _emailNotifier.TrySendReportAsync(new[] { txtPath, csvPath });
    }
}
'@

New-File "DeskPresenceService/EmailNotifier.cs" @'
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace DeskPresenceService;

public class EmailNotifier
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailNotifier> _logger;

    public EmailNotifier(IConfiguration config, ILogger<EmailNotifier> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task TrySendReportAsync(string[] attachmentPaths)
    {
        if (!_config.GetValue("Email:Enabled", true))
        {
            _logger.LogInformation("Email sending disabled; skipping.");
            return;
        }

        try
        {
            string? smtpServer = _config["Email:SmtpServer"];
            int port = _config.GetValue("Email:Port", 587);
            string? username = _config["Email:Username"];
            string? password = _config["Email:Password"];
            string? toAddress = _config["Email:To"];

            if (string.IsNullOrWhiteSpace(smtpServer) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(toAddress))
            {
                _logger.LogWarning("Email configuration incomplete; cannot send report.");
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Desk Presence Tracker", username));
            message.To.Add(new MailboxAddress("", toAddress));
            message.Subject = "EOFY Work-From-Home Report";

            var builder = new BodyBuilder
            {
                TextBody = "Attached are your WFH EOFY reports (TXT and CSV)."
            };

            foreach (var path in attachmentPaths)
            {
                if (File.Exists(path))
                    builder.Attachments.Add(path);
            }

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(smtpServer, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("EOFY report email sent to {To}.", toAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send EOFY report email.");
        }
    }
}
'@

# 3) DeskPresenceConsole project

New-File "DeskPresenceConsole/DeskPresenceConsole.csproj" @'
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\\DeskPresenceService\\DeskPresenceService.csproj" />
    <None Include="..\\DeskPresenceService\\credentials.json"
          Link="credentials.json"
          CopyToOutputDirectory="PreserveNewest" />
    <None Include="..\\DeskPresenceService\\haarcascade_frontalface_default.xml"
          Link="haarcascade_frontalface_default.xml"
          CopyToOutputDirectory="PreserveNewest" />
    <None Include="..\\DeskPresenceService\\appsettings.json"
          Link="appsettings.json"
          CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
'@

New-File "DeskPresenceConsole/Program.cs" @'
using DeskPresenceService;

Console.WriteLine("Desk Presence Console Test");
Console.WriteLine("==========================\n");

Console.WriteLine("Step 1: Testing Google Calendar access (this may open a browser)...");

try
{
    var calendar = new CalendarClient();
    await calendar.EnsureHomeDayEventAsync(DateTime.Today);
    Console.WriteLine("✅ Google Calendar auth OK. (If this is the first run, you should have logged in.)");
}
catch (Exception ex)
{
    Console.WriteLine("❌ Error initialising CalendarClient or writing event:");
    Console.WriteLine(ex);
    Console.WriteLine("\nPress any key to quit.");
    Console.ReadKey();
    return;
}

Console.WriteLine("\nStep 2: Testing webcam presence detection.");
Console.WriteLine("Please sit in front of your webcam and look at it.");
Console.WriteLine("We will scan for about 15 seconds...\n");

try
{
    var webcam = new WebcamPresenceDetector();
    bool present = webcam.IsUserPresent(TimeSpan.FromSeconds(15));

    if (present)
        Console.WriteLine("✅ Face detected! Presence detection appears to be working.");
    else
    {
        Console.WriteLine("⚠ No face detected in the 15-second window.");
        Console.WriteLine("   Check lighting, camera selection (index 0), or camera privacy settings.");
    }
}
catch (Exception ex)
{
    Console.WriteLine("❌ Error during webcam detection:");
    Console.WriteLine(ex);
}

Console.WriteLine("\nDone. Press any key to exit.");
Console.ReadKey();
'@

Write-Host "`nAll files created. Next steps:"
Write-Host "1) Add credentials.json to DeskPresenceService (from Google Cloud)."
Write-Host "2) Add haarcascade_frontalface_default.xml to DeskPresenceService (from OpenCV)."
Write-Host "3) Open DeskPresenceTracker.sln in Visual Studio 2026."
Write-Host "4) Restore NuGet packages and build."
