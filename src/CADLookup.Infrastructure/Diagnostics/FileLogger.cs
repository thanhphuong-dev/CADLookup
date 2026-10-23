using System;
using System.IO;
using System.Text;
using CADLookup.Core.Contracts;

namespace CADLookup.Infrastructure.Diagnostics
{
    public static class FileLogger
    {
        private static readonly object Sync = new object();
        private static string LogPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CADLookup", "Logs", "cadlookup.log");

        public static void Error(string operation, CadHostInfo host, CadDocumentInfo? document,
            Exception exception, string? handle = null)
        {
            try
            {
                var directory = Path.GetDirectoryName(LogPath);
                if (directory != null) Directory.CreateDirectory(directory);
                var entry = new StringBuilder()
                    .Append(DateTime.UtcNow.ToString("o")).Append(" | ").Append(host.DisplayName)
                    .Append(" | ").Append(document?.Path ?? document?.Name ?? "no drawing")
                    .Append(" | ").Append(operation);
                if (!string.IsNullOrWhiteSpace(handle)) entry.Append(" | Handle ").Append(handle);
                entry.AppendLine().AppendLine(exception.ToString()).AppendLine();
                lock (Sync) File.AppendAllText(LogPath, entry.ToString(), new UTF8Encoding(false));
            }
            catch
            {
                // Diagnostics must never prevent inspection or close the host UI.
            }
        }
    }
}
