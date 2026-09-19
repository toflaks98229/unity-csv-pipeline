using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CsvPipeline
{
    /// <summary>
    /// Wraps one row parsed by <see cref="CsvReader"/>, gathering cell access, type parsing, and list splitting in one place.
    /// </summary>
    public readonly struct CsvRow
    {
        /// <summary>Separators used to split list cells (effects, conditions, children, and so on). They avoid clashing with the CSV ','.</summary>
        public static readonly char[] ListSeparators = { ';', '|' };

        private readonly Dictionary<string, object> _cells;
        private readonly IReadOnlyList<string> _headers;

        /// <summary>Wraps a parsed row dictionary.</summary>
        /// <param name="cells">Header-to-value pairs.</param>
        public CsvRow(Dictionary<string, object> cells)
        {
            _cells = cells;
            _headers = null;
            LineNumber = 0;
        }

        /// <summary>Wraps a parsed row together with its source position and the table headers.</summary>
        /// <param name="cells">Header-to-value pairs.</param>
        /// <param name="lineNumber">Line number in the source file. (1-based, the header is line 1)</param>
        /// <param name="headers">Header list of the whole table. Used to decide whether a column exists.</param>
        public CsvRow(Dictionary<string, object> cells, int lineNumber, IReadOnlyList<string> headers)
        {
            _cells = cells;
            _headers = headers;
            LineNumber = lineNumber;
        }

        /// <summary>Line number in the source file. Zero when unknown. Used to put a location on error messages.</summary>
        public int LineNumber { get; }

        /// <summary>
        /// Whether this row has a cell under the given key.
        /// A row whose trailing cells are cut off returns false even when the column exists in the table.
        /// Use <see cref="HasColumn"/> to ask about the column itself.
        /// Rows built by <see cref="CsvReader.ReadTable(string)"/> ignore case.
        /// </summary>
        /// <param name="key">Header name to check.</param>
        /// <returns>True when this row has that cell.</returns>
        public bool Has(string key) => _cells != null && _cells.ContainsKey(key);

        /// <summary>
        /// Whether the table <b>header</b> holds the given column. Whether the cell is empty does not matter.
        /// Rows built without header information fall back to <see cref="Has"/>.
        /// </summary>
        /// <param name="key">Column name to check.</param>
        /// <returns>True when the column exists.</returns>
        public bool HasColumn(string key)
        {
            if (_headers == null) return Has(key);
            for (int i = 0; i < _headers.Count; i++)
            {
                if (string.Equals(_headers[i], key, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>Every header (column name) of this row. For importers that walk dynamic columns.</summary>
        public IEnumerable<string> Keys => _cells != null ? _cells.Keys : Enumerable.Empty<string>();

        /// <summary>
        /// Returns the cell value as a string with surrounding whitespace trimmed. Empty string when absent.
        /// <para>
        /// Rows built by <see cref="CsvReader.ReadTable(string)"/> hold <b>the raw cell text</b>, so there is
        /// nothing to undo here. <c>007</c> comes out as <c>007</c>, and <c>1.10</c> as <c>1.10</c>.
        /// </para>
        /// <para>
        /// The numeric branches below exist for rows built by the legacy <see cref="CsvReader.Read(string)"/>.
        /// That path stores boxed int/float, so they are formatted culture-independently (InvariantCulture)
        /// to stay consistent with the <see cref="int.TryParse(string,out int)"/>/<c>float.TryParse</c> calls underneath.
        /// </para>
        /// </summary>
        /// <param name="key">Header name to read.</param>
        /// <returns>Cell string.</returns>
        public string GetString(string key)
        {
            if (_cells == null || !_cells.TryGetValue(key, out object o) || o == null) return string.Empty;
            switch (o)
            {
                case float f: return f.ToString(CultureInfo.InvariantCulture);
                case int n: return n.ToString(CultureInfo.InvariantCulture);
                default: return o.ToString().Trim();
            }
        }

        /// <summary>Returns true and the value when the cell is not empty.</summary>
        /// <param name="key">Header name to read.</param>
        /// <param name="value">Value that was read.</param>
        /// <returns>True when it is not empty.</returns>
        public bool TryGetString(string key, out string value)
        {
            value = GetString(key);
            return !string.IsNullOrEmpty(value);
        }

        /// <summary>Parses the cell as a culture-independent int. Falls back on failure.</summary>
        /// <param name="key">Header name to read.</param>
        /// <param name="fallback">Value to use when parsing fails.</param>
        /// <returns>Parsed value.</returns>
        public int GetInt(string key, int fallback = 0)
            => int.TryParse(GetString(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

        /// <summary>Parses the cell as a culture-independent float. Falls back on failure.</summary>
        /// <param name="key">Header name to read.</param>
        /// <param name="fallback">Value to use when parsing fails.</param>
        /// <returns>Parsed value.</returns>
        public float GetFloat(string key, float fallback = 0f)
            => float.TryParse(GetString(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;

        /// <summary>Returns true and the value when the cell exists and parses as an int. For importers that assign fields directly: an absent column preserves the existing value.</summary>
        /// <param name="key">Header name to read.</param>
        /// <param name="value">Parsed value.</param>
        /// <returns>True when parsing succeeds.</returns>
        public bool TryGetInt(string key, out int value)
            => int.TryParse(GetString(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        /// <summary>Returns true and the value when the cell exists and parses as a float. For importers that assign fields directly: an absent column preserves the existing value.</summary>
        /// <param name="key">Header name to read.</param>
        /// <param name="value">Parsed value.</param>
        /// <returns>True when parsing succeeds.</returns>
        public bool TryGetFloat(string key, out float value)
            => float.TryParse(GetString(key), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        /// <summary>Reads "TRUE"/"1" as true and "FALSE"/"0" as false. Anything else, or an empty cell, falls back.</summary>
        /// <param name="key">Header name to read.</param>
        /// <param name="fallback">Value to use when the cell cannot be read.</param>
        /// <returns>Value that was read.</returns>
        public bool GetBool(string key, bool fallback = false)
        {
            string raw = GetString(key);
            if (string.IsNullOrEmpty(raw)) return fallback;
            if (raw.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || raw == "1") return true;
            if (raw.Equals("FALSE", StringComparison.OrdinalIgnoreCase) || raw == "0") return false;
            return fallback;
        }

        /// <summary>Returns a list cell separated by ';'/'|' as an array of trimmed tokens. Empty tokens are dropped.</summary>
        /// <param name="key">Header name to read.</param>
        /// <returns>Array of tokens.</returns>
        public string[] GetList(string key) => SplitList(GetString(key), ListSeparators);

        /// <summary>Splits a list cell with the given separators. Empty tokens are dropped.</summary>
        /// <param name="key">Header name to read.</param>
        /// <param name="separators">Separators to use.</param>
        /// <returns>Array of tokens.</returns>
        public string[] GetList(string key, params char[] separators)
            => SplitList(GetString(key), separators != null && separators.Length > 0 ? separators : ListSeparators);

        /// <summary>Splits raw text on the separators into an array of trimmed tokens.</summary>
        /// <param name="raw">Raw cell text.</param>
        /// <param name="separators">Separators to use.</param>
        /// <returns>Array of tokens.</returns>
        public static string[] SplitList(string raw, char[] separators)
        {
            return string.IsNullOrEmpty(raw)
                ? Array.Empty<string>()
                : raw.Split(separators, StringSplitOptions.RemoveEmptyEntries)
                     .Select(s => s.Trim())
                     .Where(s => s.Length > 0)
                     .ToArray();
        }

        /// <summary>Whether this cell contains a list separator.</summary>
        /// <param name="key">Header name to check.</param>
        /// <returns>True when a separator is present.</returns>
        public bool LooksLikeList(string key)
        {
            string raw = GetString(key);
            return !string.IsNullOrEmpty(raw) && raw.IndexOfAny(ListSeparators) >= 0;
        }
    }
}
