namespace CsvPipeline
{
    /// <summary>
    /// <see cref="CsvImportDefinition"/>을 <c>AssetPostprocessor</c>의 정적 콜백에서 실행하는 진입점입니다.
    /// </summary>
    public static class CsvImport
    {
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
    }
}
