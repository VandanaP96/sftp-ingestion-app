using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Meduit.ShareNormalizer
{
    /// <summary>
    /// Thread-safe append-only inventory writer.
    /// </summary>
    internal sealed class InventoryWriter : IDisposable
    {
        public const string Header =
            "ScanTimeUtc,System,Client,EnableName,ClientCode,FileName,Extension,SizeBytes,LastWriteUtc,Kind,Action,Sha256,SourceFullPath,NormalizedPath";

        private readonly StreamWriter _w;

        private readonly object _writeLock =
            new object();

        public InventoryWriter(string path)
        {
            bool writeHeader =
                !File.Exists(path);

            _w =
                new StreamWriter(
                    path,
                    true,
                    new UTF8Encoding(false));

            _w.AutoFlush = true;

            if (writeHeader)
            {
                lock (_writeLock)
                {
                    _w.WriteLine(Header);
                }
            }
        }

        public void WriteRow(
            string system,
            string client,
            string enableName,
            string clientCode,
            FileInfo file,
            string kind,
            string action,
            string sha,
            string normalized)
        {
            string line =
                string.Join(",",
                new[]
                {
                    Csv(DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture)),
                    Csv(system),
                    Csv(client),
                    Csv(enableName),
                    Csv(clientCode),
                    Csv(file.Name),
                    Csv(file.Extension.ToLowerInvariant()),
                    Csv(file.Length.ToString(CultureInfo.InvariantCulture)),
                    Csv(file.LastWriteTimeUtc.ToString("s", CultureInfo.InvariantCulture)),
                    Csv(kind),
                    Csv(action),
                    Csv(sha ?? ""),
                    Csv(file.FullName),
                    Csv(normalized)
                });

            lock (_writeLock)
            {
                _w.WriteLine(line);
            }
        }

        private static string Csv(string value)
        {
            value = value ?? "";

            if (value.IndexOfAny(
                new[]
                {
                    ',',
                    '"',
                    '\r',
                    '\n'
                }) >= 0)
            {
                value =
                    "\"" +
                    value.Replace("\"", "\"\"") +
                    "\"";
            }

            return value;
        }

        public void Dispose()
        {
            lock (_writeLock)
            {
                _w.Flush();
                _w.Dispose();
            }
        }
    }
}