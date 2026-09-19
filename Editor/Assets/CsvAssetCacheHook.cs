using UnityEditor;

namespace CsvPipeline
{
    /// <summary>
    /// Makes the gateway throw away what it holds whenever an asset changes.
    /// <para>
    /// A reference scan asks the whole project, so its result is held. When that answer goes stale, the tool
    /// <b>misses a newly created reference and deletes the asset</b> — the accident this tool works hardest
    /// to avoid. So it throws everything away without looking at which asset changed. The cost of throwing it
    /// away is one more lookup next time; the cost of missing one cannot be undone.
    /// </para>
    /// </summary>
    internal sealed class CsvAssetCacheHook : AssetPostprocessor
    {
        /// <summary>Takes the asset-change notification and throws away what is held.</summary>
        /// <param name="imported">Paths of the imported assets.</param>
        /// <param name="deleted">Paths of the deleted assets.</param>
        /// <param name="moved">New paths of the moved assets.</param>
        /// <param name="movedFrom">Previous paths of the moved assets.</param>
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool touched = (imported != null && imported.Length > 0)
                        || (deleted != null && deleted.Length > 0)
                        || (moved != null && moved.Length > 0);

            if (touched) CsvAssets.InvalidateCaches();
        }
    }
}
