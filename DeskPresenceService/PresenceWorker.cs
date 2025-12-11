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

    private DateTime _today;
    private int _positiveSamplesToday;
    private bool _wfhMarkedToday;

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

        _sampleInterval = TimeSpan.FromMinutes(
            presenceSection.GetValue("SampleIntervalMinutes", 5));

        _detectionWindow = TimeSpan.FromSeconds(
            presenceSection.GetValue("DetectionWindowSeconds", 10));

        _dailyPresenceThreshold = presenceSection.GetValue("DailyPresenceThreshold", 3);
        _enableNetworkGeofence = presenceSection.GetValue("EnableNetworkGeofence", true);
        _homeGateway = presenceSection.GetValue("HomeGateway", "192.168.1.1");

        _reportTime = TimeSpan.Parse(
            config.GetValue("Reporting:DailyScheduleTime", "02:00"));

        _today = DateTime.Today;
        _positiveSamplesToday = 0;
        _wfhMarkedToday = false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Desk Presence Tracker service starting.");
        FileLog.Write("Desk Presence Tracker service starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                HandleNewDayRolloverIfNeeded();

                // SAFETY: if any older build ever set this negative, fix it once
                if (_positiveSamplesToday < 0)
                {
                    _logger.LogWarning(
                        "PositiveSamplesToday was negative ({Value}); resetting to 0.",
                        _positiveSamplesToday);
                    FileLog.Write($"PositiveSamplesToday was negative ({_positiveSamplesToday}); resetting to 0.");
                    _positiveSamplesToday = 0;
                }

                bool shouldSample = true;

                // ---- Network geofence (home gateway check) ----
                if (_enableNetworkGeofence)
                {
                    string gateway = _wifi.GetDefaultGateway();

                    if (string.IsNullOrWhiteSpace(gateway))
                    {
                        const string msg = "No default gateway found; skipping presence sample.";
                        _logger.LogInformation(msg);
                        FileLog.Write(msg);
                        shouldSample = false;
                    }
                    else if (!string.Equals(
                                 gateway,
                                 _homeGateway,
                                 StringComparison.OrdinalIgnoreCase))
                    {
                        string msg = $"Not on home network (gateway {gateway}); skipping presence sample.";
                        _logger.LogInformation(msg);
                        FileLog.Write(msg);
                        shouldSample = false;
                    }
                    else
                    {
                        string msg = $"On home network (gateway {gateway}); sampling presence.";
                        _logger.LogInformation(msg);
                        FileLog.Write(msg);
                    }
                }

                // ---- Presence sample via webcam ----
                if (shouldSample)
                {
                    var now = DateTime.Now;
                    string sampleMsg = $"Sampling presence at {now}.";
                    _logger.LogInformation(sampleMsg);
                    FileLog.Write(sampleMsg);

                    bool present = _webcam.IsUserPresent(_detectionWindow);

                    if (present)
                    {
                        // timeline log: Present
                        FileLog.WriteTimeline(now, "Present");

                        if (!_wfhMarkedToday)
                        {
                            _positiveSamplesToday++;
                            string presentMsg =
                                $"Presence detected. Positive samples today = {_positiveSamplesToday}.";
                            _logger.LogInformation(presentMsg);
                            FileLog.Write(presentMsg);
                        }
                        else
                        {
                            const string msg =
                                "Presence detected, but WFH already recorded today; counter not incremented.";
                            _logger.LogInformation(msg);
                            FileLog.Write(msg);
                        }
                    }
                    else
                    {
                        // timeline log: Away
                        FileLog.WriteTimeline(now, "Away");

                        const string awayMsg = "No presence detected this sample (Away).";
                        _logger.LogInformation(awayMsg);
                        FileLog.Write(awayMsg);
                    }

                    if (!_wfhMarkedToday &&
                        _positiveSamplesToday >= _dailyPresenceThreshold)
                    {
                        string threshMsg =
                            $"Presence threshold reached (count={_positiveSamplesToday}). Ensuring calendar event for {_today}.";
                        _logger.LogInformation(threshMsg);
                        FileLog.Write(threshMsg);

                        await EnsureCalendarIfThresholdReachedAsync();
                    }

                    await RunReportIfScheduledAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in presence loop.");
                FileLog.Write($"Error in presence loop: {ex}");
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

    // ----------------------------------------------------
    // Helpers
    // ----------------------------------------------------

    private void HandleNewDayRolloverIfNeeded()
    {
        if (DateTime.Today == _today)
            return;

        _today = DateTime.Today;
        _positiveSamplesToday = 0;
        _wfhMarkedToday = false;

        string msg = $"New day {_today}, counters reset.";
        _logger.LogInformation(msg);
        FileLog.Write(msg);
    }

    private async Task EnsureCalendarIfThresholdReachedAsync()
    {
        try
        {
            await _calendarClient.EnsureHomeDayEventAsync(_today);
            _wfhMarkedToday = true;

            string msg =
                $"WFH event ensured for {_today}. Further presence samples today will not affect calendar.";
            _logger.LogInformation(msg);
            FileLog.Write(msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error while ensuring calendar WFH event for {Date}.",
                _today);

            FileLog.Write($"Error while ensuring calendar WFH event for '{_today}': {ex}");
        }
    }

    private async Task RunReportIfScheduledAsync()
    {
        if (_reporter == null)
            return;

        var nowTime = DateTime.Now.TimeOfDay;
        var diffMinutes = Math.Abs((nowTime - _reportTime).TotalMinutes);

        // Within +/- 1 minute of scheduled time
        if (diffMinutes < 1.0)
        {
            try
            {
                await _reporter.RunIfTodayIsReportDayAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while running EOFY reporter.");
                FileLog.Write($"Error while running EOFY reporter: {ex}");
            }
        }
    }
}
