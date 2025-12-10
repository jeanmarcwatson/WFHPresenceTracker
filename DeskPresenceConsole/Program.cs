using DeskPresenceService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeskPresenceConsole;

public class Program
{
    public static async Task Main(string[] args)
    {
        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                });
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;

                // Reuse the same CalendarClient + reporter as the service.
                services.AddSingleton<CalendarClient>();
                services.AddSingleton<EofyReporter>();
            })
            .Build();

        var reporter = host.Services.GetRequiredService<EofyReporter>();

        // For now, a single verb: "report"
        if (args.Length == 0 || !string.Equals(args[0], "report", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Usage: DeskPresenceConsole report");
            return;
        }

        var (csvPath, summaryPath, totalDays) = await reporter.GenerateCurrentFinancialYearReportAsync();

        Console.WriteLine();
        Console.WriteLine($"WFH Summary generated.");
        Console.WriteLine($"  Total WFH days : {totalDays}");
        Console.WriteLine($"  CSV report     : {csvPath}");
        Console.WriteLine($"  Summary report : {summaryPath}");
        Console.WriteLine();
    }
}
