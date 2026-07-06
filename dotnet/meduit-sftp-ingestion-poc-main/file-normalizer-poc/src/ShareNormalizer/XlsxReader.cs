using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Meduit.ShareNormalizer
{
    /// <summary>One worksheet: its tab name and its rows (ragged, indexed by column).</summary>
    internal sealed class XlsxSheet
    {
        public string Name = "";
        public List<string[]> Rows = new List<string[]>();
    }

    /// <summary>
    /// Minimal read-only OOXML (.xlsx) reader using ONLY in-box assemblies - System.IO.Compression
    /// (the .xlsx is a zip) and System.Xml.Linq (the parts are XML). No NuGet, so it keeps the
    /// single self-contained exe. Resolves shared strings and returns each worksheet as a list of
    /// string rows. Enough to read a tabular "file list" workbook; not a general spreadsheet engine.
    /// </summary>
    internal static class XlsxReader
    {
        private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

        public static List<XlsxSheet> Read(string path)
        {
            var result = new List<XlsxSheet>();
            using (var fs = File.OpenRead(path))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                string[] shared = LoadSharedStrings(zip);
                var relMap = LoadRels(zip);                  // rId -> "worksheets/sheetN.xml"
                foreach (var s in LoadSheetList(zip))        // (name, rId) in workbook order
                {
                    string target;
                    if (s.Value == null || !relMap.TryGetValue(s.Value, out target)) continue;
                    var entry = zip.GetEntry("xl/" + target.TrimStart('/'));
                    if (entry == null) continue;
                    result.Add(new XlsxSheet { Name = s.Key, Rows = ReadSheet(entry, shared) });
                }
            }
            return result;
        }

        private static string[] LoadSharedStrings(ZipArchive zip)
        {
            var e = zip.GetEntry("xl/sharedStrings.xml");
            if (e == null) return new string[0];
            XDocument doc;
            using (var st = e.Open()) doc = XDocument.Load(st);
            return doc.Root.Elements(Main + "si").Select(SiText).ToArray();
        }

        // <si> is either a single <t> or a run of <r><t>..</t></r> segments.
        private static string SiText(XElement si)
        {
            var t = si.Element(Main + "t");
            if (t != null) return t.Value;
            return string.Concat(si.Elements(Main + "r").Select(r => (string)r.Element(Main + "t")));
        }

        private static Dictionary<string, string> LoadRels(ZipArchive zip)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var e = zip.GetEntry("xl/_rels/workbook.xml.rels");
            if (e == null) return map;
            XDocument doc;
            using (var st = e.Open()) doc = XDocument.Load(st);
            foreach (var r in doc.Root.Elements(PkgRel + "Relationship"))
                map[(string)r.Attribute("Id")] = (string)r.Attribute("Target");
            return map;
        }

        private static List<KeyValuePair<string, string>> LoadSheetList(ZipArchive zip)
        {
            var list = new List<KeyValuePair<string, string>>();
            var e = zip.GetEntry("xl/workbook.xml");
            if (e == null) return list;
            XDocument doc;
            using (var st = e.Open()) doc = XDocument.Load(st);
            var sheets = doc.Root.Element(Main + "sheets");
            if (sheets == null) return list;
            foreach (var sh in sheets.Elements(Main + "sheet"))
                list.Add(new KeyValuePair<string, string>((string)sh.Attribute("name"), (string)sh.Attribute(Rel + "id")));
            return list;
        }

        private static List<string[]> ReadSheet(ZipArchiveEntry entry, string[] shared)
        {
            var rows = new List<string[]>();
            XDocument doc;
            using (var st = entry.Open()) doc = XDocument.Load(st);
            var data = doc.Root.Element(Main + "sheetData");
            if (data == null) return rows;
            foreach (var row in data.Elements(Main + "row"))
            {
                var cells = new Dictionary<int, string>();
                int maxCol = -1;
                foreach (var c in row.Elements(Main + "c"))
                {
                    int col = ColIndex((string)c.Attribute("r"));
                    if (col < 0) continue;
                    cells[col] = CellValue(c, shared);
                    if (col > maxCol) maxCol = col;
                }
                var arr = new string[maxCol + 1];
                for (int i = 0; i <= maxCol; i++) arr[i] = cells.ContainsKey(i) ? cells[i] : "";
                rows.Add(arr);
            }
            return rows;
        }

        private static string CellValue(XElement c, string[] shared)
        {
            string t = (string)c.Attribute("t");
            if (t == "s")
            {
                var v = c.Element(Main + "v");
                int idx;
                if (v != null && int.TryParse(v.Value, out idx) && idx >= 0 && idx < shared.Length) return shared[idx];
                return "";
            }
            if (t == "inlineStr")
            {
                var inl = c.Element(Main + "is");
                return inl == null ? "" : string.Concat(inl.Descendants(Main + "t").Select(x => x.Value));
            }
            var vv = c.Element(Main + "v");
            return vv == null ? "" : vv.Value;
        }

        // "B7" -> zero-based column index 1. Letters only; stops at the first digit.
        private static int ColIndex(string cellRef)
        {
            if (string.IsNullOrEmpty(cellRef)) return -1;
            int n = 0;
            foreach (char ch in cellRef)
            {
                if (ch >= 'A' && ch <= 'Z') n = n * 26 + (ch - 'A' + 1);
                else if (ch >= 'a' && ch <= 'z') n = n * 26 + (ch - 'a' + 1);
                else break;
            }
            return n - 1;
        }
    }
}
