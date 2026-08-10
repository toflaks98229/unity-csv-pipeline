using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace CsvPipeline
{
    /// <summary>
    /// 고정 파일명 표 하나를 감시해 굽는 임포터의 뼈대입니다.
    /// 파일 감지·원본 삭제 경고·파싱·열 검증·결과 보고·저장까지의 반복되는 절차를 담고,
    /// 실제 굽기만 파생 클래스에 맡깁니다.
    /// </summary>
    public abstract class CsvImportDefinition
    {
        /// <summary>감시할 원본 파일 이름입니다. (확장자 포함)</summary>
        protected abstract string FileName { get; }

        /// <summary>
        /// 산출물이 놓이는 폴더입니다. null이면 원본이 사라져도 경고하지 않습니다.
        /// (기존 에셋만 갱신하고 새로 만들지 않는 임포터용)
        /// </summary>
        protected virtual string OutputFolder => null;

        /// <summary>
        /// 표에 반드시 있어야 하는 열들입니다. 하나라도 없으면 <b>아무것도 반영하지 않고</b> 오류로 보고합니다.
        /// 빠진 열을 빈 셀로 취급해 조용히 기본값을 굽는 사고를 막습니다.
        /// </summary>
        protected virtual IEnumerable<string> RequiredColumns => null;

        /// <summary>
        /// 로그 접두 태그입니다. 기본값은 이 정의를 감싸는 바깥 클래스 이름이며, 없으면 자기 이름입니다.
        /// </summary>
        protected virtual string LogTag
        {
            get
            {
                System.Type type = GetType();
                return $"[{(type.DeclaringType != null ? type.DeclaringType.Name : type.Name)}]";
            }
        }

        /// <summary>
        /// 에셋 임포트 일괄 통지를 받아, 이 임포터가 감시하는 표가 관련됐을 때만 굽기를 수행합니다.
        /// </summary>
        /// <param name="imported">임포트된 에셋 경로들입니다.</param>
        /// <param name="deleted">삭제된 에셋 경로들입니다.</param>
        /// <param name="moved">이동된 에셋의 새 경로들입니다.</param>
        public void Execute(string[] imported, string[] deleted, string[] moved)
        {
            // 표를 여러 장 써 넣는 도중이면 굽지 않습니다. 자동 경로만 막고, 경로를 직접 넘기는
            // Run은 명시적 호출이라 그대로 돕니다.
            if (CsvImport.IsSuppressed) return;

            string fileName = FileName;
            string outputFolder = OutputFolder;

            if (outputFolder != null)
            {
                foreach (string path in deleted)
                {
                    if (CsvImportUtil.IsFile(path, fileName))
                        CsvAssetPipeline.WarnSourceRemoved(outputFolder, fileName, LogTag);
                }
            }

            if (!CsvImportUtil.Touched(imported, moved, fileName)) return;

            string csvPath = CsvAssetPipeline.FindCsvPath(fileName);
            if (csvPath == null) return;

            Run(csvPath);
        }

        /// <summary>진행 표시를 켜는 최소 행 수입니다. 작은 표에서는 그리는 값이 굽는 값보다 큽니다.</summary>
        private const int ProgressThreshold = 200;

        /// <summary>진행 막대를 띄운 적이 있는지 여부입니다. 띄웠으면 반드시 걷어야 합니다.</summary>
        private bool _progressShown;

        /// <summary>사람이 취소를 눌렀는지 여부입니다.</summary>
        private bool _cancelled;

        /// <summary>이번 굽기가 취소됐는지 여부입니다. 정리 단계는 이 값을 보고 건너뜁니다.</summary>
        protected bool IsCancelled => _cancelled;

        /// <summary>
        /// 지정 경로의 표를 읽어 굽고 결과를 보고합니다. 메뉴에서 직접 부를 수도 있습니다.
        /// </summary>
        /// <param name="csvPath">읽을 표의 에셋 경로입니다.</param>
        /// <returns>임포트 결과 리포트입니다. 읽을 것이 없으면 null입니다.</returns>
        public CsvImportReport Run(string csvPath)
        {
            CsvTable table = CsvImportUtil.ReadTable(csvPath);
            if (table == null) return null;

            var report = new CsvImportReport(FileName, LogTag);

            if (!ValidateColumns(table, report))
            {
                report.Emit();
                return report;
            }

            _progressShown = false;
            _cancelled = false;

            try
            {
                Process(table, report);
            }
            finally
            {
                if (_progressShown) EditorUtility.ClearProgressBar();
            }

            if (_cancelled)
            {
                report.Warn("취소되어 표의 일부만 반영했습니다. 사라진 행의 정리는 하지 않았습니다.");
            }

            report.Emit();

            if (report.Touched) CsvAssets.Current.SaveAll();
            return report;
        }

        /// <summary>
        /// 행 처리 진행을 알리고 취소 여부를 돌려줍니다. 큰 표에서만 막대를 띄웁니다.
        /// <b>true를 받으면 루프를 즉시 빠져나가고 정리 단계도 건너뛰어야 합니다.</b>
        /// 아직 읽지 않은 행의 에셋을 "표에서 사라진 것"으로 오해해 지우면 안 되기 때문입니다.
        /// </summary>
        /// <param name="index">지금 처리할 행의 번호입니다. (0부터)</param>
        /// <param name="total">전체 행 수입니다.</param>
        /// <returns>취소됐으면 true입니다.</returns>
        protected bool ReportRowProgress(int index, int total)
        {
            if (_cancelled) return true;
            if (total < ProgressThreshold) return false;

            // 매 행마다 그리면 그리는 데 드는 값이 굽는 값을 넘습니다.
            if (index != 0 && (index & 31) != 0) return false;

            _progressShown = true;
            _cancelled = EditorUtility.DisplayCancelableProgressBar(
                $"{FileName} 굽는 중", $"{index} / {total}행", (float)index / total);

            return _cancelled;
        }

        /// <summary>
        /// 요구한 열이 전부 있는지 확인합니다. 빠진 열은 대소문자만 다른 후보까지 함께 알립니다.
        /// </summary>
        /// <param name="table">검사할 표입니다.</param>
        /// <param name="report">결과를 기록할 리포트입니다.</param>
        /// <returns>진행해도 되면 true입니다.</returns>
        private bool ValidateColumns(CsvTable table, CsvImportReport report)
        {
            List<string> missing = table.FindMissingColumns(RequiredColumns);
            if (missing.Count == 0) return true;

            foreach (string column in missing)
            {
                string similar = table.FindSimilarColumn(column);
                report.Error(similar != null
                    ? $"열 '{column}'이(가) 없습니다. 대소문자만 다른 '{similar}'이(가) 있습니다."
                    : $"열 '{column}'이(가) 없습니다. 표의 헤더: {string.Join(", ", table.Headers)}");
            }

            report.Info("빠진 열을 빈 셀로 굽지 않도록 이 표는 반영하지 않았습니다.");
            return false;
        }

        /// <summary>
        /// 파싱된 표를 받아 에셋을 굽습니다.
        /// </summary>
        /// <param name="table">파싱된 표입니다. 행이 하나 이상 있습니다.</param>
        /// <param name="report">건수와 문제를 기록할 리포트입니다.</param>
        protected abstract void Process(CsvTable table, CsvImportReport report);

        // ====================================================================================================
        // 미리보기
        // ====================================================================================================

        /// <summary>미리보기에 표시할 이름입니다. 기본값은 로그 태그에서 대괄호를 뗀 것입니다.</summary>
        protected virtual string PlanLabel => LogTag.Trim('[', ']');

        /// <summary>
        /// 표를 지금 구우면 <b>무엇이 달라지는지</b> 계산합니다. <b>아무것도 쓰지 않습니다.</b>
        /// </summary>
        /// <param name="csvPath">읽을 표의 경로입니다. null이면 파일 이름으로 찾습니다.</param>
        /// <returns>계획입니다. 세울 수 없었으면 <see cref="CsvImportPlan.Unsupported"/>에 이유가 담깁니다.</returns>
        public CsvImportPlan Plan(string csvPath = null)
        {
            var plan = new CsvImportPlan(FileName, PlanLabel);

            string path = csvPath ?? CsvAssetPipeline.FindCsvPath(FileName);
            if (path == null)
            {
                plan.Unsupported = "표 파일을 찾지 못했습니다.";
                return plan;
            }

            plan.OutputFolder = OutputFolder;

            CsvTable table = CsvImportUtil.ReadTable(path);
            if (table == null)
            {
                plan.Unsupported = "표를 읽지 못했거나 데이터 행이 없습니다.";
                return plan;
            }

            List<string> missing = table.FindMissingColumns(RequiredColumns);
            if (missing.Count > 0)
            {
                foreach (string column in missing)
                {
                    string similar = table.FindSimilarColumn(column);
                    plan.Issues.Add(new CsvIssue(CsvIssueSeverity.Error, similar != null
                        ? $"열 '{column}'이(가) 없습니다. 이름이 비슷한 '{similar}'이(가) 있습니다."
                        : $"열 '{column}'이(가) 없습니다."));
                }
                plan.Unsupported = "필수 열이 빠져 있어 굽지 않습니다.";
                return plan;
            }

            BuildPlan(table, plan);
            return plan;
        }

        /// <summary>
        /// 표를 읽어 예정된 변경을 채웁니다. 재정의하지 않으면 미리보기를 지원하지 않는 것으로 봅니다.
        /// <b>여기서 에셋을 쓰면 안 됩니다.</b>
        /// </summary>
        /// <param name="table">파싱된 표입니다.</param>
        /// <param name="plan">채울 계획입니다.</param>
        protected virtual void BuildPlan(CsvTable table, CsvImportPlan plan)
            => plan.Unsupported = "이 임포터는 미리보기를 지원하지 않습니다.";

        /// <summary>
        /// 표에서 사라질 산출물을 계획에 더합니다. 참조가 남은 것은 보존으로 분류합니다.
        /// </summary>
        /// <param name="plan">채울 계획입니다.</param>
        /// <param name="folder">정리 대상 폴더입니다.</param>
        /// <param name="typeFilter">에셋 검색 필터입니다.</param>
        /// <param name="valid">이번 표로 확정된 이름 또는 경로들입니다.</param>
        /// <param name="byPath">경로로 대조할지 여부입니다.</param>
        protected static void PlanObsolete(CsvImportPlan plan, string folder, string typeFilter,
                                           ICollection<string> valid, bool byPath)
        {
            CsvAssetPipeline.PlanReconcile(folder, typeFilter, valid, byPath,
                                           out List<string> deletable, out List<string> preserved);

            foreach (string path in deletable) plan.Add(CsvChangeKind.Delete, path);
            foreach (string path in preserved)
            {
                plan.Add(CsvChangeKind.Preserve, path, 0, "다른 곳에서 참조 중이라 지우지 않습니다.");
            }
        }

        /// <summary>
        /// 로드된 어셈블리에서 매개변수 없는 생성자를 가진 임포터 정의를 모두 찾아 만듭니다.
        /// 미리보기 창이 직접 작성한 임포터까지 목록에 올리는 데 씁니다.
        /// </summary>
        /// <returns>찾은 정의들입니다. 표 파일 이름 순입니다.</returns>
        public static List<CsvImportDefinition> DiscoverAll()
        {
            var found = new List<CsvImportDefinition>();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }

                foreach (Type type in types)
                {
                    if (type == null || type.IsAbstract || type.IsGenericTypeDefinition) continue;
                    if (!typeof(CsvImportDefinition).IsAssignableFrom(type)) continue;
                    if (type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                            null, Type.EmptyTypes, null) == null) continue;

                    try { found.Add((CsvImportDefinition)Activator.CreateInstance(type, true)); }
                    catch (Exception) { /* 만들 수 없는 정의는 목록에서 빠집니다. */ }
                }
            }

            found.Sort((a, b) => string.CompareOrdinal(a.FileName, b.FileName));
            return found;
        }
    }
}
