using System;

namespace CsvPipeline
{
    /// <summary>
    /// Declares that this ScriptableObject is authored as one table. <b>No importer code is needed.</b>
    /// Saving the table creates and updates one asset of this type per row.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class CsvAssetAttribute : Attribute
    {
        /// <summary>Declares the link between a table and its output assets.</summary>
        /// <param name="fileName">File name of the source table, extension included. (e.g. Clues.csv)</param>
        /// <param name="idColumn">Column that becomes the asset name. Rows with an empty cell here are skipped.</param>
        public CsvAssetAttribute(string fileName, string idColumn)
        {
            FileName = fileName;
            IdColumn = idColumn;
        }

        /// <summary>File name of the source table.</summary>
        public string FileName { get; }

        /// <summary>Column that becomes the asset name.</summary>
        public string IdColumn { get; }

        /// <summary>
        /// Folder the baked assets go into. (e.g. <c>Assets/Data/Clues</c>)
        /// Leave it empty to use <b>a folder named after the type, next to the source table</b>. That
        /// follows the table if it moves, which suits distributed samples whose install path is unknown.
        /// </summary>
        public string OutputFolder { get; set; }

        /// <summary>
        /// Whether serialized fields are bound to columns of the same name automatically. On by default.
        /// Matching ignores case, so a <c>maxSpeed</c> field binds to a <c>MaxSpeed</c> column.
        /// Turn it off to bind only fields carrying <see cref="CsvColumnAttribute"/>.
        /// </summary>
        public bool AutoMap { get; set; } = true;

        /// <summary>
        /// Whether assets for rows that disappeared from the table are cleaned up. On by default.
        /// Even when on, <b>assets still referenced elsewhere are never deleted</b> — they are kept
        /// with a warning instead.
        /// </summary>
        public bool DeleteMissing { get; set; } = true;

        /// <summary>
        /// Whether cleanup matches assets by <b>path</b> rather than by <b>name</b>. By name is the default.
        /// <para>
        /// Turn this on when the output folder also holds assets of the same type that this table did not
        /// create. Matching by name would make those look like rows that disappeared, and they could be
        /// deleted. (They survive if something still references them, but that is not a thing to rely on.)
        /// </para>
        /// </summary>
        public bool ReconcileByPath { get; set; }
    }

    /// <summary>
    /// Binds a field to a particular column. Only needed when the name differs or the behaviour must change.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class CsvColumnAttribute : Attribute
    {
        /// <summary>Binds the field to a column.</summary>
        /// <param name="name">Column name. Leave it empty to use the field name as-is.</param>
        public CsvColumnAttribute(string name = null) { Name = name; }

        /// <summary>Column name. When empty, the field name is used.</summary>
        public string Name { get; }

        /// <summary>
        /// Whether this column must exist in the table. If it does not, nothing from the table is applied.
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Whether an empty cell <b>overwrites the existing value</b>. Off by default, which preserves it.
        /// Preserving is the default so that values authored in the inspector are not lost to an empty cell.
        /// </summary>
        public bool OverwriteWhenEmpty { get; set; }

        /// <summary>
        /// Delimiters used to split a list cell. Leave it empty for the defaults (<c>;</c> and <c>|</c>).
        /// </summary>
        public string Separators { get; set; }

        /// <summary>
        /// Limits the search for object references by name to this folder. Empty means the whole project.
        /// </summary>
        public string ReferenceFolder { get; set; }
    }

    /// <summary>
    /// This field is never bound to the table. (Use it to exclude a field on a type where
    /// <see cref="CsvAssetAttribute.AutoMap"/> is on.)
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class CsvIgnoreAttribute : Attribute
    {
    }
}
