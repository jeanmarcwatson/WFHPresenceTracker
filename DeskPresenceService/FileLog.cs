using System.Text;

namespace DeskPresenceService;

public static class FileLog
{
    private static readonly object _lock = new();
    private static string? _logDirectory;
    private static bool _initialized;

    public static void Configure(string? logDirectory)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(logDirectory))
            {
                // Default to <app base>\Logs
                _logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
            }
            else
            {
                _logDirectory = logDirectory;
            }

            Directory.CreateDirectory(_logDirectory);
            _initialized = true;
        }
    }

    public static void Write(string message)
    {
        try
        {
            if (!_initialized)
            {
                Configure(null);
            }

            string dir;
            lock (_lock)
            {
                dir = _logDirectory!;
            }

            string filePath = Path.Combine(dir, $"DeskPresence-{DateTime.Today:yyyy-MM-dd}.log");
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}";

            lock (_lock)
            {
                File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Never throw from logging
        }
    }
}
