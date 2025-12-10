using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DeskPresenceService;

/// <summary>
/// Generates end-of-financial-year WFH reports.
/// </summary>
public class EofyReporter
{
    private readonly CalendarClient _calendarClient;
    private readonly ILogger<EofyReporter> _logger;
    private readonly IConfiguration _config;

    public EofyReporter(
        CalendarClient calendarClient,
        ILogger<EofyReporter> logger,
        IConfiguration config)
    {
        _calendarClient = calendarClient;
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// Called from the service once a day; only runs when Reporting:Enabled = true.
    /// Generates CSV + summary for the *current* FY.
    /// </summary>
    public async Task RunIfTodayIsReportDayAsync()
    {
        var reportingSection = _config.GetSection("Reporting");
        bool enabled = reportingSection.GetValue("Enabled", true);

        if (!enabled)
        {
            _logger.LogInformation("Reporting disabled in configuration; skipping EOFY report.");
            return;
        }

        string outputFolder = reportingSection.GetValue(
            "ReportOutputFolder",
            @"C:\Users\Public\Documents\DeskPresenceReports");

        // Generate report for the financial year containing today.
        DateTime today = DateTime.Today;
        var (fyStart, fyEnd) = GetFinancialYearBounds(today);

        _ = await GenerateFinancialYearReportAsync(fyStart, fyEnd, outputFolder);
    }

    /// <summary>
    /// Generates CSV and a .txt summary for the specified FY.
    /// Returns (csvPath, summaryPath, totalDays).
    /// Used by both the service and the console tool.
    /// </summary>
    public async Task<(string CsvPath, string SummaryPath, int TotalDays)>
        GenerateFinancialYearReportAsync(DateTime fyStart, DateTime fyEnd, string outputFolder)
    {
        Directory.CreateDirectory(outputFolder);

        // Pull all WFH days from the WFH calendar.
        var wfhDays = await _calendarClient.GetHomeDayEventsAsync(fyStart, fyEnd);

        string fyLabel = $"{fyStart:yyyy}-{fyEnd:yyyy}";
        string csvPath = Path.Combine(outputFolder, $"WFH-FY{fyLabel}.csv");
        string summaryPath = Path.Combine(outputFolder, $"WFH-FY{fyLabel}-summary.txt");

        // Write CSV: one row per WFH day.
        using (var writer = new StreamWriter(csvPath, false, System.Text.Encoding.UTF8))
        {
            writer.WriteLine("Date,Title,Source");

            foreach (var e in wfhDays.OrderBy(e => e.Date))
            {
                // Very simple CSV escaping (no commas expected in title).
                writer.WriteLine($"{e.Date:yyyy-MM-dd},{e.Title},DeskPresenceTracker");
            }
        }

        int totalDays = wfhDays.Count;

        // Write summary text file.
        using (var writer = new StreamWriter(summaryPath, false, System.Text.Encoding.UTF8))
        {
            writer.WriteLine($"WFH Summary for FY {fyStart:dd MMM yyyy} – {fyEnd:dd MMM yyyy}");
            writer.WriteLine($"Total WFH days: {totalDays}");
            writer.WriteLine();
            writer.WriteLine($"CSV report: {csvPath}");
        }

        _logger.LogInformation(
            "Generated WFH FY report. CSV: {CsvPath}, Summary: {SummaryPath}, TotalDays: {TotalDays}",
            csvPath, summaryPath, totalDays);

        return (csvPath, summaryPath, totalDays);
    }

    /// <summary>
    /// Utility used by the console to generate for "today's" FY.
    /// </summary>
    public Task<(string CsvPath, string SummaryPath, int TotalDays)>
        GenerateCurrentFinancialYearReportAsync()
    {
        string outputFolder = _config.GetValue(
            "Reporting:ReportOutputFolder",
            @"C:\Users\Public\Documents\DeskPresenceReports");

        DateTime today = DateTime.Today;
        var (fyStart, fyEnd) = GetFinancialYearBounds(today);

        return GenerateFinancialYearReportAsync(fyStart, fyEnd, outputFolder);
    }

    private static (DateTime Start, DateTime End) GetFinancialYearBounds(DateTime referenceDate)
    {
        // Australian FY: 1 July – 30 June
        int year = referenceDate.Month >= 7 ? referenceDate.Year : referenceDate.Year - 1;
        var start = new DateTime(year, 7, 1);
        var end = new DateTime(year + 1, 6, 30);
        return (start, end);
    }
}
