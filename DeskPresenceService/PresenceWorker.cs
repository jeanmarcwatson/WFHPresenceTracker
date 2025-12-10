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
    private readonly bool _enableNetworkGeofence;
    private readonly string _homeGateway;
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

        // Configure file logging
        var logFolder = config.GetValue<string>("Logging:LogFolder");
        FileLog.Configure(logFolder);
        FileLog.Write("PresenceWorker constructed.");

        var presenceSection = config.GetSection("Presence");

        _sampleInterval = TimeSpan.FromMinutes(
            presenceSection.GetValue("SampleIntervalMinutes", 5));

        _detectionWindow = TimeSpan.FromSeconds(
            presenceSection.GetValue("DetectionWindowSeconds", 10));

        _dailyPresenceThreshold = presenceSection.GetValue("DailyPresenceThreshold", 3);

        _enableNetworkGeofence = presenceSection.GetValue("EnableNetworkGeofence", true);
        _homeGateway = presenceSection.GetValue("HomeGateway", "192.168.1.1");

        _reportTime = TimeSpan.Parse(
            config.GetValue("Reporting:DailyScheduleTime", "02:00"));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Desk Presence Tracker service starting.");
        FileLog.Write("Desk Presence Tracker service starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (DateTime.Today != _today)
                {
                    _today = DateTime.Today;
                    _positiveSamplesToday = 0;
                    _logger.LogInformation("New day {Date}, counters reset.", _today);
                    FileLog.Write($"NEW_DAY {_today:yyyy-MM-dd} counters reset");
                }

                bool onHomeNetwork = true;

                if (_enableNetworkGeofence)
                {
                    onHomeNetwork = _wifi.IsOnHomeNetwork(_homeGateway);

                    if (!onHomeNetwork)
                    {
                        _logger.LogInformation(
                            "Not on home network (gateway {Gateway}); skipping presence sample.",
                            _homeGateway);

                        FileLog.Write($"GEOFENCE_NOT_HOME gateway={_homeGateway}");
                    }
                    else
                    {
                        _logger.LogInformation(
                            "On home network (gateway {Gateway}); sampling presence.",
                            _homeGateway);

                        FileLog.Write($"GEOFENCE_HOME gateway={_homeGateway}");
                    }
                }

                if (onHomeNetwork)
                {
                    _logger.LogInformation("Sampling presence at {Time}.", DateTime.Now);
                    FileLog.Write($"SAMPLE_START {DateTime.Now:HH:mm:ss}");

                    bool present = _webcam.IsUserPresent(_detectionWindow);

                    if (present)
                    {
                        _positiveSamplesToday++;
                        _logger.LogInformation(
                            "Face detected. Positive samples today = {Count}.",
                            _positiveSamplesToday);

                        FileLog.Write($"FACE_DETECTED count={_positiveSamplesToday}");
                    }
                    else
                    {
                        _logger.LogInformation("No face detected this sample.");
                        FileLog.Write("FACE_NOT_DETECTED");
                    }

                    if (_positiveSamplesToday >= _dailyPresenceThreshold)
                    {
                        _logger.LogInformation(
                            "Threshold reached; ensuring calendar event for today {Date}.",
                            _today);

                        FileLog.Write($"THRESHOLD_REACHED {_today:yyyy-MM-dd}");

                        await _calendarClient.EnsureHomeDayEventAsync(_today);

                        // Mark in log that a WFH day was recorded
                        FileLog.Write($"WFH_DAY_RECORDED {_today:yyyy-MM-dd}");

                        // Prevent multiple events per day
                        _positiveSamplesToday = int.MinValue / 2;
                    }

                    if (Math.Abs((DateTime.Now.TimeOfDay - _reportTime).TotalMinutes) < 1)
                    {
                        await _reporter.RunIfTodayIsReportDayAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in presence loop.");
                FileLog.Write($"ERROR {ex.GetType().Name}: {ex.Message}");
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
        FileLog.Write("Desk Presence Tracker service stopping.");
    }
}
