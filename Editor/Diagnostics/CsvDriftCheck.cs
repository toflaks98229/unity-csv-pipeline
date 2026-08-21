using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>표 하나가 산출물과 어긋난 까닭입니다.</summary>
    public enum CsvDriftKind
    {
        /// <summary>어긋나지 않았습니다.</summary>
        None,

        /// <summary>지금 구우면 산출물이 달라집니다. 표를 고치고 굽기를 잊은 자리입니다.</summary>
        Changed,

        /// <summary>표 자체에 손볼 것이 있습니다. (겹친 식별자·쓸 수 없는 이름 등)</summary>
        Problem,

        /// <summary>계획을 세우지 못했습니다. 어긋났는지조차 알 수 없습니다.</summary>
        Unreadable,
    }

    /// <summary>어긋난 표 하나입니다.</summary>
    public sealed class CsvDriftEntry
    {
        /// <summary>어긋난 표 하나를 만듭니다.</summary>
        /// <param name="plan">그 표의 계획입니다.</param>
        /// <param name="kind">어긋난 까닭입니다.</param>
        public CsvDriftEntry(CsvImportPlan plan, CsvDriftKind kind)
        {
            Plan = plan;
            Kind = kind;
        }

        /// <summary>그 표의 계획입니다.</summary>
        public CsvImportPlan Plan { get; }

        /// <summary>어긋난 까닭입니다.</summary>
        public CsvDriftKind Kind { get; }

        /// <summary>왜 어긋났는지 한 줄로 설명합니다.</summary>
        /// <returns>설명 문자열입니다.</returns>
        public string Reason()
        {
            switch (Kind)
            {
                case CsvDriftKind.Unreadable:
                    return Plan.Unsupported ?? "계획을 세우지 못했습니다.";

                case CsvDriftKind.Problem:
                    return Plan.Issues.Count > 0 ? Plan.Issues[0].Message : "손볼 것이 있습니다.";

                default:
                    return Plan.Summary();
            }
        }
    }

    /// <summary>표와 산출물이 어긋나는지 살펴본 결과입니다.</summary>
    public sealed class CsvDriftReport
    {
        private readonly List<CsvDriftEntry> _drifted = new List<CsvDriftEntry>();

        /// <summary>살펴본 표의 수입니다.</summary>
        public int Checked { get; private set; }

        /// <summary>어긋난 표들입니다.</summary>
        public IReadOnlyList<CsvDriftEntry> Drifted => _drifted;

        /// <summary>어긋난 것이 하나도 없으면 true입니다.</summary>
        public bool IsClean => _drifted.Count == 0;

        /// <summary>배치 실행이 답으로 쓸 종료 코드입니다. 0이면 깨끗합니다.</summary>
        public int ExitCode => IsClean ? 0 : 1;

        /// <summary>살펴본 표 하나를 셉니다.</summary>
        /// <param name="plan">그 표의 계획입니다.</param>
        /// <param name="kind">어긋난 까닭입니다. <see cref="CsvDriftKind.None"/>이면 세기만 합니다.</param>
        public void Add(CsvImportPlan plan, CsvDriftKind kind)
        {
            Checked++;
            if (kind != CsvDriftKind.None) _drifted.Add(new CsvDriftEntry(plan, kind));
        }

        /// <summary>사람이 읽을 결과입니다. CI 로그에 그대로 남습니다.</summary>
        /// <returns>여러 줄의 보고입니다.</returns>
        public string Describe()
        {
            if (IsClean) return $"표 {Checked}장이 모두 산출물과 일치합니다.";

            var text = new StringBuilder();
            text.Append($"표 {Checked}장 중 {_drifted.Count}장이 산출물과 어긋납니다.");

            foreach (CsvDriftEntry entry in _drifted)
            {
                text.AppendLine();
                text.Append("  ").Append(Word(entry.Kind).PadRight(8))
                    .Append(entry.Plan.Label).Append(" · ").Append(entry.Plan.FileName)
                    .Append(" — ").Append(entry.Reason());
            }

            text.AppendLine();
            text.Append("표를 고치고 굽기를 잊었거나, 구운 결과를 커밋하지 않았을 수 있습니다.");
            return text.ToString();
        }

        /// <summary>까닭의 한 마디입니다.</summary>
        /// <param name="kind">표기할 까닭입니다.</param>
        /// <returns>표기 문자열입니다.</returns>
        private static string Word(CsvDriftKind kind)
        {
            switch (kind)
            {
                case CsvDriftKind.Problem: return "문제";
                case CsvDriftKind.Unreadable: return "못 읽음";
                default: return "안 구움";
            }
        }
    }

    /// <summary>
    /// 표와 산출물이 어긋나는지 확인합니다. <b>아무것도 쓰지 않습니다.</b>
    /// <para>
    /// 표를 고치고 굽기를 잊은 채 커밋하면, 그 값은 git 이력에는 있는데 게임에는 없습니다.
    /// 사람은 알아채기 어렵습니다 — 표 파일이 바뀐 것은 diff 에 보이지만, 산출물이 안 바뀐 것은
    /// <b>diff 에 보이지 않기</b> 때문입니다.
    /// </para>
    /// <para>
    /// 계획(<see cref="CsvImportDefinition.Plan"/>)이 이미 "지금 구우면 무엇이 달라지는가"를
    /// 계산하고 있으므로, 그 답이 "아무것도"인지만 물으면 됩니다.
    /// </para>
    /// </summary>
    public static class CsvDriftCheck
    {
        /// <summary>로그에 붙는 접두 태그입니다.</summary>
        private const string Tag = "[CSV Pipeline]";

        /// <summary>
        /// 배치 실행 진입점입니다. 어긋난 것이 있으면 <b>0이 아닌 종료 코드</b>로 답합니다.
        /// </summary>
        /// <example>
        /// <code>
        /// Unity -batchmode -quit -projectPath . -executeMethod CsvPipeline.CsvDriftCheck.Run
        /// </code>
        /// </example>
        public static void Run()
        {
            CsvDriftReport report = Inspect();

            if (report.IsClean) Debug.Log($"{Tag} {report.Describe()}");
            else Debug.LogError($"{Tag} {report.Describe()}");

            EditorApplication.Exit(report.ExitCode);
        }

        /// <summary>메뉴에서 부르는 확인입니다. 종료하지 않고 로그만 남깁니다.</summary>
        [MenuItem("Tools/CSV Pipeline/표와 산출물이 어긋나는지 확인", false, 22)]
        public static void CheckMenu()
        {
            CsvDriftReport report = Inspect();

            if (report.IsClean) Debug.Log($"{Tag} {report.Describe()}");
            else Debug.LogWarning($"{Tag} {report.Describe()}");
        }

        /// <summary>
        /// 등록된 표를 모두 살펴 어긋난 것을 모읍니다.
        /// </summary>
        /// <param name="definitions">
        /// 살펴볼 임포터들입니다. null이면 프로젝트에서 모두 찾습니다.
        /// <b>검사에서 넘길 수 있게 열어 두었습니다</b> — 전부 찾으면 검사용 픽스처까지 딸려 옵니다.
        /// </param>
        /// <returns>살펴본 결과입니다.</returns>
        public static CsvDriftReport Inspect(IEnumerable<CsvImportDefinition> definitions = null)
        {
            var report = new CsvDriftReport();

            foreach (CsvImportDefinition definition in definitions ?? DiscoverAll())
            {
                CsvImportPlan plan = definition.Plan();
                report.Add(plan, KindOf(plan));
            }

            return report;
        }

        /// <summary>프로젝트의 임포터를 모두 찾습니다. 속성으로 선언한 표까지 함께 봅니다.</summary>
        /// <returns>찾은 임포터들입니다.</returns>
        private static IEnumerable<CsvImportDefinition> DiscoverAll()
        {
            var found = new List<CsvImportDefinition>(CsvImportDefinition.DiscoverAll());
            foreach (CsvSchema schema in CsvSchema.All()) found.Add(new CsvSchemaImportDefinition(schema));

            return found;
        }

        /// <summary>
        /// 계획 하나가 어긋났는지 가립니다. <b>판정은 창의 것과 같습니다</b> —
        /// 화면에서 "바뀌는 것 없음"으로 보이는 표가 CI 에서는 실패하면, 둘 중 하나는 거짓말입니다.
        /// </summary>
        /// <param name="plan">볼 계획입니다.</param>
        /// <returns>어긋난 까닭입니다.</returns>
        public static CsvDriftKind KindOf(CsvImportPlan plan)
        {
            switch (CsvPlanStatus.Of(plan))
            {
                case CsvPlanState.Ok: return CsvDriftKind.None;
                case CsvPlanState.Blocked: return CsvDriftKind.Unreadable;
                case CsvPlanState.Problem: return CsvDriftKind.Problem;
                default: return CsvDriftKind.Changed;
            }
        }
    }
}
