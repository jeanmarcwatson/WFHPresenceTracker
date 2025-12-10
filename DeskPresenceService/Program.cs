using DeskPresenceService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.SetBasePath(AppContext.BaseDirectory);
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        config.AddEnvironmentVariables();
    })
    .ConfigureLogging((context, logging) =>
    {
        // Remove default providers (incl. EventLog)
        logging.ClearProviders();

        // Log to console; file logging is handled separately via FileLog
        logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        });
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        services.AddSingleton<CalendarClient>();
        services.AddSingleton<WebcamPresenceDetector>();
        services.AddSingleton<WifiHelper>();
        services.AddSingleton<EofyReporter>();

        services.AddHostedService<PresenceWorker>();
    })
    .Build();

// No UseWindowsService here – just run as a normal long-lived host
await host.RunAsync();
