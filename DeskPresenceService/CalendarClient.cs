using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;

namespace DeskPresenceService;

public sealed record HomeDayRecord(DateTime Date, string Title);

// existing CalendarClient class follows...


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

    // inside public class CalendarClient
    public async Task<List<HomeDayRecord>> GetHomeDayEventsAsync(DateTime from, DateTime to)
    {
        // Make sure we have a service instance and calendar id the same way you do in EnsureHomeDayEventAsync.
        // I’m assuming you already have `_service` and `_calendarId` fields set up.
        // Do NOT change your constructor or auth code.

        var request = _service.Events.List(_calendarId);

        // Use the same time zone behaviour as your existing code.
        request.TimeMin = from;
        // include the end date fully
        request.TimeMax = to.AddDays(1);
        request.SingleEvents = true;
        request.ShowDeleted = false;
        request.MaxResults = 2500;

        var result = await request.ExecuteAsync();
        var list = new List<HomeDayRecord>();

        if (result.Items == null || result.Items.Count == 0)
            return list;

        foreach (var ev in result.Items)
        {
            // Decide what counts as a WFH event.
            // Here we treat ANY event whose Summary matches your WFH title.
            // Use the same summary text you use in EnsureHomeDayEventAsync.
            var title = ev.Summary ?? string.Empty;

            // If your EnsureHomeDayEventAsync uses "WFH" as the summary, filter on that:
            if (!string.Equals(title, "WFH", StringComparison.OrdinalIgnoreCase))
                continue;

            DateTime date;

            // All-day event: Start.Date is populated (yyyy-MM-dd)
            if (!string.IsNullOrEmpty(ev.Start?.Date))
            {
                // Parse as local date
                date = DateTime.Parse(ev.Start.Date).Date;
            }
            // Timed event: Start.DateTime
            else if (ev.Start?.DateTime != null)
            {
                date = ev.Start.DateTime.Value.Date;
            }
            else
            {
                continue;
            }

            list.Add(new HomeDayRecord(date, title));
        }

        return list;
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
