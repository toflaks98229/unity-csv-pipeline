using System.Collections.Generic;

namespace CsvPipeline
{
    /// <summary>Kind of a planned change.</summary>
    public enum CsvChangeKind
    {
        /// <summary>Creates a new asset.</summary>
        Create,

        /// <summary>Changes the values of an existing asset.</summary>
        Update,

        /// <summary>Deletes an asset that is gone from the table.</summary>
        Delete,

        /// <summary>Preserves an asset that is gone from the table but is still referenced.</summary>
        Preserve,

        /// <summary>Skips the row without applying it.</summary>
        Skip
    }

    /// <summary>How one field changes.</summary>
    public sealed class CsvFieldChange
    {
        /// <summary>Name of the column the value comes from.</summary>
        public string Column;

        /// <summary>Name of the field that changes.</summary>
        public string Field;

        /// <summary>Current value.</summary>
        public string From;

        /// <summary>Value it changes to.</summary>
        public string To;
    }

    /// <summary>A change planned for one asset.</summary>
    public sealed class CsvPlannedChange
    {
        /// <summary>Kind of the change.</summary>
        public CsvChangeKind Kind;

        /// <summary>Path of the target asset. When it does not exist yet, the path it will be created at.</summary>
        public string AssetPath;

        /// <summary>Line number in the source table. 0 when unknown.</summary>
        public int Line;

        /// <summary>Human-readable note. (why it is skipped, why it is preserved, and so on)</summary>
        public string Note;

        /// <summary>Fields that change. Empty when the plan only went as far as paths.</summary>
        public List<CsvFieldChange> Fields = new List<CsvFieldChange>();

        /// <summary>Name to display. (the file name in the path)</summary>
        public string DisplayName =>
            string.IsNullOrEmpty(AssetPath) ? "(unnamed)" : System.IO.Path.GetFileNameWithoutExtension(AssetPath);
    }

    /// <summary>
    /// Precomputed answer to <b>what changes</b> if you bake the table now. It writes nothing.
    /// </summary>
    public sealed class CsvImportPlan
    {
        /// <summary>Creates a plan.</summary>
        /// <param name="fileName">File name of the source table.</param>
        /// <param name="label">Human-readable name of the target. (usually the asset type name)</param>
        public CsvImportPlan(string fileName, string label)
        {
            FileName = fileName;
            Label = label;
        }

        /// <summary>File name of the source table.</summary>
        public string FileName { get; }

        /// <summary>Human-readable name of the target.</summary>
        public string Label { get; }

        /// <summary>Output folder. null when it could not be determined.</summary>
        public string OutputFolder { get; set; }

        /// <summary>Planned changes.</summary>
        public List<CsvPlannedChange> Changes { get; } = new List<CsvPlannedChange>();

        /// <summary>Issues found while computing the plan.</summary>
        public List<CsvIssue> Issues { get; } = new List<CsvIssue>();

        /// <summary>Why no plan could be built. null when one was built.</summary>
        public string Unsupported { get; set; }

        /// <summary>Whether a plan could be built.</summary>
        public bool IsSupported => Unsupported == null;

        /// <summary>True when nothing changes at all.</summary>
        public bool IsNoOp => Count(CsvChangeKind.Create) == 0
                           && Count(CsvChangeKind.Update) == 0
                           && Count(CsvChangeKind.Delete) == 0;

        /// <summary>Number of changes of the given kind.</summary>
        /// <param name="kind">Kind to count.</param>
        /// <returns>The count.</returns>
        public int Count(CsvChangeKind kind)
        {
            int n = 0;
            foreach (CsvPlannedChange change in Changes)
            {
                if (change.Kind == kind) n++;
            }
            return n;
        }

        /// <summary>Adds one change.</summary>
        /// <param name="kind">Kind of the change.</param>
        /// <param name="assetPath">Path of the target asset.</param>
        /// <param name="line">Line number in the source.</param>
        /// <param name="note">Human-readable note.</param>
        /// <returns>The added change. You can go on to fill in its field list.</returns>
        public CsvPlannedChange Add(CsvChangeKind kind, string assetPath, int line = 0, string note = null)
        {
            var change = new CsvPlannedChange { Kind = kind, AssetPath = assetPath, Line = line, Note = note };
            Changes.Add(change);
            return change;
        }

        /// <summary>Summary in the form "created 3 / updated 12 / deleted 1".</summary>
        /// <returns>Summary string.</returns>
        public string Summary()
        {
            if (!IsSupported) return Unsupported;

            var parts = new List<string>(5);
            AppendCount(parts, "created", CsvChangeKind.Create);
            AppendCount(parts, "updated", CsvChangeKind.Update);
            AppendCount(parts, "deleted", CsvChangeKind.Delete);
            AppendCount(parts, "preserved", CsvChangeKind.Preserve);
            AppendCount(parts, "skipped", CsvChangeKind.Skip);

            return parts.Count == 0 ? "no changes" : string.Join(" / ", parts);
        }

        /// <summary>Appends the count to the summary when it is not zero.</summary>
        /// <param name="parts">Pieces of the summary.</param>
        /// <param name="label">Name to label it with.</param>
        /// <param name="kind">Kind to count.</param>
        private void AppendCount(List<string> parts, string label, CsvChangeKind kind)
        {
            int n = Count(kind);
            if (n > 0) parts.Add($"{label} {n}");
        }
    }
}
