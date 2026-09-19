using System.Collections.Generic;
using System.Text;

namespace CsvPipeline
{
    /// <summary>Writer that builds table text. It wraps cells holding the delimiter, a quote, or a newline by the rules.</summary>
    public sealed class CsvWriter
    {
        private readonly StringBuilder _text = new StringBuilder();
        private readonly char _delimiter;

        /// <summary>Creates a writer.</summary>
        /// <param name="delimiter">Field delimiter.</param>
        public CsvWriter(char delimiter = CsvReader.Comma) { _delimiter = delimiter; }

        /// <summary>Writes one row.</summary>
        /// <param name="fields">Cells to write.</param>
        public void WriteRow(IReadOnlyList<string> fields)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0) _text.Append(_delimiter);
                _text.Append(Escape(fields[i]));
            }
            _text.Append('\n');
        }

        /// <summary>Everything written so far. Line endings are all LF.</summary>
        /// <returns>Table text.</returns>
        public override string ToString() => _text.ToString();

        /// <summary>
        /// Wraps in double quotes only when needed. A quote inside is escaped as <c>""</c>.
        /// </summary>
        /// <param name="value">Value to wrap.</param>
        /// <returns>Cell string ready to write into the table as is.</returns>
        private string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            bool needsQuotes = value.IndexOf(_delimiter) >= 0
                            || value.IndexOf('"') >= 0
                            || value.IndexOf('\n') >= 0
                            || value.IndexOf('\r') >= 0;

            if (!needsQuotes) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
