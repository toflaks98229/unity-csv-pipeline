using System;
using System.Collections.Generic;

namespace CsvPipeline
{
    /// <summary>
    /// One parsed table. It carries the <b>headers</b> alongside the rows,
    /// so a typo in a column name is caught instead of slipping through as an "empty cell".
    /// </summary>
    public sealed class CsvTable
    {
        private readonly HashSet<string> _headerSet;

        /// <summary>Builds a table.</summary>
        /// <param name="headers">List of headers (column names).</param>
        /// <param name="rows">Data rows.</param>
        /// <param name="defect">
        /// A parsing problem that makes the table untrustworthy. Null when there is none.
        /// </param>
        public CsvTable(IReadOnlyList<string> headers, IReadOnlyList<CsvRow> rows, string defect = null)
        {
            Headers = headers ?? Array.Empty<string>();
            Rows = rows ?? Array.Empty<CsvRow>();
            Defect = defect;

            // 헤더는 대소문자를 가리지 않습니다. 표는 PascalCase(MaxSpeed), 필드는 camelCase(maxSpeed)로
            // 적히는 것이 보통이라, 가려서 비교하면 자동 연결이 하나도 붙지 않습니다.
            _headerSet = new HashSet<string>(Headers, StringComparer.OrdinalIgnoreCase);

            DuplicateHeaders = FindDuplicateHeaders(Headers);
        }

        /// <summary>
        /// A parsing problem that makes this table untrustworthy. Null when there is none.
        /// <para>
        /// Today there is only one: an <b>unclosed double quote</b>. That single character swallows every
        /// following row into one field, so the table keeps only the front part and the remaining rows
        /// <b>do not even exist</b>. Baking reads them as "rows that disappeared from the table" and deletes
        /// their output assets. That kind of loss is not even counted as skipped and leaves no trace anywhere,
        /// so the table is treated as unreadable instead.
        /// </para>
        /// </summary>
        public string Defect { get; }

        /// <summary>
        /// Columns whose name appears more than once. Empty list when there are none.
        /// <para>
        /// The row dictionary stores values by name, so <b>a later column overwrites an earlier one.</b>
        /// Whatever was written in the earlier column survives nowhere, and there was no warning. Duplicating
        /// a column in a sheet produces this shape immediately, and so does a name that differs only in case —
        /// header matching ignores case.
        /// </para>
        /// </summary>
        public IReadOnlyList<string> DuplicateHeaders { get; }

        /// <summary>Finds columns whose name appears more than once. Keeps the order of appearance and reports each name once.</summary>
        /// <param name="headers">Header list to inspect.</param>
        /// <returns>Names of the duplicated columns.</returns>
        private static IReadOnlyList<string> FindDuplicateHeaders(IReadOnlyList<string> headers)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> duplicates = null;

            for (int i = 0; i < headers.Count; i++)
            {
                string name = headers[i];
                if (string.IsNullOrEmpty(name)) continue;

                if (seen.Add(name) || !reported.Add(name)) continue;

                duplicates = duplicates ?? new List<string>();
                duplicates.Add(name);
            }
            return (IReadOnlyList<string>)duplicates ?? Array.Empty<string>();
        }

        /// <summary>List of headers (column names). Keeps the order written in the table.</summary>
        public IReadOnlyList<string> Headers { get; }

        /// <summary>Data rows. The header row is not included.</summary>
        public IReadOnlyList<CsvRow> Rows { get; }

        /// <summary>Number of data rows.</summary>
        public int Count => Rows.Count;

        /// <summary>Whether the table has the given column. Case is ignored.</summary>
        /// <param name="column">Column name to check.</param>
        /// <returns>True when the column is present.</returns>
        public bool HasColumn(string column) => !string.IsNullOrEmpty(column) && _headerSet.Contains(column);

        /// <summary>
        /// Returns the required columns that the table does not have.
        /// </summary>
        /// <param name="required">Names of the columns that must be present.</param>
        /// <returns>Names of the missing columns. Empty list when all are present.</returns>
        public List<string> FindMissingColumns(IEnumerable<string> required)
        {
            var missing = new List<string>();
            if (required == null) return missing;

            foreach (string column in required)
            {
                if (!string.IsNullOrEmpty(column) && !HasColumn(column)) missing.Add(column);
            }
            return missing;
        }

        /// <summary>
        /// Returns the column name exactly as written in the table. It matches even when the case differs.
        /// Exporting uses it so the spelling a person wrote (<c>MaxSpeed</c>) is not replaced by the field name (<c>maxSpeed</c>).
        /// </summary>
        /// <param name="column">Column name to look up.</param>
        /// <returns>The name as written in the table, or null when it is absent.</returns>
        public string ResolveHeader(string column)
        {
            if (string.IsNullOrEmpty(column)) return null;

            for (int i = 0; i < Headers.Count; i++)
            {
                if (string.Equals(Headers[i], column, StringComparison.OrdinalIgnoreCase)) return Headers[i];
            }
            return null;
        }

        /// <summary>
        /// Finds a column whose name is nearly the same. Used to point out typos.
        /// <see cref="HasColumn"/> already absorbs case, so this looks at differences in spaces, underscores, and hyphens.
        /// </summary>
        /// <param name="column">Column name that was looked for.</param>
        /// <returns>The real column name that is nearly the same, or null when there is none.</returns>
        public string FindSimilarColumn(string column)
        {
            if (string.IsNullOrEmpty(column)) return null;

            string needle = Squash(column);
            if (needle.Length == 0) return null;

            for (int i = 0; i < Headers.Count; i++)
            {
                if (Squash(Headers[i]) == needle) return Headers[i];
            }
            return null;
        }

        /// <summary>Strips separator characters from a name and lowercases it, for comparison.</summary>
        /// <param name="name">Name to normalize.</param>
        /// <returns>Normalized name.</returns>
        private static string Squash(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;

            var text = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (c == ' ' || c == '_' || c == '-') continue;
                text.Append(char.ToLowerInvariant(c));
            }
            return text.ToString();
        }
    }
}
