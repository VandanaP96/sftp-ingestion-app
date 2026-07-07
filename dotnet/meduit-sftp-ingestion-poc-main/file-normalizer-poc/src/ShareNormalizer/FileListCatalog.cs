using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Meduit.ShareNormalizer
{
    /// <summary>One catalog row: a file pattern for a client, with its system + enable + client code.</summary>
    internal sealed class FileEntry
    {
        public string System;
        public string Dir;
        public string ClientFolder;     // canonical, as written in the Excel "folder name"
        public string Enable;
        public string ClientCode;
        public Regex FileNameRegex;      // best-effort match against discovered file names (may be null)
    }

    /// <summary>
    /// The FILE_LIST.xlsx catalog: the AUTHORITY for which system (MCD1/MCD2/MCD3/BOPC) a discovered
    /// file belongs to - the system is not in the folder path, it lives in this workbook. Also offers
    /// best-effort client-name and enable/client-code enrichment.
    ///
    /// Each per-system tab is read by header name (columns differ per sheet), and system is taken from
    /// the "Planner Label" column (e.g. "MCD1- FCS-RMP-WACO" -> MCD1). Two lookups are built, most
    /// reliable first:
    ///   1. top-level directory under the share  (FCS-&gt;MCD1, IMC-&gt;MCD2, ABS-&gt;MCD3, BOPC-&gt;BOPC)
    ///   2. client folder name                     (e.g. CONIFER -&gt; MCD1)
    /// </summary>
    internal sealed class FileListCatalog
    {
        private readonly Dictionary<string, string> _dirToSystem =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _clientToSystem =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _clientCanonical =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<FileEntry>> _entriesByClient =
            new Dictionary<string, List<FileEntry>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _clientKeys = new List<string>();   // for "contains" matching

        private bool _allowContains = true;
        private const int MinContainsLen = 4;   // ignore tiny client tokens when doing substring match

        public int DirCount { get { return _dirToSystem.Count; } }
        public int ClientCount { get { return _clientCanonical.Count; } }
        public int RowCount { get; private set; }

        public static FileListCatalog Load(string path, bool allowContains)
        {
            var cat = new FileListCatalog { _allowContains = allowContains };
            foreach (var sheet in XlsxReader.Read(path))
                cat.IngestSheet(sheet);
            return cat;
        }

        private void IngestSheet(XlsxSheet sheet)
        {
            int headerIdx = FindHeaderRow(sheet.Rows);
            if (headerIdx < 0) return;                       // not a per-system file-list tab; skip
            var h = sheet.Rows[headerIdx];

            int cPlanner = FindCol(h, "planner label");
            int cFolder = FindCol(h, "folder name");
            int cDir = FindCol(h, "new business");
            if (cDir < 0) cDir = FindCol(h, "directory");
            int cEnable = FindCol(h, "enable name");
            int cCode = FindCol(h, "client code");
            int cFile = FindFileNameCol(h);
            if (cPlanner < 0 || cFolder < 0) return;

            for (int i = headerIdx + 1; i < sheet.Rows.Count; i++)
            {
                var r = sheet.Rows[i];
                string system = ParseSystem(Cell(r, cPlanner));
                if (system == null) continue;

                string folder = LastSegment(Cell(r, cFolder).Trim());
                if (folder.Length == 0) continue;
                string folderKey = NormKey(folder);
                string dir = Cell(r, cDir).Trim();

                if (dir.Length > 0 && !_dirToSystem.ContainsKey(dir)) _dirToSystem[dir] = system;
                if (!_clientToSystem.ContainsKey(folderKey)) _clientToSystem[folderKey] = system;
                if (!_clientCanonical.ContainsKey(folderKey)) { _clientCanonical[folderKey] = folder; _clientKeys.Add(folderKey); }

                var entry = new FileEntry
                {
                    System = system,
                    Dir = dir,
                    ClientFolder = folder,
                    Enable = Cell(r, cEnable).Trim(),
                    ClientCode = Cell(r, cCode).Trim(),
                    FileNameRegex = BuildPatternRegex(Cell(r, cFile))
                };
                List<FileEntry> list;
                if (!_entriesByClient.TryGetValue(folderKey, out list))
                {
                    list = new List<FileEntry>();
                    _entriesByClient[folderKey] = list;
                }
                list.Add(entry);
                RowCount++;
            }
        }

        /// <summary>System for a file given its folder segments, or null if the catalog can't tell.</summary>
        public string ResolveSystem(string[] dirSegs)
        {
            foreach (var seg in dirSegs)                       // top-folder map first (FCS/IMC/ABS/BOPC)
            {
                string s;
                if (_dirToSystem.TryGetValue(seg.Trim(), out s)) return s;
            }
            string key = MatchClientKey(dirSegs);             // then by client folder (exact or contains)
            return key == null ? null : _clientToSystem[key];
        }

        /// <summary>Canonical client-folder name from the workbook, or null if no segment matches.</summary>
        public string ResolveClient(string[] dirSegs)
        {
            string key = MatchClientKey(dirSegs);
            return key == null ? null : _clientCanonical[key];
        }

        /// <summary>
        /// Fills enable + client code ONLY when exactly one catalog row for the matched client has a
        /// file-name pattern that matches this file. Ambiguous or no match -&gt; false (left blank).
        /// </summary>
        public bool TryResolveEnable(string[] dirSegs, string fileName, out string enable, out string clientCode)
        {
            enable = "";
            clientCode = "";
            string key = MatchClientKey(dirSegs);
            List<FileEntry> list;
            if (key == null || !_entriesByClient.TryGetValue(key, out list)) return false;

            var hits = list.Where(e => e.FileNameRegex != null && e.FileNameRegex.IsMatch(fileName))
                           .Select(e => new KeyValuePair<string, string>(e.Enable, e.ClientCode))
                           .Distinct().ToList();
            if (hits.Count != 1) return false;                // none, or ambiguous -> leave blank
            enable = hits[0].Key;
            clientCode = hits[0].Value;
            return true;
        }

        // Map the file's folder segments to a workbook client key: exact match on any segment first,
        // then (if enabled) the longest "contains" match in either direction. Returns null if none.
        private string MatchClientKey(string[] dirSegs)
        {
            foreach (var seg in dirSegs)
            {
                string k = NormKey(seg);
                if (_clientCanonical.ContainsKey(k)) return k;
            }
            if (!_allowContains) return null;

            string best = null;
            int bestLen = -1;
            foreach (var seg in dirSegs)
            {
                string k = NormKey(seg);
                if (k.Length < MinContainsLen) continue;
                foreach (var ck in _clientKeys)
                {
                    if (ck.Length < MinContainsLen) continue;
                    if (k.Contains(ck) || ck.Contains(k))
                    {
                        if (ck.Length > bestLen) { bestLen = ck.Length; best = ck; }
                    }
                }
            }
            return best;
        }

        // --- helpers ----------------------------------------------------------------------------

        private static int FindHeaderRow(List<string[]> rows)
        {
            for (int i = 0; i < rows.Count && i < 10; i++)
                if (FindCol(rows[i], "planner label") >= 0 && FindCol(rows[i], "folder name") >= 0) return i;
            return -1;
        }

        private static int FindCol(string[] header, string contains)
        {
            for (int i = 0; i < header.Length; i++)
                if (header[i] != null && header[i].Trim().ToLowerInvariant().Contains(contains)) return i;
            return -1;
        }

        // The data file-name column. Most tabs head it "File Name" / "FILE NAME"; the MCD2 tab heads
        // it "_Ini". Either way, never the "Pre-Processed File Name" column.
        private static int FindFileNameCol(string[] header)
        {
            for (int i = 0; i < header.Length; i++)
            {
                string v = (header[i] ?? "").Trim().ToLowerInvariant();
                if (v.Contains("file name") && !v.Contains("pre-process") && !v.Contains("pre process") && !v.Contains("preprocess"))
                    return i;
            }
            for (int i = 0; i < header.Length; i++)
                if ((header[i] ?? "").Trim().Equals("_Ini", StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        private static string Cell(string[] row, int idx)
        {
            return (idx >= 0 && idx < row.Length && row[idx] != null) ? row[idx] : "";
        }

        private static string ParseSystem(string planner)
        {
            if (string.IsNullOrEmpty(planner)) return null;
            var m = Regex.Match(planner, "(MCD\\s*\\d|BOPC)", RegexOptions.IgnoreCase);
            return m.Success ? m.Value.ToUpperInvariant().Replace(" ", "") : null;
        }

        private static string LastSegment(string folder)
        {
            var parts = folder.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? folder : parts[parts.Length - 1].Trim();
        }

        private static string NormKey(string s)
        {
            if (s == null) return "";
            return Regex.Replace(s.Trim().ToUpperInvariant(), "\\s+", " ");
        }

        // Turn an Excel file-name pattern (e.g. "NEW_ALL_PDE_MMDDYY.txt ( REGULAR FILE )") into a
        // regex by stripping notes and replacing date placeholders / '#' runs / spaces with wildcards.
        // Best-effort: returns null for free-text cells like "USE INVENTORY FILE (PLACEMENT)".
        private static Regex BuildPatternRegex(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return null;
            string p = pattern.Trim();
            p = Regex.Replace(p, "\\([^)]*\\)", " ");          // ( REGULAR FILE )
            p = Regex.Replace(p, "\\[[^\\]]*\\]", " ");        // [notes]
            int dash = p.IndexOf(" - ", StringComparison.Ordinal);
            if (dash > 0) p = p.Substring(0, dash);             // trailing " - NOTES" comment
            p = Regex.Replace(p, "\\s+", " ").Trim();
            if (p.Length == 0) return null;

            string lower = p.ToLowerInvariant();
            if (lower.Contains("use inventory") || lower.StartsWith("see ")) return null;

            // Control-char sentinels survive Regex.Escape untouched, so we swap them for real regex
            // fragments afterwards. D8 = 8-digit date, D6 = 6-8 digit date, HASH = '#' run, SP = space.
            const string D8 = "", D6 = "", HASH = "", SP = "";
            p = Regex.Replace(p, "(?i)YYYYMMDD", D8);
            p = Regex.Replace(p, "(?i)MMDDYYYY", D8);
            p = Regex.Replace(p, "(?i)YYYMMDD", D8);            // a typo'd header variant seen in the sheet
            p = Regex.Replace(p, "(?i)MMDDYY", D6);
            p = Regex.Replace(p, "(?i)YYYYMM", D6);
            p = Regex.Replace(p, "(?i)YYMMDD", D6);
            p = p.Replace("#", HASH);
            p = p.Replace(" ", SP);

            string esc = Regex.Escape(p);
            esc = esc.Replace(D8, "\\d{8}").Replace(D6, "\\d{6,8}");
            esc = Regex.Replace(esc, "(" + HASH + ")+", "\\d+");
            esc = esc.Replace(SP, "\\s*");
            try { return new Regex("^" + esc + "$", RegexOptions.IgnoreCase); }
            catch { return null; }
        }
    }
}
