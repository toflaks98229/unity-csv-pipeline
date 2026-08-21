namespace CsvPipeline
{
    /// <summary>한 단위를 구운 결과의 종류입니다.</summary>
    public enum CsvBakeKind
    {
        /// <summary>이 단위를 반영하지 않았습니다.</summary>
        Skipped,

        /// <summary>에셋을 새로 만들었습니다.</summary>
        Created,

        /// <summary>이미 있던 에셋을 갱신했습니다.</summary>
        Updated,
    }

    /// <summary>
    /// 표의 한 단위(행 또는 그룹)를 구운 결과입니다.
    /// <see cref="Name"/>·<see cref="Path"/>는 <b>이번 표가 소유하고 있다</b>는 표시라,
    /// 정리 단계가 이 목록에 없는 산출물만 사라진 것으로 봅니다.
    /// </summary>
    public readonly struct CsvBakeOutcome
    {
        /// <summary>결과의 종류입니다.</summary>
        public CsvBakeKind Kind { get; }

        /// <summary>이번에 확정된 에셋 이름입니다. 없으면 null입니다.</summary>
        public string Name { get; }

        /// <summary>이번에 확정된 에셋 경로입니다. 없으면 null입니다.</summary>
        public string Path { get; }

        /// <summary>
        /// 이 단위가 온 줄 번호입니다. 모르면 0입니다.
        /// 식별자가 겹쳤을 때 <b>어느 행과 부딪쳤는지</b>를 말하려면 이 값이 필요합니다.
        /// </summary>
        public int Line { get; }

        /// <summary>결과를 만듭니다.</summary>
        /// <param name="kind">결과의 종류입니다.</param>
        /// <param name="name">확정된 에셋 이름입니다.</param>
        /// <param name="path">확정된 에셋 경로입니다.</param>
        /// <param name="line">이 단위가 온 줄 번호입니다.</param>
        private CsvBakeOutcome(CsvBakeKind kind, string name, string path, int line)
        {
            Kind = kind;
            Name = name;
            Path = path;
            Line = line;
        }

        /// <summary>새로 만든 결과입니다.</summary>
        /// <param name="name">에셋 이름입니다.</param>
        /// <param name="path">에셋 경로입니다.</param>
        /// <param name="line">이 단위가 온 줄 번호입니다.</param>
        /// <returns>결과입니다.</returns>
        public static CsvBakeOutcome Created(string name, string path = null, int line = 0)
            => new CsvBakeOutcome(CsvBakeKind.Created, name, path, line);

        /// <summary>갱신한 결과입니다.</summary>
        /// <param name="name">에셋 이름입니다.</param>
        /// <param name="path">에셋 경로입니다.</param>
        /// <param name="line">이 단위가 온 줄 번호입니다.</param>
        /// <returns>결과입니다.</returns>
        public static CsvBakeOutcome Updated(string name = null, string path = null, int line = 0)
            => new CsvBakeOutcome(CsvBakeKind.Updated, name, path, line);

        /// <summary>반영하지 않은 결과입니다.</summary>
        /// <returns>결과입니다.</returns>
        public static CsvBakeOutcome Skipped() => new CsvBakeOutcome(CsvBakeKind.Skipped, null, null, 0);

        /// <summary>새로 만들었거나 갱신한 결과를 종류만 정해 만듭니다.</summary>
        /// <param name="created">새로 만들었으면 true입니다.</param>
        /// <param name="name">에셋 이름입니다.</param>
        /// <param name="path">에셋 경로입니다.</param>
        /// <param name="line">이 단위가 온 줄 번호입니다.</param>
        /// <returns>결과입니다.</returns>
        public static CsvBakeOutcome Baked(bool created, string name, string path = null, int line = 0)
            => created ? Created(name, path, line) : Updated(name, path, line);
    }

    /// <summary>표에서 사라진 산출물을 무엇으로 대조할지입니다.</summary>
    public enum CsvReconcileMode
    {
        /// <summary>정리하지 않습니다. 이 표는 산출물을 만들지도 지우지도 않습니다.</summary>
        None,

        /// <summary>에셋 이름으로 대조합니다.</summary>
        ByName,

        /// <summary>
        /// 에셋 경로로 대조합니다.
        /// 파생 타입별로 하위 폴더가 갈리는 등 이름만으로는 대조할 수 없을 때 씁니다.
        /// </summary>
        ByPath,
    }
}
