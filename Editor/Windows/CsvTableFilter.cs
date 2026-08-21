using System;

namespace CsvPipeline
{
    /// <summary>표 목록에서 무엇을 보여 줄지입니다.</summary>
    public enum CsvTableView
    {
        /// <summary>바뀌는 것이 있는 표만 봅니다. 처음 여는 자리입니다.</summary>
        Changed = 0,

        /// <summary>손볼 것이 있는 표만 봅니다.</summary>
        Problems = 1,

        /// <summary>등록된 표를 전부 봅니다.</summary>
        All = 2,
    }

    /// <summary>
    /// 표 목록을 거르는 규칙입니다.
    /// <para>
    /// 무엇을 감출지는 <b>눈에 보이지 않는 판단</b>이라 그리기에서 떼어 둡니다.
    /// 잘못 거르면 사람은 "표가 없다"고만 보게 되고, 무엇이 감춰졌는지 알 방법이 없습니다.
    /// </para>
    /// </summary>
    public static class CsvTableFilter
    {
        /// <summary>이 표를 보여 줘야 하는지 봅니다.</summary>
        /// <param name="plan">볼 계획입니다.</param>
        /// <param name="view">지금 고른 보기입니다.</param>
        /// <param name="search">검색어입니다. 비어 있으면 이름으로 거르지 않습니다.</param>
        /// <returns>보여 줘야 하면 true입니다.</returns>
        public static bool Matches(CsvImportPlan plan, CsvTableView view, string search)
        {
            if (plan == null) return false;
            if (!MatchesView(plan, view)) return false;

            return MatchesSearch(plan, search);
        }

        /// <summary>보기 조건에 걸리는지 봅니다.</summary>
        /// <param name="plan">볼 계획입니다.</param>
        /// <param name="view">지금 고른 보기입니다.</param>
        /// <returns>걸리면 true입니다.</returns>
        public static bool MatchesView(CsvImportPlan plan, CsvTableView view)
        {
            CsvPlanState state = CsvPlanStatus.Of(plan);

            switch (view)
            {
                case CsvTableView.Problems: return CsvPlanStatus.NeedsAttention(state);
                case CsvTableView.Changed: return state != CsvPlanState.Ok;
                default: return true;
            }
        }

        /// <summary>
        /// 검색어에 걸리는지 봅니다. 표 파일 이름과 산출물 타입 이름을 <b>대소문자를 무시하고</b> 봅니다.
        /// </summary>
        /// <param name="plan">볼 계획입니다.</param>
        /// <param name="search">검색어입니다.</param>
        /// <returns>걸리면 true입니다.</returns>
        public static bool MatchesSearch(CsvImportPlan plan, string search)
        {
            if (plan == null) return false;
            if (string.IsNullOrWhiteSpace(search)) return true;

            string trimmed = search.Trim();

            return Contains(plan.FileName, trimmed)
                || Contains(plan.Label, trimmed)
                || Contains(plan.OutputFolder, trimmed);
        }

        /// <summary>보기의 이름입니다. 도구 줄에 그대로 씁니다.</summary>
        /// <param name="view">이름을 물을 보기입니다.</param>
        /// <returns>표기 문자열입니다.</returns>
        public static string Label(CsvTableView view)
        {
            switch (view)
            {
                case CsvTableView.Problems: return "손볼 것만";
                case CsvTableView.All: return "전부";
                default: return "바뀌는 것만";
            }
        }

        /// <summary>보기가 무엇을 감추는지에 대한 설명입니다. 툴팁과 빈 화면 안내가 함께 씁니다.</summary>
        /// <param name="view">설명할 보기입니다.</param>
        /// <returns>설명 문자열입니다.</returns>
        public static string Describe(CsvTableView view)
        {
            switch (view)
            {
                case CsvTableView.Problems:
                    return "문제가 있거나 계획을 세우지 못한 표만 보입니다. 나머지는 감춰져 있습니다.";
                case CsvTableView.All:
                    return "등록된 표를 모두 보입니다. 아무것도 감추지 않습니다.";
                default:
                    return "지금 구우면 달라지는 것이 있는 표만 보입니다. 표와 같은 산출물은 감춰져 있습니다.";
            }
        }

        /// <summary>다음 보기입니다. 도구 줄에서 한 칸씩 돌려 가며 고르는 데 씁니다.</summary>
        /// <param name="view">지금 보기입니다.</param>
        /// <returns>다음 보기입니다.</returns>
        public static CsvTableView Next(CsvTableView view)
            => view == CsvTableView.All ? CsvTableView.Changed : view + 1;

        /// <summary>대소문자를 무시하고 담고 있는지 봅니다.</summary>
        /// <param name="haystack">찾을 대상입니다. null이면 false입니다.</param>
        /// <param name="needle">찾을 조각입니다.</param>
        /// <returns>담고 있으면 true입니다.</returns>
        private static bool Contains(string haystack, string needle)
            => !string.IsNullOrEmpty(haystack)
            && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
