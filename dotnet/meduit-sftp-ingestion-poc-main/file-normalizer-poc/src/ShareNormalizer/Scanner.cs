using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Meduit.ShareNormalizer
{
    /// <summary>
    /// Crawls each configured source root and, per file: skip in-flight folders -&gt; classify -&gt;
    /// recency filter -&gt; hash -&gt; dedup -&gt; copy into the normalized layout -&gt; append an inventory
    /// row. Strictly read-only on the source (copy, never move or delete).
    ///
    /// Output layout:
    ///   &lt;out&gt;\organized\&lt;System&gt;\&lt;Client&gt;\&lt;yyyy-MM&gt;\&lt;original-filename&gt;
    ///   &lt;out&gt;\inventory.csv        (one row per file seen, append-only)
    ///   &lt;out&gt;\copied-hashes.txt    (SHA-256 of every copied file, cross-run dedup record)
    /// </summary>
    internal sealed class Scanner
    {
        private sealed class Counts
{
    public int scanned;

    public int copied;

    public int dup;

    public int existed;

    public int inflight;

    public int old;

    public int excluded;

    public int nonInput;

    public int errors;

    public void IncrementScanned()
    {
        System.Threading.Interlocked.Increment(ref scanned);
    }

    public void IncrementCopied()
    {
        System.Threading.Interlocked.Increment(ref copied);
    }

    public void IncrementDuplicate()
    {
        System.Threading.Interlocked.Increment(ref dup);
    }

    public void IncrementExists()
    {
        System.Threading.Interlocked.Increment(ref existed);
    }

    public void IncrementInflight()
    {
        System.Threading.Interlocked.Increment(ref inflight);
    }

    public void IncrementOld()
    {
        System.Threading.Interlocked.Increment(ref old);
    }

    public void IncrementExcluded()
    {
        System.Threading.Interlocked.Increment(ref excluded);
    }

    public void IncrementNonInput()
    {
        System.Threading.Interlocked.Increment(ref nonInput);
    }

    public void IncrementErrors()
    {
        System.Threading.Interlocked.Increment(ref errors);
    }
}

        private readonly Config _cfg;
        private readonly Classifier _classifier;
        private readonly FileListCatalog _catalog;   // may be null (no FILE_LIST.xlsx loaded)
        private readonly Logger _log;
        private readonly string _organizedRoot;
        private readonly string _inventoryPath;
        private readonly string _seenPath;
        //private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, byte> _seen =
    new ConcurrentDictionary<string, byte>(
        StringComparer.OrdinalIgnoreCase);

        private readonly object _seenFileLock =
    new object();

        public Scanner(Config cfg, Logger log, FileListCatalog catalog)
        {
            _cfg = cfg;
            _log = log;
            _catalog = catalog;
            _classifier = new Classifier(cfg, catalog);

            _organizedRoot = Path.Combine(cfg.OutputRoot, "organized");
            _inventoryPath = Path.Combine(cfg.OutputRoot, "inventory.csv");
            _seenPath = Path.Combine(cfg.OutputRoot, "copied-hashes.txt");

            Directory.CreateDirectory(_organizedRoot);
            if (File.Exists(_seenPath))
                foreach (string h in File.ReadAllLines(_seenPath))
{
    if (!string.IsNullOrWhiteSpace(h))
    {
        _seen.TryAdd(h.Trim(), 0);
    }
}
        }

        public void RunOnce()
{
    _log.Log(
        string.Format(
            "SCAN start roots={0} dryRun={1} inputOnly={2} minYear={3}",
            _cfg.Sources.Count,
            _cfg.DryRun,
            _cfg.InputCandidatesOnly,
            _cfg.MinYear > 0
                ? _cfg.MinYear.ToString()
                : "(off)"));

    Counts counts =
        new Counts();

    using (InventoryWriter inventory =
        new InventoryWriter(_inventoryPath))
    {
        Parallel.ForEach(
            _cfg.Sources,

            new ParallelOptions
            {
                MaxDegreeOfParallelism =
                    _cfg.ScannerFolderThreads
            },

            source =>
            {
                ScanSource(
                    source,
                    inventory,
                    counts);
            });
    }

    _log.Log(
        string.Format(
        "SCAN done scanned={0} copied={1} dup={2} existed={3} skippedInflight={4} skippedOld={5} skippedExcluded={6} skippedNonInput={7} errors={8}",
        counts.scanned,
        counts.copied,
        counts.dup,
        counts.existed,
        counts.inflight,
        counts.old,
        counts.excluded,
        counts.nonInput,
        counts.errors));
}

        private void ScanSource(SourceSpec source, InventoryWriter inv, Counts c)
        {
            string src = Path.GetFullPath(source.Path).TrimEnd('\\', '/');
            _log.Log(string.Format("SCAN root   system={0,-7} '{1}'", source.System ?? "(auto)", src));

            //ConcurrentBag<string> files = new ConcurrentBag<string>();

            //    foreach (string file in SafeEnumerateFiles(src))
            //    {   files.Add(file); }

List<string> files =
    SafeEnumerateFiles(src).ToList();

Parallel.ForEach(

    files,

    new ParallelOptions
    {
        MaxDegreeOfParallelism =
            _cfg.ScannerFileThreads
    },

    path =>
    {
            
                FileInfo file;
                try { file = new FileInfo(path); } catch { c.IncrementErrors(); return; }

                string rel = path.Length > src.Length ? path.Substring(src.Length).TrimStart('\\', '/') : file.Name;
                string[] segs = rel.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                string[] dirSegs = segs.Length > 1 ? segs.Take(segs.Length - 1).ToArray() : new string[0];

                if (_classifier.ShouldSkip(dirSegs)) { c.IncrementInflight(); return; }
                c.IncrementScanned();

                string system = source.System ?? _classifier.ResolveSystem(dirSegs);
                string client = Classifier.SafeName(_classifier.ResolveClient(dirSegs));
                string kind = _classifier.ResolveKind(file.Name);

                //
// Allow only configured data extensions
//
if (!_classifier.IsAllowedExtension(file.Name))
{
    c.IncrementExcluded();

    inv.WriteRow(
        system,
        client,
        "",
        "",
        file,
        kind,
        "SKIPPED_EXTENSION",
        null,
        "");

    return;
}

//
// Skip output files
//

                // Exclusion: acknowledgment / message / output files we never copy (e.g. *.ack.txt, *.msg).
                if (_classifier.IsExcluded(file.Name))
                {
                    c.IncrementExcluded();
                    inv.WriteRow(system, client, "", "", file, kind, "SKIPPED_EXCLUDED", null, "");
                    return;
                }

                // Recency filter: drop anything older than MinYear (inventoried, not copied or hashed).
                if (_cfg.MinYear > 0 && _classifier.ResolveYear(dirSegs, file.Name, file.LastWriteTime) < _cfg.MinYear)
                {
                    c.IncrementOld();
                    inv.WriteRow(system, client, "", "", file, kind, "SKIPPED_OLD", null, "");
                    return;
                }

                string enable = "", clientCode = "";
                if (_catalog != null) _catalog.TryResolveEnable(dirSegs, file.Name, out enable, out clientCode);

                string sha = null, normalized = "", action;
                try { sha = HashUtil.Sha256File(path); }
                catch { c.IncrementErrors(); inv.WriteRow(system, client, enable, clientCode, file, kind, "ERROR_READ", null, ""); return; }

                // Duplicate content (same SHA-256 seen this run or a prior run): ignore entirely -
                // not copied, and not written to the inventory. Still counted in the run summary.
                if (!_seen.TryAdd(sha, 0)){ c.IncrementDuplicate(); return; }

                if (_cfg.InputCandidatesOnly && kind != "INPUT_CANDIDATE") { action = "SKIPPED_NONINPUT"; c.IncrementNonInput(); }
                else
                {
                    string ym = _classifier.ResolveYearMonth(dirSegs, file.Name, file.LastWriteTime);
                    string destDir = Path.Combine(_organizedRoot, system, client, ym);
                    normalized = Path.Combine(destDir, file.Name);
                    if (!_cfg.DryRun && File.Exists(normalized)) { action = "SKIPPED_EXISTS"; c.IncrementExists(); }
                    else if (_cfg.DryRun) { action = "WOULD_COPY"; c.IncrementCopied(); }
                    else
                    {
                        Directory.CreateDirectory(destDir);
                        File.Copy(path, normalized, overwrite: true);
                        //_seen.TryAdd(sha, 0);
                        lock (_seenFileLock)
{
    File.AppendAllText(
        _seenPath,
        sha + Environment.NewLine);
}
                        action = "COPIED"; c.IncrementCopied();
                    }
                }

                inv.WriteRow(system, client, enable, clientCode, file, kind, action, sha, normalized);
            

            });
        }

        // Manual recursion so a single unreadable folder (locked / denied) does not abort the scan.
        // Year folders older than MinYear are pruned here, so old trees are never even descended.
        private IEnumerable<string> SafeEnumerateFiles(string root)
        {
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                string dir = stack.Pop();
                string[] subs = null, files = null;
                try { subs = Directory.GetDirectories(dir); } catch { }
                try { files = Directory.GetFiles(dir); } catch { }
                if (files != null) foreach (var f in files) yield return f;
                if (subs == null) continue;
                foreach (var s in subs)
                {
                    // In filename mode, prune old year folders for speed. In modified mode the folder
                    // year is not authoritative (Date Modified is), so we descend and judge per file.
                    if (_cfg.MinYear > 0 && !_cfg.YearFromModified)
                    {
                        int y;
                        string leaf = Path.GetFileName(s.TrimEnd('\\', '/'));
                        if (Classifier.TryYearFolder(leaf, out y) && y < _cfg.MinYear) continue;   // prune old year
                    }
                    stack.Push(s);
                }
            }
        }
    }
}
