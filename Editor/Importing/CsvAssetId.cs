using System;

namespace CsvPipeline
{
    /// <summary>
    /// Decides whether a table identifier <b>can be used as the output file name</b>.
    /// <para>
    /// The identifier becomes <c>{folder}/{identifier}.asset</c> as-is. So when it contains a character
    /// that a file name cannot hold, the asset is never created, and <b>that failure is recorded nowhere</b> —
    /// the identifier is not empty either, so it is not counted as skipped; the run takes the normal path
    /// and fails quietly down in the asset store.
    /// </para>
    /// <para>
    /// <b>It rejects instead of scrubbing.</b> Baking <c>Item/Sword</c> as <c>Item_Sword</c> splits the name
    /// written in the table from the asset name, and the next person reading that table is more confused,
    /// not less. The place to fix it is the table.
    /// </para>
    /// </summary>
    public static class CsvAssetId
    {
        /// <summary>
        /// Characters that no operating system accepts in a file name.
        /// <see cref="System.IO.Path.GetInvalidFileNameChars"/> is not used because that list <b>differs
        /// with the operating system it runs on</b>. Using it would let a table rejected on Windows pass on
        /// macOS, so the same table behaves differently from person to person.
        /// </summary>
        private const string Forbidden = "<>:\"/\\|?*";

        /// <summary>
        /// Length limit for a single file name. It is set low enough to leave room for the folder path
        /// and <c>.asset</c> on top.
        /// </summary>
        private const int MaxLength = 100;

        /// <summary>Names Windows reserves for devices. No file can carry them, even with an extension.</summary>
        private static readonly string[] Reserved =
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        /// <summary>
        /// The reason this identifier cannot be used as a file name, or null when it can.
        /// <b>Empty identifiers are filtered out by the caller first.</b> That case is not a table written
        /// wrong but a row left empty, and it needs different guidance.
        /// </summary>
        /// <param name="id">Identifier read from the table.</param>
        /// <returns>The rejection reason, or null when the identifier is usable.</returns>
        public static string Reject(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            // Note: identifiers read through CsvRow.GetString arrive already trimmed, so this branch
            // only fires for importers that build an id some other way. It stays because a custom
            // importer can concatenate columns, and trailing space there is invisible and destructive.
            if (id.Trim() != id)
            {
                return "It has leading or trailing whitespace, which is invisible and splits into a different "
                     + "identifier on the next import.";
            }

            foreach (char c in id)
            {
                if (c < ' ' || c == (char)0x7F) return "It contains an invisible control character.";
                if (Forbidden.IndexOf(c) >= 0) return $"'{c}' cannot be used in a file name.";
            }

            if (id[id.Length - 1] == '.')
            {
                return "It ends with a period, which Windows strips, producing a different name.";
            }

            if (IsReserved(id))
            {
                return "It is a name Windows reserves for devices, so no file can have it.";
            }

            if (id.Length > MaxLength)
            {
                return $"It is longer than {MaxLength} characters ({id.Length}). An over-long path cannot be created.";
            }

            return null;
        }

        /// <summary>
        /// Turns a rejection reason into one sentence a person can read. It lives here so the report and
        /// the preview say the same thing.
        /// </summary>
        /// <param name="id">The rejected identifier.</param>
        /// <param name="reason">The reason <see cref="Reject"/> returned.</param>
        /// <returns>The explanatory sentence.</returns>
        public static string Describe(string id, string reason)
            => $"Identifier '{id}' becomes the output file name as-is, and cannot be used as one. "
             + $"Skipping this row. {reason}";

        /// <summary>Checks for a reserved device name. Anything after a period counts as an extension, so only the part before it is examined.</summary>
        /// <param name="id">Identifier to check.</param>
        /// <returns>True when it is a reserved name.</returns>
        private static bool IsReserved(string id)
        {
            int dot = id.IndexOf('.');
            string stem = dot < 0 ? id : id.Substring(0, dot);

            foreach (string name in Reserved)
            {
                if (string.Equals(stem, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
