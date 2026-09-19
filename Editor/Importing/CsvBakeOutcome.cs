namespace CsvPipeline
{
    /// <summary>Kind of result from baking one unit.</summary>
    public enum CsvBakeKind
    {
        /// <summary>This unit was not applied.</summary>
        Skipped,

        /// <summary>A new asset was created.</summary>
        Created,

        /// <summary>An existing asset was updated.</summary>
        Updated,
    }

    /// <summary>
    /// Result of baking one unit of a table (a row or a group).
    /// <see cref="Name"/> and <see cref="Path"/> mark the output as <b>owned by this table run</b>,
    /// so the cleanup step treats only the output assets missing from this list as gone.
    /// </summary>
    public readonly struct CsvBakeOutcome
    {
        /// <summary>Kind of the result.</summary>
        public CsvBakeKind Kind { get; }

        /// <summary>Asset name settled by this run. Null when there is none.</summary>
        public string Name { get; }

        /// <summary>Asset path settled by this run. Null when there is none.</summary>
        public string Path { get; }

        /// <summary>
        /// Line number this unit came from. 0 when unknown.
        /// Telling <b>which row an identifier collided with</b> needs this value.
        /// </summary>
        public int Line { get; }

        /// <summary>Builds a result.</summary>
        /// <param name="kind">Kind of the result.</param>
        /// <param name="name">Settled asset name.</param>
        /// <param name="path">Settled asset path.</param>
        /// <param name="line">Line number this unit came from.</param>
        private CsvBakeOutcome(CsvBakeKind kind, string name, string path, int line)
        {
            Kind = kind;
            Name = name;
            Path = path;
            Line = line;
        }

        /// <summary>Result for a newly created asset.</summary>
        /// <param name="name">Asset name.</param>
        /// <param name="path">Asset path.</param>
        /// <param name="line">Line number this unit came from.</param>
        /// <returns>The result.</returns>
        public static CsvBakeOutcome Created(string name, string path = null, int line = 0)
            => new CsvBakeOutcome(CsvBakeKind.Created, name, path, line);

        /// <summary>Result for an updated asset.</summary>
        /// <param name="name">Asset name.</param>
        /// <param name="path">Asset path.</param>
        /// <param name="line">Line number this unit came from.</param>
        /// <returns>The result.</returns>
        public static CsvBakeOutcome Updated(string name = null, string path = null, int line = 0)
            => new CsvBakeOutcome(CsvBakeKind.Updated, name, path, line);

        /// <summary>Result for a unit that was not applied.</summary>
        /// <returns>The result.</returns>
        public static CsvBakeOutcome Skipped() => new CsvBakeOutcome(CsvBakeKind.Skipped, null, null, 0);

        /// <summary>Builds a created or updated result, picking the kind from a flag.</summary>
        /// <param name="created">True when the asset was newly created.</param>
        /// <param name="name">Asset name.</param>
        /// <param name="path">Asset path.</param>
        /// <param name="line">Line number this unit came from.</param>
        /// <returns>The result.</returns>
        public static CsvBakeOutcome Baked(bool created, string name, string path = null, int line = 0)
            => created ? Created(name, path, line) : Updated(name, path, line);
    }

    /// <summary>What an output asset missing from the table is matched against.</summary>
    public enum CsvReconcileMode
    {
        /// <summary>No cleanup. This table neither creates nor deletes output assets.</summary>
        None,

        /// <summary>Match against the asset name.</summary>
        ByName,

        /// <summary>
        /// Match against the asset path.
        /// Use it when the name alone cannot match, such as when derived types split into subfolders.
        /// </summary>
        ByPath,
    }
}
