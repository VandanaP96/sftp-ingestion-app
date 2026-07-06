using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Meduit.ShareNormalizer
{
    /// <summary>
    /// Append-only, Excel-friendly <c>inventory.csv</c> writer: one row per file seen. The header is
    /// written only when the file is first created, so repeated runs keep appending to one ledger.
    /// </summary>
    internal sealed class InventoryWriter : IDisposable
    {
        public const string Header =
            "ScanTimeUtc,System,Client,EnableName,ClientCode,FileName,Extension,SizeBytes,LastWriteUtc,Kind,Action,Sha256,SourceFullPath,NormalizedPath";

        private readonly StreamWriter _w;

        public InventoryWriter(string path)
        {
            bool writeHeader = !File.Exists(path);
            _w = new StreamWriter(path, append: true, encoding: new UTF8Encoding(false));
            if (writeHeader) _w.WriteLine(Header);
        }

        public void WriteRow(string system, string client, string enableName, string clientCode,
            FileInfo file, string kind, string action, string sha, string normalized)
        {
            _w.WriteLine(string.Join(",", new[]
            {
                Csv(DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture)),
                Csv(system), Csv(client), Csv(enableName), Csv(clientCode),
                Csv(file.Name), Csv(file.Extension.ToLowerInvariant()),
                Csv(file.Length.ToString(CultureInfo.InvariantCulture)),
                Csv(file.LastWriteTimeUtc.ToString("s", CultureInfo.InvariantCulture)),
                Csv(kind), Csv(action), Csv(sha ?? ""), Csv(file.FullName), Csv(normalized)
            }));
        }

        private static string Csv(string v)
        {
            v = v ?? "";
            if (v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0) v = "\"" + v.Replace("\"", "\"\"") + "\"";
            return v;
        }

        public void Dispose()
        {
            _w.Dispose();
        }
    }
}
