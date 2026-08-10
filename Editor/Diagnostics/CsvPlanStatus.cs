namespace CsvPipeline
{
    /// <summary>계획 하나를 한 마디로 요약한 상태입니다. 값이 클수록 사람이 먼저 봐야 합니다.</summary>
    public enum CsvPlanState
    {
        /// <summary>표와 산출물이 같습니다. 지금 구워도 달라지는 것이 없습니다.</summary>
        Ok = 0,

        /// <summary>만들거나 갱신할 것이 있습니다.</summary>
        Changed = 1,

        /// <summary>사라지는 산출물이 있습니다. 되돌릴 수 없어 보기보다 무겁습니다.</summary>
        Removing = 2,

        /// <summary>계산 중에 문제를 찾았습니다.</summary>
        Problem = 3,

        /// <summary>계획 자체를 세우지 못했습니다. 무슨 일이 일어날지 알 수 없습니다.</summary>
        Blocked = 4,
    }

    /// <summary>
    /// 계획을 상태 하나로 줄입니다. <b>그리기와 떼어 둡니다.</b>
    /// 목록의 정렬과 아이콘이 모두 이 판정에 매여 있어, 화면 안에 두면 확인할 방법이 없습니다.
    /// </summary>
    public static class CsvPlanStatus
    {
        /// <summary>
        /// 계획의 상태를 가립니다.
        /// 오류가 경고를 이기고, 삭제가 단순 변경을 이깁니다. <b>더 무거운 쪽이 남습니다.</b>
        /// </summary>
        /// <param name="plan">볼 계획입니다.</param>
        /// <returns>상태입니다.</returns>
        public static CsvPlanState Of(CsvImportPlan plan)
        {
            if (plan == null || !plan.IsSupported) return CsvPlanState.Blocked;

            foreach (CsvIssue issue in plan.Issues)
            {
                if (issue.Severity == CsvIssueSeverity.Error) return CsvPlanState.Problem;
            }

            if (plan.Count(CsvChangeKind.Delete) > 0) return CsvPlanState.Removing;
            if (plan.Issues.Count > 0) return CsvPlanState.Problem;
            if (!plan.IsNoOp) return CsvPlanState.Changed;

            return CsvPlanState.Ok;
        }

        /// <summary>사람이 손볼 것이 있는 상태인지 여부입니다.</summary>
        /// <param name="state">볼 상태입니다.</param>
        /// <returns>손볼 것이 있으면 true입니다.</returns>
        public static bool NeedsAttention(CsvPlanState state)
            => state == CsvPlanState.Problem || state == CsvPlanState.Blocked;
    }
}
