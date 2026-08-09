using System;
using UnityEditor;

namespace CsvPipeline
{
    /// <summary>
    /// 연동 설정 에셋이 생기거나 사라질 때 자동 받기 루프를 걸고 뗍니다.
    /// 이 덕분에 시트 연동을 쓰지 않는 프로젝트에는 매 프레임 콜백이 아예 남지 않습니다.
    /// </summary>
    internal sealed class GoogleSheetSyncHook : AssetPostprocessor
    {
        /// <summary>에셋 변화 통지에서 설정 에셋이 관련됐을 때만 루프를 다시 판정합니다.</summary>
        /// <param name="imported">임포트된 에셋 경로들입니다.</param>
        /// <param name="deleted">삭제된 에셋 경로들입니다.</param>
        /// <param name="moved">이동된 에셋의 새 경로들입니다.</param>
        /// <param name="movedFrom">이동된 에셋의 이전 경로들입니다.</param>
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!TouchesAsset(imported) && !TouchesAsset(deleted) && !TouchesAsset(moved)) return;

            GoogleSheetSync.RefreshAutoPullHook();
        }

        /// <summary>목록에 <c>.asset</c> 파일이 하나라도 있는지 여부입니다.</summary>
        /// <param name="paths">검사할 경로들입니다.</param>
        /// <returns>있으면 true입니다.</returns>
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
