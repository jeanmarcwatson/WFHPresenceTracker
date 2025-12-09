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
