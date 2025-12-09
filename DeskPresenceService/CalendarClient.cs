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
