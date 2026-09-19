using System;
using UnityEditor;

namespace CsvPipeline
{
    /// <summary>
    /// Drops what is held whenever a settings asset appears, disappears, or moves.
    /// <b>Without this the cache goes stale.</b> You delete the settings and the page keeps saying they are there,
    /// or you create them and the pipeline keeps running on defaults.
    /// </summary>
    internal sealed class CsvPipelineSettingsHook : AssetPostprocessor
    {
        /// <summary>Drops the cache only when an asset change notification involves a settings asset.</summary>
        /// <param name="imported">Paths of the imported assets.</param>
        /// <param name="deleted">Paths of the deleted assets.</param>
        /// <param name="moved">New paths of the moved assets.</param>
        /// <param name="movedFrom">Previous paths of the moved assets.</param>
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            // 삭제와 이동은 경로만 남습니다. 그 자리에 무엇이 있었는지 물어볼 수 없으므로
            // 확장자만 보고 넉넉히 버립니다. 버리는 값은 다음 조회 한 번이라 싸고,
            // 놓치면 화면이 없는 설정을 있다고 말하게 되어 비쌉니다.
            if (TouchesSettings(imported, checkType: true)
             || TouchesSettings(deleted, checkType: false)
             || TouchesSettings(moved, checkType: true)
             || TouchesSettings(movedFrom, checkType: false))
            {
                CsvPipelineSettings.InvalidateCache();
            }
        }

        /// <summary>Whether the list contains a path that could be a settings asset.</summary>
        /// <param name="paths">Paths to inspect.</param>
        /// <param name="checkType">
        /// Whether to also check the type of the asset sitting at that path right now.
        /// A deleted path, or the path an asset moved away from, has nothing left to check, so it is called with false.
        /// </param>
        /// <returns>true when a settings asset is involved.</returns>
        private static bool TouchesSettings(string[] paths, bool checkType)
        {
            if (paths == null) return false;

            foreach (string path in paths)
            {
                if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) continue;
                if (!checkType) return true;

                if (AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(CsvPipelineSettings)) return true;
            }
            return false;
        }
    }
}
