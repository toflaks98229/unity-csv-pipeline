using System;
using UnityEditor;

namespace CsvPipeline
{
    /// <summary>
    /// <see cref="CsvImportDefinition"/>을 <c>AssetPostprocessor</c>의 정적 콜백에서 실행하는 진입점입니다.
    /// </summary>
    public static class CsvImport
    {
        /// <summary>자동 임포트를 멈춘 <see cref="Suppress"/> 범위의 겹친 깊이입니다.</summary>
        private static int _suppressDepth;

        /// <summary>
        /// 멈춤 깊이가 남아 있지 않도록 편집 모드로 돌아올 때마다 풉니다.
        /// Domain Reload를 끈 프로젝트에서는 정적 값이 살아남는데, 이 깊이가 0으로 돌아오지 못하면
        /// <b>자동 임포트가 영영 멈춘 채</b>로 남고 사람은 이유를 알 수 없습니다.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void InstallSuppressionReset()
        {
            EditorApplication.playModeStateChanged -= ReleaseSuppressionOnEditMode;
            EditorApplication.playModeStateChanged += ReleaseSuppressionOnEditMode;
        }

        /// <summary>편집 모드로 돌아오면 남은 멈춤 깊이를 풉니다.</summary>
        /// <param name="change">플레이 모드 전이입니다.</param>
        private static void ReleaseSuppressionOnEditMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode) _suppressDepth = 0;
        }

        /// <summary>지금 자동 임포트가 멈춰 있는지 여부입니다.</summary>
        public static bool IsSuppressed => _suppressDepth > 0;

        /// <summary>
        /// 표가 임포트돼도 <b>자동으로 굽지 않게</b> 합니다. <c>using</c>으로 감싸 쓰십시오.
        /// 표를 여러 장 한꺼번에 써 넣을 때, 쓰는 도중마다 되굽는 낭비를 막습니다.
        /// 경로를 직접 넘기는 <see cref="CsvImportDefinition.Run"/>은 명시적 호출이라 영향을 받지 않습니다.
        /// </summary>
        /// <returns>범위를 벗어나면 자동 임포트를 되살리는 객체입니다.</returns>
        public static IDisposable Suppress() => new SuppressionScope();

        /// <summary>
        /// 임포터 정의를 실행합니다. <c>OnPostprocessAllAssets</c>에서 한 줄로 호출하십시오.
        /// </summary>
        /// <typeparam name="TDefinition">실행할 임포터 정의 타입입니다.</typeparam>
        /// <param name="imported">임포트된 에셋 경로들입니다.</param>
        /// <param name="deleted">삭제된 에셋 경로들입니다.</param>
        /// <param name="moved">이동된 에셋의 새 경로들입니다.</param>
        public static void Run<TDefinition>(string[] imported, string[] deleted, string[] moved)
            where TDefinition : CsvImportDefinition, new()
        {
            // 정의는 상태를 들고 있지 않으므로 매번 새로 만듭니다. 임포트 배치당 한 번뿐이라 비용이 없고,
            // 캐싱하면 도메인 리로드를 건너뛴 세션에서 낡은 상태가 남습니다.
            new TDefinition().Execute(imported, deleted, moved);
        }

        /// <summary>자동 임포트를 멈췄다가 되살리는 범위입니다.</summary>
        private sealed class SuppressionScope : IDisposable
        {
            private bool _released;

            /// <summary>범위에 들어갑니다.</summary>
            public SuppressionScope() => _suppressDepth++;

            /// <summary>범위를 벗어납니다. 예외로 빠져나가도 반드시 되살아납니다.</summary>
            public void Dispose()
            {
                if (_released) return;

                _released = true;
                if (_suppressDepth > 0) _suppressDepth--;
            }
        }
    }
}
