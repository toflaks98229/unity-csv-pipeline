using System.IO;
using System.Text;

namespace CsvPipeline
{
    /// <summary>
    /// Keeps a snapshot of what the last pull wrote.
    /// This snapshot is what lets the tool <b>tell "the sheet changed" apart from "someone edited
    /// the local file by hand"</b>.
    /// It lives outside version control, so everything still works without it — only the warning goes away.
    /// </summary>
    public static class SheetSnapshot
    {
        /// <summary>Folder that holds the snapshots.</summary>
        private static string Root => CsvPipelineSettings.Instance.SnapshotFolder;

        /// <summary>Makes sure the snapshot folder exists.</summary>
        public static void EnsureFolder() => Directory.CreateDirectory(Root);

        /// <summary>Snapshot path for the given table.</summary>
        /// <param name="csvFileName">Table file name.</param>
        /// <returns>Path to the snapshot file.</returns>
        public static string PathFor(string csvFileName) => Path.Combine(Root, csvFileName);

        /// <summary>
        /// Whether the local file was edited directly since the last sync.
        /// With no snapshot there is <b>nothing to judge against, so this is false</b>.
        /// A missing snapshot is not treated as an edit.
        /// </summary>
        /// <param name="csvFileName">Table file name.</param>
        /// <param name="localText">Normalized content of the local file right now.</param>
        /// <returns>True when there are signs of an edit.</returns>
        public static bool DivergedFromLocal(string csvFileName, string localText)
        {
            string path = PathFor(csvFileName);
            if (!File.Exists(path)) return false;

            return SheetDiff.Normalize(File.ReadAllText(path)) != localText;
        }

        /// <summary>
        /// Writes what this pull fetched as the snapshot.
        /// <para>
        /// <b>This is the one place that never writes a BOM.</b> The file lives under Library and nobody
        /// opens it — the next pull reads it only to decide whether the local file was edited. If it
        /// followed the setting instead, flipping the BOM setting would flag every table as
        /// "edited locally" the moment it changed.
        /// </para>
        /// </summary>
        /// <param name="csvFileName">Table file name.</param>
        /// <param name="text">Content to write.</param>
        public static void Write(string csvFileName, string text)
            => File.WriteAllText(PathFor(csvFileName), text, new UTF8Encoding(false));
    }
}
