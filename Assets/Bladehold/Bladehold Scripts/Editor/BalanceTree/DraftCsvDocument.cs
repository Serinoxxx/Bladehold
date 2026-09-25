using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

/// <summary>
///     Lossless, in-memory copy of <c>DraftUpgrades.csv</c> for the Balance Tree Editor. Untouched rows are
///     written back byte for byte (their original line), and edited rows re-serialize only their own cells,
///     keeping each cell's original quoting. Line endings, BOM, trailing newline, blank lines and any extra
///     columns all survive, so a one-number edit is a one-number diff.
/// </summary>
public class DraftCsvDocument
{
    public const string DefaultPath = "Assets/Bladehold/Resources/DraftUpgrades.csv";

    public string Path { get; private set; }
    public string[] Header { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<DraftCsvRow> Rows => rows;
    public bool IsDirty => rows.Any(r => r.IsDirty);

    private readonly List<DraftCsvRow> rows = new List<DraftCsvRow>();
    private readonly Dictionary<string, int> columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    // Every physical line in file order. A null entry in lineRows means "keep the raw line" (header, blanks).
    private readonly List<string> rawLines = new List<string>();
    private readonly List<DraftCsvRow> lineRows = new List<DraftCsvRow>();
    private string newline = "\n";
    private bool trailingNewline = true;
    private bool hasBom;

    public static DraftCsvDocument Load(string path = DefaultPath)
    {
        var doc = new DraftCsvDocument { Path = path };
        if (!File.Exists(path)) return doc;

        byte[] bytes = File.ReadAllBytes(path);
        doc.hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        string text = new UTF8Encoding(false).GetString(bytes, doc.hasBom ? 3 : 0, bytes.Length - (doc.hasBom ? 3 : 0));

        doc.newline = text.Contains("\r\n") ? "\r\n" : "\n";
        doc.trailingNewline = text.EndsWith("\n");
        string body = doc.trailingNewline ? text.Substring(0, text.Length - doc.newline.Length) : text;
        string[] lines = body.Split(new[] { doc.newline }, StringSplitOptions.None);

        for (int i = 0; i < lines.Length; i++)
        {
            doc.rawLines.Add(lines[i]);
            if (i == 0)
            {
                doc.Header = DraftUpgradeService.ParseCsvRow(lines[i]).Select(h => h.Trim()).ToArray();
                for (int c = 0; c < doc.Header.Length; c++) doc.columnIndex[doc.Header[c]] = c;
                doc.lineRows.Add(null);
                continue;
            }

            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                doc.lineRows.Add(null);
                continue;
            }

            var row = new DraftCsvRow(doc, lines[i], i + 1);
            doc.rows.Add(row);
            doc.lineRows.Add(row);
        }

        return doc;
    }

    public int ColumnIndex(string column) => columnIndex.TryGetValue(column, out int i) ? i : -1;

    /// <summary>Writes only if something changed, then reimports so the runtime TextAsset picks it up.</summary>
    public bool Save()
    {
        if (!IsDirty) return false;

        var sb = new StringBuilder();
        for (int i = 0; i < rawLines.Count; i++)
        {
            DraftCsvRow row = lineRows[i];
            if (row != null && row.IsDirty)
            {
                rawLines[i] = row.Serialize();
                row.MarkClean(rawLines[i]);
            }
            sb.Append(rawLines[i]);
            if (i < rawLines.Count - 1 || trailingNewline) sb.Append(newline);
        }

        File.WriteAllText(Path, sb.ToString(), new UTF8Encoding(hasBom));
        AssetDatabase.ImportAsset(Path);
        return true;
    }
}

/// <summary>One data row of <see cref="DraftCsvDocument" />: raw cells, edited by column name.</summary>
public class DraftCsvRow
{
    public int LineNumber { get; }
    public string RawLine { get; private set; }
    public bool IsDirty { get; private set; }

    /// <summary>Cell count differs from the header. The runtime still reads it; the validator flags it.</summary>
    public bool IsMalformed => cells.Count != document.Header.Length;

    private readonly DraftCsvDocument document;
    private readonly List<string> cells = new List<string>();
    private readonly List<bool> quoted = new List<bool>();

    public DraftCsvRow(DraftCsvDocument document, string rawLine, int lineNumber)
    {
        this.document = document;
        LineNumber = lineNumber;
        RawLine = rawLine;
        cells.AddRange(DraftUpgradeService.ParseCsvRow(rawLine));
        quoted.AddRange(SplitQuotedFlags(rawLine));
    }

    public string Id => Get("id").Trim();

    public string Get(string column)
    {
        int i = document.ColumnIndex(column);
        return i >= 0 && i < cells.Count ? cells[i] : "";
    }

    /// <summary>Sets a cell. A no-op when the value is unchanged, so rows only go dirty on real edits.</summary>
    public void Set(string column, string value)
    {
        int i = document.ColumnIndex(column);
        if (i < 0) return;
        value ??= "";
        if (Get(column) == value) return;

        while (cells.Count <= i)
        {
            cells.Add("");
            quoted.Add(false);
        }
        cells[i] = value;
        IsDirty = true;
    }

    /// <summary>The row as the runtime parser sees it (the in-memory edits included).</summary>
    public string CurrentLine => IsDirty ? Serialize() : RawLine;

    public string Serialize()
    {
        var parts = new string[cells.Count];
        for (int i = 0; i < cells.Count; i++) parts[i] = Escape(cells[i], quoted[i]);
        return string.Join(",", parts);
    }

    internal void MarkClean(string newRaw)
    {
        RawLine = newRaw;
        IsDirty = false;
    }

    private static string Escape(string cell, bool wasQuoted)
    {
        bool needsQuotes = wasQuoted || cell.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        return needsQuotes ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell;
    }

    // Mirrors DraftUpgradeService.ParseCsvRow's cell boundaries, recording whether each cell was quoted.
    private static List<bool> SplitQuotedFlags(string line)
    {
        var flags = new List<bool>();
        bool inQuotes = false;
        bool cellQuoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') i++;
                else
                {
                    inQuotes = !inQuotes;
                    cellQuoted = true;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                flags.Add(cellQuoted);
                cellQuoted = false;
            }
        }
        flags.Add(cellQuoted);
        return flags;
    }
}
