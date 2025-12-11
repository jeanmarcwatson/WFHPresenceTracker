using System;
using System.IO;
using System.Text;

namespace DeskPresenceService
{
    /// <summary>
    /// Simple file logger for DeskPresence.
    /// Writes two files per day:
    ///  - DeskPresence-YYYY-MM-DD.log
    ///  - DeskPresenceTimeline-YYYY-MM-DD.log
    /// </summary>
    public static class FileLog
    {
        private static readonly object _lock = new();
        private static string _logFolder = AppContext.BaseDirectory;

        /// <summary>
        /// Folder where log files are written.
        /// Set from Program.cs using appsettings.json (Logging:LogFolder).
        /// </summary>
        public static string LogFolder
        {
            get => _logFolder;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logFolder = value;
                }
            }
        }

        private static void EnsureFolder()
        {
            if (!Directory.Exists(_logFolder))
            {
                Directory.CreateDirectory(_logFolder);
            }
        }

        private static string GetMainLogPath(DateTime date) =>
            Path.Combine(_logFolder, $"DeskPresence-{date:yyyy-MM-dd}.log");

        private static string GetTimelineLogPath(DateTime date) =>
            Path.Combine(_logFolder, $"DeskPresenceTimeline-{date:yyyy-MM-dd}.log");

        /// <summary>
        /// Writes a detailed log line like:
        /// 2025-12-11 12:03:36 On home network (gateway 192.168.1.1); sampling presence.
        /// </summary>
        public static void Write(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            EnsureFolder();

            var now = DateTime.Now;
            var line = $"{now:yyyy-MM-dd HH:mm:ss} {message}";

            lock (_lock)
            {
                File.AppendAllText(GetMainLogPath(now.Date), line + Environment.NewLine, Encoding.UTF8);
            }
        }

        /// <summary>
        /// Writes to the timeline log like:
        /// 07:44 Present
        /// 07:49 Away
        /// </summary>
        public static void WriteTimeline(DateTime timestamp, string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return;

            EnsureFolder();

            var line = $"{timestamp:HH:mm} {status}";

            lock (_lock)
            {
                File.AppendAllText(GetTimelineLogPath(timestamp.Date), line + Environment.NewLine, Encoding.UTF8);
            }
        }
    }
}
