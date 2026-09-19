using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CsvPipeline
{
    /// <summary>
    /// Lifecycle boilerplate shared by importers that key off a fixed file name.
    /// (File matching, touch detection, reading)
    /// </summary>
    public static class CsvImportUtil
    {
        /// <summary>Extensions treated as tables.</summary>
        public static readonly string[] TableExtensions = { ".csv", ".tsv", ".tab" };

        /// <summary>Whether the file name in the path matches the given file name, ignoring case.</summary>
        /// <param name="path">Asset path to check.</param>
        /// <param name="fileName">File name to compare against.</param>
        /// <returns>True when they match.</returns>
        public static bool IsFile(string path, string fileName)
            => Path.GetFileName(path).Equals(fileName, StringComparison.OrdinalIgnoreCase);

        /// <summary>Whether this import/move batch contains the given file.</summary>
        /// <param name="imported">Paths of the imported assets.</param>
        /// <param name="moved">New paths of the moved assets.</param>
        /// <param name="fileName">File name to look for.</param>
        /// <returns>True when it is included.</returns>
        public static bool Touched(string[] imported, string[] moved, string fileName)
            => imported.Concat(moved).Any(p => IsFile(p, fileName));

        /// <summary>Whether the path has an extension that is treated as a table.</summary>
        /// <param name="path">Path to check.</param>
        /// <returns>True for a table extension.</returns>
        public static bool IsTableFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            string ext = Path.GetExtension(path);
            foreach (string candidate in TableExtensions)
            {
                if (ext.Equals(candidate, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// Reads the raw text of a table file.
        /// It tries the TextAsset first and reads straight from disk when that fails.
        /// The fallback is needed because Unity does not import <c>.tsv</c> as a TextAsset.
        /// </summary>
        /// <param name="assetPath">Asset path to read.</param>
        /// <returns>The raw text, or null when it could not be read.</returns>
        public static string ReadText(string assetPath) => CsvAssets.Current.ReadText(assetPath);

        /// <summary>Reads a table file and parses it into a table complete with headers. The delimiter comes from the extension.</summary>
        /// <param name="assetPath">Asset path to read.</param>
        /// <returns>The parsed table, or null when there is nothing to read.</returns>
        public static CsvTable ReadTable(string assetPath) => ReadTable(assetPath, out _, out _);

        /// <summary>
        /// Reads a table file and parses it into a table, and reports <b>why</b> when no table came out.
        /// <para>
        /// Three different things all produce null — the file is missing, the characters cannot be decoded,
        /// or there are no data rows. Lumping the three into one sentence gives people guidance they cannot act
        /// on. Telling someone with a wrongly encoded table only "the table could not be read" leaves no way to
        /// know what to fix.
        /// </para>
        /// <para>
        /// <paramref name="unreadable"/> separates <b>what needs fixing</b> from <b>what is not filled in yet</b>.
        /// A table with no rows is authoring that has not started, so it is not an error, but a table whose
        /// characters cannot be decoded is a problem a person must fix and has to stand out.
        /// </para>
        /// </summary>
        /// <param name="assetPath">Asset path to read.</param>
        /// <param name="problem">Receives the reason no table came out. Null when a table did.</param>
        /// <param name="unreadable">Receives true when the file could not be opened or decoded.</param>
        /// <returns>The parsed table, or null when there is nothing to read.</returns>
        public static CsvTable ReadTable(string assetPath, out string problem, out bool unreadable)
        {
            unreadable = false;
            string text = CsvAssets.Current.ReadText(assetPath, out problem);

            if (problem != null)
            {
                unreadable = true;
                return null;
            }

            if (string.IsNullOrEmpty(text))
            {
                problem = "The table file is empty or cannot be found.";
                return null;
            }

            CsvTable table = CsvReader.ReadTable(text, CsvReader.DelimiterForPath(assetPath));

            // 파싱이 표를 내놓았어도 믿을 수 없는 경우가 있습니다. 닫히지 않은 따옴표가 뒤의 행을
            // 통째로 삼키면 앞부분만 남은 '멀쩡해 보이는' 표가 나오고, 굽기는 사라진 행의 산출물을
            // 지웁니다. 읽지 '못한' 것으로 다뤄 굽기를 아예 멈춥니다.
            if (table.Defect != null)
            {
                unreadable = true;
                problem = table.Defect;
                return null;
            }

            if (table.Count > 0) return table;

            problem = table.Headers.Count > 0
                ? "There is only a header line and no data rows."
                : "The table has no content at all.";
            return null;
        }

        /// <summary>Reads a table file and parses it into a list of rows. Null when loading fails or the file is empty.</summary>
        /// <param name="csvPath">Asset path to read.</param>
        /// <returns>The parsed list of rows, or null when there is nothing to read.</returns>
        public static List<Dictionary<string, object>> ReadRows(string csvPath)
        {
            string text = ReadText(csvPath);
            if (string.IsNullOrEmpty(text)) return null;

            var rows = CsvReader.Read(text, CsvReader.DelimiterForPath(csvPath));
            return (rows == null || rows.Count == 0) ? null : rows;
        }
    }
}
