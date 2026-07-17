using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Meduit.ShareNormalizer
{
    /// <summary>
    /// Pure path/filename heuristics used to bucket a file: whether to skip it (in-flight folder),
    /// which <c>System</c> and <c>Client</c> it belongs to, its <c>yyyy-MM</c> bucket, and a
    /// best-guess INPUT/OUTPUT <c>Kind</c>. Every result is a HINT only - the SME confirms later.
    /// </summary>
    internal sealed class Classifier
    {
        private readonly Config _cfg;
        private readonly FileListCatalog _catalog;   // may be null (no FILE_LIST.xlsx loaded)

        // Date tokens recognised in a filename, most specific first: YYYYMMDD, MMDDYYYY, MMDDYY.
        private static readonly string[] DatePatterns =
        {
            "(?<![0-9])(?<y>20[0-9]{2})(?<m>0[1-9]|1[0-2])(?<d>0[1-9]|[12][0-9]|3[01])(?![0-9])",
            "(?<![0-9])(?<m>0[1-9]|1[0-2])(?<d>0[1-9]|[12][0-9]|3[01])(?<y>20[0-9]{2})(?![0-9])",
            "(?<![0-9])(?<m>0[1-9]|1[0-2])(?<d>0[1-9]|[12][0-9]|3[01])(?<y>[0-9]{2})(?![0-9])"
        };

        // A folder segment that is just a 4-digit year, e.g. "2026".
        private static readonly Regex YearFolderRx = new Regex("^(19|20)[0-9]{2}$");

        // A bare 4-digit year token in a name (not part of a longer number), e.g. "2020" in "2020.zip".
        private static readonly Regex BareYearRx = new Regex("(?<![0-9])(19|20)[0-9]{2}(?![0-9])");

        public Classifier(Config cfg, FileListCatalog catalog)
        {
            _cfg = cfg;
            _catalog = catalog;
        }

        /// <summary>True when any folder segment matches a configured in-flight / working folder.</summary>
        public bool ShouldSkip(string[] dirSegs)
        {
            foreach (var seg in dirSegs)
                foreach (var skip in _cfg.SkipFolderNames)
                    if (seg.Equals(skip, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// System for a file: the FILE_LIST.xlsx catalog is the authority (the system is not in the
        /// folder path); fall back to a literal MCD1/MCD2/MCD3/BOPC token in the path, else UNKNOWN.
        /// </summary>
        public string ResolveSystem(string[] dirSegs)
        {
            if (_catalog != null)
            {
                string fromCatalog = _catalog.ResolveSystem(dirSegs);
                if (fromCatalog != null) return fromCatalog;
            }
            foreach (var seg in dirSegs)
                foreach (var tok in _cfg.SystemTokens)
                    if (Regex.IsMatch(seg, "\\b" + Regex.Escape(tok) + "\\b", RegexOptions.IgnoreCase))
                        return tok.ToUpperInvariant();
            return "UNKNOWN";
        }

        /// <summary>
        /// Client: the catalog's canonical folder name if a path segment matches; else the folder
        /// directly above a "processed" segment, else the top folder under the source.
        /// </summary>
        public string ResolveClient(string[] dirSegs)
        {
            if (_catalog != null)
            {
                string fromCatalog = _catalog.ResolveClient(dirSegs);
                if (fromCatalog != null) return fromCatalog;
            }
            for (int i = 0; i < dirSegs.Length; i++)
                if (dirSegs[i].Equals("processed", StringComparison.OrdinalIgnoreCase) && i >= 1)
                    return dirSegs[i - 1];
            return dirSegs.Length >= 1 ? dirSegs[0] : "UNKNOWN";
        }

        /// <summary>True when a folder segment is a bare 4-digit year; yields the year.</summary>
        public static bool TryYearFolder(string seg, out int year)
        {
            year = 0;
            if (seg == null) return false;
            seg = seg.Trim();
            if (!YearFolderRx.IsMatch(seg)) return false;
            year = int.Parse(seg);
            return true;
        }

        /// <summary>
        /// The file's year for the recency filter. In the default (modified) mode this is simply the
        /// last-modified year - the file's actual recency, regardless of what the name says. In
        /// filename mode it is a year folder, else a filename date, else last-modified (out-of-range
        /// years ignored).
        /// </summary>
        public int ResolveYear(string[] dirSegs, string fileName, DateTime lastWrite)
        {
            if (_cfg.YearFromModified) return lastWrite.Year;

            int deepest = -1;
            foreach (var seg in dirSegs)
            {
                int y;
                if (TryYearFolder(seg, out y) && IsSaneYear(y)) deepest = y;   // keep deepest sane year folder
            }
            if (deepest > 0) return deepest;

            int fy, fm;
            if (TryFilenameDate(fileName, out fy, out fm)) return fy;
            return lastWrite.Year;
        }

        /// <summary>
        /// The yyyy-MM bucket. Default (modified) mode: the file's Date Modified, except that if the
        /// filename or any folder segment names a year other than MinYear, the YEAR defaults to
        /// MinYear (so a stray older/newer year in a name or folder can never make a wrong-year
        /// folder). Filename mode: a filename date wins (out-of-range years ignored), else last-modified.
        /// </summary>
        public string ResolveYearMonth(string[] dirSegs, string fileName, DateTime lastWrite)
        {
            if (_cfg.YearFromModified)
            {
                int year = lastWrite.Year, month = lastWrite.Month;
                if (_cfg.MinYear > 0 && HasConflictingYear(fileName, dirSegs, _cfg.MinYear))
                    year = _cfg.MinYear;
                return string.Format(
    CultureInfo.InvariantCulture,
    "{0:D4}_{1:D2}",
    year,
    month);
            }

            int y, m;
            if (TryFilenameDate(fileName, out y, out m))
                return string.Format(
    CultureInfo.InvariantCulture,
    "{0:D4}_{1:D2}",
    y,
    m);
            return lastWrite.ToString(
    "yyyy_MM",
    CultureInfo.InvariantCulture);
        }

        // True when the filename or any folder segment names a year different from 'target' - i.e.
        // a year token (a full date's year, or a bare 4-digit year) that is not 'target'.
        private static bool HasConflictingYear(string fileName, string[] dirSegs, int target)
        {
            foreach (int y in YearsIn(Path.GetFileNameWithoutExtension(fileName)))
                if (y != target) return true;
            foreach (var seg in dirSegs)
                foreach (int y in YearsIn(seg))
                    if (y != target) return true;
            return false;
        }

        // Every year implied by a string: the year of any full date (MMDDYY / MMDDYYYY / YYYYMMDD)
        // plus any bare 4-digit year. Used only to detect a year that disagrees with MinYear.
        private static IEnumerable<int> YearsIn(string s)
        {
            if (string.IsNullOrEmpty(s)) yield break;
            foreach (var rx in DatePatterns)
                foreach (Match m in Regex.Matches(s, rx))
                {
                    string y = m.Groups["y"].Value;
                    if (y.Length == 2) y = "20" + y;
                    yield return int.Parse(y, CultureInfo.InvariantCulture);
                }
            foreach (Match m in BareYearRx.Matches(s))
                yield return int.Parse(m.Value, CultureInfo.InvariantCulture);
        }

        // Sane window for a parsed/folder year: 2000 .. (this year + 1). Files are recent, so a year
        // beyond next year is almost certainly a misread number in the filename.
        private const int MinSaneYear = 2000;
        private static int MaxSaneYear { get { return DateTime.UtcNow.Year + 1; } }
        private static bool IsSaneYear(int y) { return y >= MinSaneYear && y <= MaxSaneYear; }

        // First in-range date found in the filename, scanning each pattern's matches in order.
        private static bool TryFilenameDate(string fileName, out int year, out int month)
        {
            year = 0;
            month = 0;
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            foreach (var rx in DatePatterns)
            {
                foreach (Match m in Regex.Matches(baseName, rx))
                {
                    string y = m.Groups["y"].Value;
                    if (y.Length == 2) y = "20" + y;
                    int yi = int.Parse(y, CultureInfo.InvariantCulture);
                    if (!IsSaneYear(yi)) continue;
                    year = yi;
                    month = int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Triage flag: OUTPUT_CANDIDATE / INPUT_CANDIDATE / INPUT_MAYBE / UNKNOWN. A hint, not a filter -
        /// nothing is dropped on the strength of this.
        /// </summary>
        public string ResolveKind(string fileName)
        {
            string lower = fileName.ToLowerInvariant();
            foreach (string mk in _cfg.OutputFileMarkers)
{
    if (string.IsNullOrWhiteSpace(mk))
        continue;

    string token =
        mk.Trim().ToLowerInvariant();

    if (lower.Contains(token))
        return "OUTPUT_CANDIDATE";
}

            string ext = Path.GetExtension(lower);
            bool hasDate = Regex.IsMatch(fileName, "(?<![0-9])[0-9]{6}(?:[0-9]{2})?(?![0-9])");
            bool dataExt = _cfg.DataExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
            if (dataExt && hasDate) return "INPUT_CANDIDATE";
            if (dataExt) return "INPUT_MAYBE";
            return "UNKNOWN";
        }

        /// <summary>
        /// True when the file should NOT be copied (acknowledgment / message / output file). A plain
        /// pattern matches as a case-insensitive suffix (".ack.txt", ".msg"); a pattern containing
        /// '*' is a wildcard over the whole name (e.g. "*ack*").
        /// </summary>
        public bool IsExcluded(string fileName)
{
    if (string.IsNullOrWhiteSpace(fileName))
        return false;

    string lowerName =
        Path.GetFileName(fileName)
            .ToLowerInvariant();

    //
    // 1. Output markers
    //
    foreach (string marker in _cfg.OutputFileMarkers)
    {
        if (string.IsNullOrWhiteSpace(marker))
            continue;

        string token =
            marker.Trim().ToLowerInvariant();

        if (lowerName.Contains(token))
            return true;
    }

    //
    // 2. Exclude patterns
    //
    foreach (string pattern in _cfg.ExcludeFilePatterns)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            continue;

        string p =
            pattern.Trim().ToLowerInvariant();

        if (p.Contains("*"))
        {
            string regex =
                "^" +
                Regex.Escape(p)
                    .Replace("\\*", ".*") +
                "$";

            if (Regex.IsMatch(
                    lowerName,
                    regex,
                    RegexOptions.IgnoreCase))
            {
                return true;
            }
        }
        else
        {
            if (lowerName.EndsWith(
                    p,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string token =
                p.TrimStart('.');

            if (lowerName.Contains(token))
            {
                return true;
            }
        }
    }

    return false;
}

        public bool IsAllowedExtension(string fileName)
{
    string extension =
        Path.GetExtension(fileName);

    if (string.IsNullOrWhiteSpace(extension))
        return false;

    return _cfg.DataExtensions.Any(
        x => x.Equals(
            extension,
            StringComparison.OrdinalIgnoreCase));
}

        /// <summary>Sanitise a folder/client name into a safe single path segment.</summary>
        public static string SafeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "UNKNOWN";
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim().Trim('.');
        }
    }
}
