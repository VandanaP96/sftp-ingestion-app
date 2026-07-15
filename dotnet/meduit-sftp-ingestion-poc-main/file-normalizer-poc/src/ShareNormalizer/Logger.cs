using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Meduit.ShareNormalizer
{
    /// <summary>
    /// Thread-safe logger.
    /// Writes to Console and normalize.log.
    /// </summary>
    internal sealed class Logger
    {
        private readonly string _logPath;

        private readonly object _lock =
            new object();

        private readonly object _syncRoot =
    new object();

        public Logger(string logPath)
        {
            _logPath = logPath;

            string folder =
                Path.GetDirectoryName(logPath);

            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }

        public void Log(string msg)
{
    string line =
        DateTime.Now.ToString(
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture)
        + "  "
        + msg;

    lock (_syncRoot)
    {
        Console.WriteLine(line);

        try
        {
            File.AppendAllText(
                _logPath,
                line + Environment.NewLine);
        }
        catch
        {
        }
    }
}
    }
}