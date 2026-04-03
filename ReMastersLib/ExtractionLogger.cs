using System;
using System.IO;

namespace ReMastersLib
{
    public static class ExtractionLogger
    {
        private static readonly object Sync = new object();
        private static string _errorLogPath;

        public static void Initialize(string outputPath)
        {
            var logDir = Path.Combine(outputPath, "logs");
            Directory.CreateDirectory(logDir);
            _errorLogPath = Path.Combine(logDir, "errors.log");
        }

        public static void Error(string context, Exception ex)
        {
            var message = $"{context}: {ex}";
            Console.Error.WriteLine(message);
            Append($"[{DateTime.UtcNow:O}] ERROR {message}");
        }

        public static void Error(string message)
        {
            Console.Error.WriteLine(message);
            Append($"[{DateTime.UtcNow:O}] ERROR {message}");
        }

        private static void Append(string line)
        {
            if (string.IsNullOrWhiteSpace(_errorLogPath))
                return;

            lock (Sync)
            {
                File.AppendAllLines(_errorLogPath, new[] { line });
            }
        }
    }
}
