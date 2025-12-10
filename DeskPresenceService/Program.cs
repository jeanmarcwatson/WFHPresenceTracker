using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeskPresenceService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                // Default config already loads appsettings.json from the exe folder
                .ConfigureLogging(logging =>
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
                    // DI registrations for the worker and helpers
                    services.AddSingleton<CalendarClient>();
                    services.AddSingleton<WebcamPresenceDetector>();
                    services.AddSingleton<WifiHelper>();
                    services.AddSingleton<EofyReporter>();
                    services.AddHostedService<PresenceWorker>();
                });
    }
}
