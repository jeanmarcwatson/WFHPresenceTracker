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
