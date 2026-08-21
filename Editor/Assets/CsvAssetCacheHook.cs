using UnityEditor;

namespace CsvPipeline
{
    /// <summary>
    /// 에셋이 바뀌면 게이트웨이가 들고 있던 것을 버리게 합니다.
    /// <para>
    /// 참조 조사는 프로젝트 전체를 묻는 일이라 결과를 들고 있습니다. 그 답이 낡으면
    /// <b>새로 생긴 참조를 못 보고 지우게</b> 됩니다 — 이 도구가 가장 피하려는 사고입니다.
    /// 그래서 어느 에셋이 바뀌었는지 가리지 않고 통째로 버립니다. 버리는 값은 다음 조회 한 번뿐이고,
    /// 놓치면 되돌릴 수 없습니다.
    /// </para>
    /// </summary>
    internal sealed class CsvAssetCacheHook : AssetPostprocessor
    {
        /// <summary>에셋 변화 통지를 받아 들고 있던 것을 버립니다.</summary>
        /// <param name="imported">임포트된 에셋 경로들입니다.</param>
        /// <param name="deleted">삭제된 에셋 경로들입니다.</param>
        /// <param name="moved">이동된 에셋의 새 경로들입니다.</param>
        /// <param name="movedFrom">이동된 에셋의 이전 경로들입니다.</param>
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool touched = (imported != null && imported.Length > 0)
                        || (deleted != null && deleted.Length > 0)
                        || (moved != null && moved.Length > 0);

            if (touched) CsvAssets.InvalidateCaches();
        }
    }
}
