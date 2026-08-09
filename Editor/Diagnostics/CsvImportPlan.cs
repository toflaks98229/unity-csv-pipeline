using System.Collections.Generic;

namespace CsvPipeline
{
    /// <summary>계획된 변경의 종류입니다.</summary>
    public enum CsvChangeKind
    {
        /// <summary>새로 만듭니다.</summary>
        Create,

        /// <summary>기존 에셋의 값을 바꿉니다.</summary>
        Update,

        /// <summary>표에서 사라져 지웁니다.</summary>
        Delete,

        /// <summary>표에서 사라졌지만 참조가 남아 보존합니다.</summary>
        Preserve,

        /// <summary>반영하지 않고 건너뜁니다.</summary>
        Skip
    }

    /// <summary>필드 하나가 어떻게 바뀌는지입니다.</summary>
    public sealed class CsvFieldChange
    {
        /// <summary>값이 오는 열 이름입니다.</summary>
        public string Column;

        /// <summary>바뀌는 필드 이름입니다.</summary>
        public string Field;

        /// <summary>지금 값입니다.</summary>
        public string From;

        /// <summary>바뀔 값입니다.</summary>
        public string To;
    }

    /// <summary>에셋 하나에 예정된 변경입니다.</summary>
    public sealed class CsvPlannedChange
    {
        /// <summary>변경의 종류입니다.</summary>
        public CsvChangeKind Kind;

        /// <summary>대상 에셋 경로입니다. 아직 없으면 만들어질 경로입니다.</summary>
        public string AssetPath;

        /// <summary>원본 표에서의 줄 번호입니다. 모르면 0입니다.</summary>
        public int Line;

        /// <summary>사람이 읽을 부연입니다. (건너뛰는 이유, 보존하는 이유 등)</summary>
        public string Note;

        /// <summary>바뀌는 필드들입니다. 경로 수준으로만 계획했으면 비어 있습니다.</summary>
        public List<CsvFieldChange> Fields = new List<CsvFieldChange>();

        /// <summary>표시용 이름입니다. (경로의 파일명)</summary>
        public string DisplayName =>
            string.IsNullOrEmpty(AssetPath) ? "(이름 없음)" : System.IO.Path.GetFileNameWithoutExtension(AssetPath);
    }

    /// <summary>
    /// 표를 지금 구우면 <b>무엇이 달라지는지</b>를 미리 계산한 결과입니다. 아무것도 쓰지 않습니다.
    /// </summary>
    public sealed class CsvImportPlan
    {
        /// <summary>계획을 만듭니다.</summary>
        /// <param name="fileName">원본 표의 파일 이름입니다.</param>
        /// <param name="label">사람이 읽을 대상 이름입니다. (보통 에셋 타입 이름)</param>
        public CsvImportPlan(string fileName, string label)
        {
            FileName = fileName;
            Label = label;
        }

        /// <summary>원본 표의 파일 이름입니다.</summary>
        public string FileName { get; }

        /// <summary>사람이 읽을 대상 이름입니다.</summary>
        public string Label { get; }

        /// <summary>산출물 폴더입니다. 정하지 못했으면 null입니다.</summary>
        public string OutputFolder { get; set; }

        /// <summary>예정된 변경들입니다.</summary>
        public List<CsvPlannedChange> Changes { get; } = new List<CsvPlannedChange>();

        /// <summary>계산 도중 발견한 문제들입니다.</summary>
        public List<CsvIssue> Issues { get; } = new List<CsvIssue>();

        /// <summary>계획을 세울 수 없었으면 그 이유입니다. 세웠으면 null입니다.</summary>
        public string Unsupported { get; set; }

        /// <summary>계획을 세울 수 있었는지 여부입니다.</summary>
        public bool IsSupported => Unsupported == null;

        /// <summary>바뀌는 것이 하나도 없으면 true입니다.</summary>
        public bool IsNoOp => Count(CsvChangeKind.Create) == 0
                           && Count(CsvChangeKind.Update) == 0
                           && Count(CsvChangeKind.Delete) == 0;

        /// <summary>지정 종류의 변경 개수입니다.</summary>
        /// <param name="kind">셀 종류입니다.</param>
        /// <returns>개수입니다.</returns>
        public int Count(CsvChangeKind kind)
        {
            int n = 0;
            foreach (CsvPlannedChange change in Changes)
            {
                if (change.Kind == kind) n++;
            }
            return n;
        }

        /// <summary>변경 하나를 더합니다.</summary>
        /// <param name="kind">변경의 종류입니다.</param>
        /// <param name="assetPath">대상 에셋 경로입니다.</param>
        /// <param name="line">원본 줄 번호입니다.</param>
        /// <param name="note">사람이 읽을 부연입니다.</param>
        /// <returns>더해진 변경입니다. 필드 목록을 이어서 채울 수 있습니다.</returns>
        public CsvPlannedChange Add(CsvChangeKind kind, string assetPath, int line = 0, string note = null)
        {
            var change = new CsvPlannedChange { Kind = kind, AssetPath = assetPath, Line = line, Note = note };
            Changes.Add(change);
            return change;
        }

        /// <summary>"생성 3 / 갱신 12 / 삭제 1" 형태의 요약입니다.</summary>
        /// <returns>요약 문자열입니다.</returns>
        public string Summary()
        {
            if (!IsSupported) return Unsupported;

            var parts = new List<string>(5);
            AppendCount(parts, "생성", CsvChangeKind.Create);
            AppendCount(parts, "갱신", CsvChangeKind.Update);
            AppendCount(parts, "삭제", CsvChangeKind.Delete);
            AppendCount(parts, "보존", CsvChangeKind.Preserve);
            AppendCount(parts, "건너뜀", CsvChangeKind.Skip);

            return parts.Count == 0 ? "바뀌는 것 없음" : string.Join(" / ", parts);
        }

        /// <summary>개수가 0이 아니면 요약에 덧붙입니다.</summary>
        /// <param name="parts">요약 조각들입니다.</param>
        /// <param name="label">표기할 이름입니다.</param>
        /// <param name="kind">셀 종류입니다.</param>
        private void AppendCount(List<string> parts, string label, CsvChangeKind kind)
        {
            int n = Count(kind);
            if (n > 0) parts.Add($"{label} {n}");
        }
    }
}
