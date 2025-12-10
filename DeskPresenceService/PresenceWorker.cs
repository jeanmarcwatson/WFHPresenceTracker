using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeskPresenceService
{
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
        private readonly bool _enableNetworkGeofence;
        private readonly string _homeGateway;
        private readonly TimeSpan _reportTime;
        private readonly string _logFolder;

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
            var reportingSection = config.GetSection("Reporting");
            var loggingSection = config.GetSection("Logging");

            _sampleInterval = TimeSpan.FromMinutes(
                presenceSection.GetValue<int>("SampleIntervalMinutes", 5));

            _detectionWindow = TimeSpan.FromSeconds(
                presenceSection.GetValue<int>("DetectionWindowSeconds", 10));

            _dailyPresenceThreshold =
                presenceSection.GetValue<int>("DailyPresenceThreshold", 3);

            _enableNetworkGeofence =
                presenceSection.GetValue<bool>("EnableNetworkGeofence", true);

            _homeGateway =
                presenceSection.GetValue<string>("HomeGateway", "192.168.1.1");

            _reportTime = TimeSpan.Parse(
                reportingSection.GetValue<string>("DailyScheduleTime", "02:00"));

            // Logging folder: prefer Path, fall back to LogFolder, then default
            _logFolder =
                loggingSection.GetValue<string>("Path",
                    loggingSection.GetValue<string>(
                        "LogFolder",
                        @"C:\Apps\DeskPresenceTracker\Logs"));

            Directory.CreateDirectory(_logFolder);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            WriteMainLog("Desk Presence Tracker service starting.");
            _logger.LogInformation("Desk Presence Tracker service starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Day rollover
                    if (DateTime.Today != _today)
                    {
                        _today = DateTime.Today;
                        _positiveSamplesToday = 0;
                        WriteMainLog($"New day {_today:yyyy-MM-dd}, counters reset.");
                        _logger.LogInformation("New day {Date}, counters reset.", _today);
                    }

                    bool shouldSample = true;
                    string? gateway = null;

                    if (_enableNetworkGeofence)
                    {
                        gateway = _wifi.GetDefaultGateway();

                        if (string.IsNullOrWhiteSpace(gateway) || gateway == "0.0.0.0")
                        {
                            WriteMainLog("No usable default gateway; skipping presence sample.");
                            _logger.LogInformation(
                                "No default gateway detected; skipping presence sample.");
                            shouldSample = false;
                        }
                        else if (!string.Equals(gateway, _homeGateway, StringComparison.OrdinalIgnoreCase))
                        {
                            WriteMainLog(
                                $"Not on home network (gateway {gateway}); skipping presence sample.");
                            _logger.LogInformation(
                                "Not on home network (gateway {Gateway}); skipping presence sample.",
                                gateway);
                            shouldSample = false;
                        }
                        else
                        {
                            WriteMainLog(
                                $"On home network (gateway {gateway}); sampling presence.");
                            _logger.LogInformation(
                                "On home network (gateway {Gateway}); sampling presence.",
                                gateway);
                        }
                    }
                    else
                    {
                        WriteMainLog("Network geofence disabled; sampling without gateway check.");
                        _logger.LogInformation(
                            "Network geofence disabled; sampling presence without gateway check.");
                    }

                    if (shouldSample)
                    {
                        DateTime now = DateTime.Now;

                        WriteMainLog($"Sampling presence at {now:yyyy-MM-dd HH:mm:ss}.");
                        _logger.LogInformation("Sampling presence at {Time}.", now);

                        bool present = _webcam.IsUserPresent(_detectionWindow);

                        // Timeline log: 09:05 Present / Away
                        WriteTimeline(present ? "Present" : "Away");

                        if (present)
                        {
                            _positiveSamplesToday++;
                            WriteMainLog(
                                $"Face detected. Positive samples today = {_positiveSamplesToday}.");
                            _logger.LogInformation(
                                "Face detected. Positive samples today = {Count}.",
                                _positiveSamplesToday);
                        }
                        else
                        {
                            WriteMainLog("No face detected this sample.");
                            _logger.LogInformation("No face detected this sample.");
                        }

                        if (_positiveSamplesToday >= _dailyPresenceThreshold)
                        {
                            WriteMainLog(
                                $"Threshold reached ({_dailyPresenceThreshold}); " +
                                $"ensuring calendar event for {_today:yyyy-MM-dd}.");
                            _logger.LogInformation(
                                "Threshold reached; ensuring calendar event for today {Date}.",
                                _today);

                            await _calendarClient.EnsureHomeDayEventAsync(_today);

                            // Prevent re-writing events all day
                            _positiveSamplesToday = int.MinValue / 2;
                        }

                        // EOFY reporting window
                        if (Math.Abs((now.TimeOfDay - _reportTime).TotalMinutes) < 1)
                        {
                            WriteMainLog("Running EOFY report check.");
                            await _reporter.RunIfTodayIsReportDayAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteMainLog("Error in presence loop: " + ex);
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

            WriteMainLog("Desk Presence Tracker service stopping.");
            _logger.LogInformation("Desk Presence Tracker service stopping.");
        }

        private void WriteMainLog(string message)
        {
            try
            {
                string file = Path.Combine(
                    _logFolder,
                    $"DeskPresence-{DateTime.Today:yyyy-MM-dd}.log");

                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}";
                File.AppendAllText(file, line);
            }
            catch
            {
                // Don't crash the service because logging failed
            }
        }

        private void WriteTimeline(string state)
        {
            try
            {
                string file = Path.Combine(
                    _logFolder,
                    $"presence-timeline-{DateTime.Today:yyyy-MM-dd}.log");

                string line = $"{DateTime.Now:HH:mm} {state}{Environment.NewLine}";
                File.AppendAllText(file, line);
            }
            catch
            {
                // Ignore timeline logging errors too
            }
        }
    }
}
