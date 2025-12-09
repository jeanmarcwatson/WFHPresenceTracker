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
