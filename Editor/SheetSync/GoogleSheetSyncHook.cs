using System;
using UnityEditor;

namespace CsvPipeline
{
    /// <summary>
    /// Installs and removes the automatic pull loop as sync settings assets appear and disappear.
    /// Thanks to this, a project that does not use sheet sync keeps no per-frame callback at all.
    /// </summary>
    internal sealed class GoogleSheetSyncHook : AssetPostprocessor
    {
        /// <summary>Re-decides the loop only when an asset change notification involves a settings asset.</summary>
        /// <param name="imported">Paths of imported assets.</param>
        /// <param name="deleted">Paths of deleted assets.</param>
        /// <param name="moved">New paths of moved assets.</param>
        /// <param name="movedFrom">Previous paths of moved assets.</param>
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!TouchesAsset(imported) && !TouchesAsset(deleted) && !TouchesAsset(moved)) return;

            GoogleSheetSync.RefreshAutoPullHook();
        }

        /// <summary>Whether the list holds at least one <c>.asset</c> file.</summary>
        /// <param name="paths">Paths to inspect.</param>
        /// <returns>True when one is present.</returns>
        private static bool TouchesAsset(string[] paths)
        {
            if (paths == null) return false;

            foreach (string path in paths)
            {
                if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
