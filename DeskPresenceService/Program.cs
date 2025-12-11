using System;
using System.IO;
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
                // NOTE: we are not using UseWindowsService() – you run as console / scheduled task
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.SetBasePath(AppContext.BaseDirectory);
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
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
                    // Configure FileLog folder from appsettings
                    var logFolder = context.Configuration.GetValue<string>(
                        "Logging:LogFolder",
                        Path.Combine(AppContext.BaseDirectory, "Logs"));

                    FileLog.LogFolder = logFolder;

                    services.AddSingleton<CalendarClient>();
                    services.AddSingleton<WebcamPresenceDetector>();
                    services.AddSingleton<WifiHelper>();
                    services.AddSingleton<EofyReporter>();
                    services.AddHostedService<PresenceWorker>();
                });
    }
}
