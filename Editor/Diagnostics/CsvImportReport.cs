using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>문제 하나의 심각도입니다.</summary>
    public enum CsvIssueSeverity
    {
        /// <summary>알아 두면 좋은 사실입니다. 결과에 영향이 없습니다.</summary>
        Info,

        /// <summary>의도한 것이 아닐 수 있습니다. 임포트는 계속됩니다.</summary>
        Warning,

        /// <summary>그 행 또는 표를 반영하지 못했습니다.</summary>
        Error
    }

    /// <summary>임포트 중 발견한 문제 하나입니다. 어디서 났는지를 함께 들고 있습니다.</summary>
    public sealed class CsvIssue
    {
        /// <summary>문제를 만듭니다.</summary>
        /// <param name="severity">심각도입니다.</param>
        /// <param name="message">사람이 읽을 설명입니다.</param>
        /// <param name="line">원본 줄 번호입니다. 모르면 0입니다.</param>
        /// <param name="column">관련 열 이름입니다. 없으면 null입니다.</param>
        /// <param name="context">클릭해 찾아갈 에셋입니다. 없으면 null입니다.</param>
        public CsvIssue(CsvIssueSeverity severity, string message, int line = 0, string column = null, Object context = null)
        {
            Severity = severity;
            Message = message;
            Line = line;
            Column = column;
            Context = context;
        }

        /// <summary>심각도입니다.</summary>
        public CsvIssueSeverity Severity { get; }

        /// <summary>사람이 읽을 설명입니다.</summary>
        public string Message { get; }

        /// <summary>원본 줄 번호입니다. 모르면 0입니다.</summary>
        public int Line { get; }

        /// <summary>관련 열 이름입니다. 없으면 null입니다.</summary>
        public string Column { get; }

        /// <summary>클릭해 찾아갈 에셋입니다.</summary>
        public Object Context { get; }

        /// <summary>"12행 · MaxSpeed" 형태의 위치 표기입니다. 위치를 모르면 빈 문자열입니다.</summary>
        public string Where
        {
            get
            {
                if (Line > 0 && !string.IsNullOrEmpty(Column)) return $"{Line}행 · {Column}";
                if (Line > 0) return $"{Line}행";
                return string.IsNullOrEmpty(Column) ? string.Empty : Column;
            }
        }
    }

    /// <summary>
    /// 표 하나를 임포트한 결과입니다. Console에 흩어지던 로그를 모아 <b>한 번에</b> 보고합니다.
    /// </summary>
    public sealed class CsvImportReport
    {
        /// <summary>한 로그에 나열할 문제의 최대 개수입니다. 넘으면 개수만 알립니다.</summary>
        private const int MaxListedIssues = 20;

        private readonly List<CsvIssue> _issues = new List<CsvIssue>();

        /// <summary>리포트를 만듭니다.</summary>
        /// <param name="fileName">원본 파일 이름입니다.</param>
        /// <param name="logTag">로그 접두 태그입니다.</param>
        public CsvImportReport(string fileName, string logTag)
        {
            FileName = fileName;
            LogTag = logTag;
        }

        /// <summary>원본 파일 이름입니다.</summary>
        public string FileName { get; }

        /// <summary>로그 접두 태그입니다.</summary>
        public string LogTag { get; }

        /// <summary>새로 만든 에셋 수입니다.</summary>
        public int Created { get; private set; }

        /// <summary>기존 에셋을 갱신한 수입니다.</summary>
        public int Updated { get; private set; }

        /// <summary>반영하지 못하고 건너뛴 행 수입니다.</summary>
        public int Skipped { get; private set; }

        /// <summary>정리로 삭제한 에셋 수입니다.</summary>
        public int Deleted { get; private set; }

        /// <summary>삭제 대상이었으나 참조가 남아 보존한 에셋 수입니다.</summary>
        public int Preserved { get; private set; }

        /// <summary>발견한 문제들입니다.</summary>
        public IReadOnlyList<CsvIssue> Issues => _issues;

        /// <summary>반영을 막는 오류가 하나라도 있으면 true입니다.</summary>
        public bool HasErrors { get; private set; }

        /// <summary>에셋을 하나라도 건드렸으면 true입니다.</summary>
        public bool Touched => Created + Updated + Deleted > 0;

        /// <summary>새 에셋을 만들었음을 셉니다.</summary>
        public void CountCreated() => Created++;

        /// <summary>기존 에셋을 갱신했음을 셉니다.</summary>
        public void CountUpdated() => Updated++;

        /// <summary>행을 건너뛰었음을 셉니다.</summary>
        public void CountSkipped() => Skipped++;

        /// <summary>에셋을 삭제했음을 셉니다.</summary>
        public void CountDeleted() => Deleted++;

        /// <summary>참조가 남아 보존했음을 셉니다.</summary>
        public void CountPreserved() => Preserved++;

        /// <summary>참고 사항을 남깁니다.</summary>
        /// <param name="message">설명입니다.</param>
        /// <param name="line">원본 줄 번호입니다.</param>
        /// <param name="column">관련 열 이름입니다.</param>
        public void Info(string message, int line = 0, string column = null)
            => _issues.Add(new CsvIssue(CsvIssueSeverity.Info, message, line, column));

        /// <summary>경고를 남깁니다. 임포트는 계속됩니다.</summary>
        /// <param name="message">설명입니다.</param>
        /// <param name="line">원본 줄 번호입니다.</param>
        /// <param name="column">관련 열 이름입니다.</param>
        /// <param name="context">클릭해 찾아갈 에셋입니다.</param>
        public void Warn(string message, int line = 0, string column = null, Object context = null)
            => _issues.Add(new CsvIssue(CsvIssueSeverity.Warning, message, line, column, context));

        /// <summary>오류를 남깁니다. 해당 행 또는 표를 반영하지 못했다는 뜻입니다.</summary>
        /// <param name="message">설명입니다.</param>
        /// <param name="line">원본 줄 번호입니다.</param>
        /// <param name="column">관련 열 이름입니다.</param>
        /// <param name="context">클릭해 찾아갈 에셋입니다.</param>
        public void Error(string message, int line = 0, string column = null, Object context = null)
        {
            _issues.Add(new CsvIssue(CsvIssueSeverity.Error, message, line, column, context));
            HasErrors = true;
        }

        /// <summary>
        /// 결과를 Console에 <b>한 줄의 로그로</b> 냅니다. 문제가 없으면 조용히 넘어갑니다.
        /// </summary>
        /// <param name="alwaysLog">문제도 변경도 없을 때에도 로그를 낼지 여부입니다.</param>
        public void Emit(bool alwaysLog = false)
        {
            if (!Touched && Skipped == 0 && _issues.Count == 0 && !alwaysLog) return;

            var text = new StringBuilder();
            text.Append(LogTag).Append(' ').Append(FileName).Append(" — ").Append(Summary());

            int errors = 0, warnings = 0;
            foreach (CsvIssue issue in _issues)
            {
                if (issue.Severity == CsvIssueSeverity.Error) errors++;
                else if (issue.Severity == CsvIssueSeverity.Warning) warnings++;
            }

            if (_issues.Count > 0)
            {
                text.AppendLine();
                int listed = 0;
                foreach (CsvIssue issue in _issues)
                {
                    if (listed >= MaxListedIssues)
                    {
                        text.Append("  … 외 ").Append(_issues.Count - listed).Append("건");
                        break;
                    }
                    text.Append("  [").Append(Label(issue.Severity)).Append("] ");
                    string where = issue.Where;
                    if (!string.IsNullOrEmpty(where)) text.Append(where).Append(" — ");
                    text.AppendLine(issue.Message);
                    listed++;
                }
            }

            Object context = FindContext();
            string message = text.ToString().TrimEnd();

            if (errors > 0) Debug.LogError(message, context);
            else if (warnings > 0) Debug.LogWarning(message, context);
            else Debug.Log(message, context);
        }

        /// <summary>"생성 3 / 갱신 12 / 건너뜀 1" 형태의 요약입니다.</summary>
        /// <returns>요약 문자열입니다.</returns>
        public string Summary()
        {
            var parts = new List<string>(5);
            if (Created > 0) parts.Add($"생성 {Created}");
            if (Updated > 0) parts.Add($"갱신 {Updated}");
            if (Skipped > 0) parts.Add($"건너뜀 {Skipped}");
            if (Deleted > 0) parts.Add($"삭제 {Deleted}");
            if (Preserved > 0) parts.Add($"보존 {Preserved}");
            return parts.Count == 0 ? "변경 없음" : string.Join(" / ", parts);
        }

        /// <summary>로그를 클릭했을 때 찾아갈 대상을 고릅니다. 원본 CSV를 우선합니다.</summary>
        /// <returns>찾아갈 에셋이거나 null입니다.</returns>
        private Object FindContext()
        {
            foreach (CsvIssue issue in _issues)
            {
                if (issue.Context != null) return issue.Context;
            }

            string path = CsvAssetPipeline.FindCsvPath(FileName);
            return path == null ? null : AssetDatabase.LoadAssetAtPath<Object>(path);
        }

        /// <summary>심각도의 한 글자 표기입니다.</summary>
        /// <param name="severity">표기할 심각도입니다.</param>
        /// <returns>표기 문자열입니다.</returns>
        private static string Label(CsvIssueSeverity severity)
        {
            switch (severity)
            {
                case CsvIssueSeverity.Error: return "오류";
                case CsvIssueSeverity.Warning: return "경고";
                default: return "참고";
            }
        }
    }
}
