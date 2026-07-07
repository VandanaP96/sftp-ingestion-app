using System;
using System.Globalization;
using System.IO;

namespace Meduit.ShareNormalizer
{
    /// <summary>Writes each line to both the console and the run log (<c>normalize.log</c>).</summary>
    internal sealed class Logger
    {
        private readonly string _logPath;

        public Logger(string logPath)
        {
            _logPath = logPath;
        }

        public void Log(string msg)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "  " + msg;
            Console.WriteLine(line);
            try { File.AppendAllText(_logPath, line + Environment.NewLine); } catch { }
        }
    }
}
