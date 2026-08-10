using System;
using UnityEditor;

namespace CsvPipeline
{
    /// <summary>
    /// 설정 에셋이 생기거나 사라지거나 옮겨지면 들고 있던 것을 버립니다.
    /// <b>이것이 없으면 캐시가 낡습니다.</b> 설정을 지웠는데 화면은 계속 있다고 말하거나,
    /// 새로 만들었는데 기본값으로 도는 상태가 됩니다.
    /// </summary>
    internal sealed class CsvPipelineSettingsHook : AssetPostprocessor
    {
        /// <summary>에셋 변화 통지에서 설정 에셋이 관련됐을 때만 캐시를 버립니다.</summary>
        /// <param name="imported">임포트된 에셋 경로들입니다.</param>
        /// <param name="deleted">삭제된 에셋 경로들입니다.</param>
        /// <param name="moved">이동된 에셋의 새 경로들입니다.</param>
        /// <param name="movedFrom">이동된 에셋의 이전 경로들입니다.</param>
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

        /// <summary>목록에 설정 에셋일 수 있는 경로가 있는지 여부입니다.</summary>
        /// <param name="paths">검사할 경로들입니다.</param>
        /// <param name="checkType">
        /// 지금 그 자리에 있는 에셋의 타입까지 확인할지 여부입니다.
        /// 삭제·이동 전 경로에는 확인할 대상이 없어 false 로 부릅니다.
        /// </param>
        /// <returns>관련됐으면 true입니다.</returns>
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
