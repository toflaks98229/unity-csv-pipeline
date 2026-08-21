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
        /// 남은 단위를 굽지 않고 이번 굽기를 멈춥니다. <b>정리 단계도 함께 건너뜁니다.</b>
        /// 더 진행해 봐야 결과가 틀어질 조건(참조 원본이 사라졌다든지)을 굽는 중에 알아챘을 때 쓰십시오.
        /// 표의 절반만 반영된 상태에서 나머지를 지우면 사람이 되돌릴 수 없습니다.
        /// </summary>
        protected void CancelCurrentRun() => _cancelled = true;

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

        // ====================================================================================================
        // 굽기 골격
        // ====================================================================================================

        /// <summary>표에서 사라진 산출물을 무엇으로 대조할지입니다. 기본은 정리하지 않음입니다.</summary>
        protected virtual CsvReconcileMode ReconcileMode => CsvReconcileMode.None;

        /// <summary>정리 대상을 찾을 검색 필터입니다. <see cref="ReconcileMode"/>가 None이 아니면 필요합니다.</summary>
        protected virtual string ReconcileTypeFilter => null;

        /// <summary>정리할 산출물 폴더입니다. 기본은 <see cref="OutputFolder"/>입니다.</summary>
        protected virtual string ReconcileFolder => OutputFolder;

        /// <summary>
        /// 단위마다 <paramref name="bake"/>를 부르고, 진행·취소·집계·겹침 확인·정리를 한 자리에서 처리합니다.
        /// <para>
        /// <b>정리는 취소되지 않았을 때만 합니다.</b> 취소되면 아직 읽지 않은 단위가 남아 있어,
        /// 그 산출물을 "표에서 사라진 것"으로 오해해 지우게 됩니다. 이 규칙을 파생 클래스마다
        /// 다시 적으면 한 곳만 빠뜨려도 취소 버튼이 삭제 버튼이 되므로 여기에 둡니다.
        /// </para>
        /// <para>
        /// <b>같은 자리를 두 번 차지하면 알립니다.</b> 겹친 식별자는 앞 행을 덮어 값을 지우는데,
        /// 집계가 <c>HashSet</c>이라 건수로는 드러나지 않습니다. 이것도 파생마다 적을 일이 아니라
        /// 골격이 지킬 계약입니다.
        /// </para>
        /// </summary>
        /// <typeparam name="TUnit">구울 단위의 타입입니다. (행 또는 그룹 식별자)</typeparam>
        /// <param name="units">구울 단위들입니다.</param>
        /// <param name="report">건수와 문제를 기록할 리포트입니다.</param>
        /// <param name="bake">단위 하나를 굽고 결과를 돌려주는 함수입니다.</param>
        protected void BakeEach<TUnit>(IReadOnlyList<TUnit> units, CsvImportReport report,
                                       Func<TUnit, CsvImportReport, CsvBakeOutcome> bake)
        {
            var validNames = new HashSet<string>();
            var validPaths = new HashSet<string>();
            var claims = new CsvIdClaims();

            for (int i = 0; i < units.Count; i++)
            {
                if (ReportRowProgress(i, units.Count)) break;

                CsvBakeOutcome outcome = bake(units[i], report);

                switch (outcome.Kind)
                {
                    case CsvBakeKind.Created: report.CountCreated(); break;
                    case CsvBakeKind.Updated: report.CountUpdated(); break;
                    default: report.CountSkipped(); continue;
                }

                WarnIfClaimed(claims, outcome, report);

                if (outcome.Name != null) validNames.Add(outcome.Name);
                if (outcome.Path != null) validPaths.Add(outcome.Path);
            }

            if (IsCancelled) return;

            Reconcile(validNames, validPaths, report);
        }

        /// <summary>
        /// 이 단위가 앞선 단위와 같은 에셋을 가리키면 경고합니다.
        /// 자리는 <b>경로</b>로 가립니다. 같은 식별자라도 파생 타입에 따라 다른 폴더에 놓이면
        /// 서로 다른 에셋이고, 그때 경고하면 없는 문제를 있다고 말하게 됩니다.
        /// </summary>
        /// <param name="claims">이번 표의 자리 장부입니다.</param>
        /// <param name="outcome">방금 구운 결과입니다.</param>
        /// <param name="report">경고를 남길 리포트입니다.</param>
        private static void WarnIfClaimed(CsvIdClaims claims, CsvBakeOutcome outcome, CsvImportReport report)
        {
            string key = outcome.Path ?? outcome.Name;
            string display = outcome.Name ?? outcome.Path;
            if (key == null) return;

            if (claims.TryClaim(key, display, outcome.Line, out CsvIdClaim taken)) return;

            report.Warn(CsvIdClaims.Describe(display, taken), outcome.Line);
        }

        /// <summary>
        /// 이번 표가 확정하지 않은 산출물을 정리합니다. 참조가 남은 것은 보존됩니다.
        /// </summary>
        /// <param name="validNames">이번에 확정된 에셋 이름들입니다.</param>
        /// <param name="validPaths">이번에 확정된 에셋 경로들입니다.</param>
        /// <param name="report">결과를 기록할 리포트입니다.</param>
        private void Reconcile(HashSet<string> validNames, HashSet<string> validPaths, CsvImportReport report)
        {
            CsvReconcileMode mode = ReconcileMode;
            if (mode == CsvReconcileMode.None) return;

            string folder = ReconcileFolder;
            string filter = ReconcileTypeFilter;
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(filter)) return;

            if (mode == CsvReconcileMode.ByPath)
                CsvAssetPipeline.ReconcileFolderByPath(folder, filter, validPaths, LogTag, report);
            else
                CsvAssetPipeline.ReconcileFolderByName(folder, filter, validNames, LogTag, report);
        }

        /// <summary>
        /// 행 처리 진행을 알리고 취소 여부를 돌려줍니다. 큰 표에서만 막대를 띄웁니다.
        /// <b>true를 받으면 루프를 즉시 빠져나가야 합니다.</b> 정리를 건너뛰는 것은
        /// <see cref="BakeEach{TUnit}"/>가 대신 지킵니다.
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
        /// 계획 단계에서 같은 에셋을 두 번 가리키면 계획에 경고를 남깁니다.
        /// 굽기 쪽의 <see cref="BakeEach{TUnit}"/>와 같은 판정을 써야 <b>미리보기와 실제가 어긋나지 않습니다.</b>
        /// </summary>
        /// <param name="claims">이번 표의 자리 장부입니다.</param>
        /// <param name="plan">경고를 남길 계획입니다.</param>
        /// <param name="key">자리를 가리는 값입니다. 보통 에셋 경로입니다.</param>
        /// <param name="display">사람에게 보일 식별자 표기입니다.</param>
        /// <param name="line">지금 행의 줄 번호입니다.</param>
        /// <returns>처음 차지한 자리면 true입니다.</returns>
        protected static bool ClaimForPlan(CsvIdClaims claims, CsvImportPlan plan,
                                           string key, string display, int line)
        {
            if (claims.TryClaim(key, display, line, out CsvIdClaim taken)) return true;

            plan.Issues.Add(new CsvIssue(CsvIssueSeverity.Warning,
                                         CsvIdClaims.Describe(display, taken), line));
            return false;
        }

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
            if (preserved.Count == 0) return;

            // 조사할 수 없어 보존한 것과 참조가 남아 보존한 것은 뜻이 다릅니다. 같은 말로 적으면
            // 안전장치가 꺼져 있다는 사실이 "잘 지켜지고 있다"로 읽힙니다.
            string blocked = CsvAssetPipeline.ReferenceScanBlocked;
            string note = blocked ?? "다른 곳에서 참조 중이라 지우지 않습니다.";

            foreach (string path in preserved) plan.Add(CsvChangeKind.Preserve, path, 0, note);

            if (blocked != null) plan.Issues.Add(new CsvIssue(CsvIssueSeverity.Warning, blocked));
        }

        /// <summary>
        /// 로드된 어셈블리에서 매개변수 없는 생성자를 가진 임포터 정의를 모두 찾아 만듭니다.
        /// 미리보기 창이 직접 작성한 임포터까지 목록에 올리는 데 씁니다.
        /// </summary>
        /// <returns>찾은 정의들입니다. 표 파일 이름 순입니다.</returns>
        public static List<CsvImportDefinition> DiscoverAll()
        {
            if (_definitionTypes == null) _definitionTypes = ScanDefinitionTypes();

            var found = new List<CsvImportDefinition>(_definitionTypes.Count);
            foreach (Type type in _definitionTypes)
            {
                try { found.Add((CsvImportDefinition)Activator.CreateInstance(type, true)); }
                catch (Exception) { /* 만들 수 없게 된 정의는 목록에서 빠집니다. */ }
            }

            found.Sort((a, b) => string.CompareOrdinal(a.FileName, b.FileName));
            return found;
        }

        /// <summary>
        /// 찾아 둔 정의 타입들입니다. <b>인스턴스가 아니라 타입을 들고 있습니다</b> —
        /// 정의는 굽는 동안 취소 상태를 지니므로, 나눠 쓰면 지난 실행의 상태가 따라옵니다.
        /// 스크립트를 고치면 도메인이 다시 실리고 이 값도 함께 사라집니다.
        /// </summary>
        private static List<Type> _definitionTypes;

        /// <summary>들고 있던 정의 목록을 버립니다.</summary>
        public static void InvalidateCache() => _definitionTypes = null;

        /// <summary>
        /// 로드된 어셈블리를 전부 훑어 정의 타입을 찾습니다. <b>비싼 쪽이 이것입니다.</b>
        /// 창을 새로고침할 때마다 되풀이하지 않도록 결과를 들고 있습니다.
        /// </summary>
        /// <returns>찾은 정의 타입들입니다.</returns>
        private static List<Type> ScanDefinitionTypes()
        {
            var found = new List<Type>();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // 검사용 임포터는 소비 프로젝트의 것이 아닙니다. (CsvAssemblies 참고)
                if (CsvAssemblies.IsTestAssembly(assembly)) continue;

                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }

                foreach (Type type in types)
                {
                    if (type == null || type.IsAbstract || type.IsGenericTypeDefinition) continue;
                    if (!typeof(CsvImportDefinition).IsAssignableFrom(type)) continue;
                    if (type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                            null, Type.EmptyTypes, null) == null) continue;

                    found.Add(type);
                }
            }

            return found;
        }
    }
}
