namespace CsvPipeline
{
    /// <summary>A plan reduced to one status. The higher the value, the sooner a human should look at it.</summary>
    public enum CsvPlanState
    {
        /// <summary>The table and its output assets agree. Baking now changes nothing.</summary>
        Ok = 0,

        /// <summary>There is something to create or update.</summary>
        Changed = 1,

        /// <summary>Output assets disappear. That cannot be undone, so it weighs more than it looks.</summary>
        Removing = 2,

        /// <summary>A problem turned up while computing the plan.</summary>
        Problem = 3,

        /// <summary>No plan could be built at all. There is no telling what would happen.</summary>
        Blocked = 4,
    }

    /// <summary>
    /// Reduces a plan to a single status. <b>Kept apart from drawing.</b>
    /// The sort order and the icons of the list all hang on this verdict, and inside the window there would be
    /// no way to check it.
    /// </summary>
    public static class CsvPlanStatus
    {
        /// <summary>
        /// Decides the status of a plan.
        /// Errors beat warnings, and deletions beat plain changes. <b>The heavier one wins.</b>
        /// </summary>
        /// <param name="plan">Plan to look at.</param>
        /// <returns>The status.</returns>
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

        /// <summary>Whether the status is one a human has to fix.</summary>
        /// <param name="state">Status to look at.</param>
        /// <returns>True when there is something to fix.</returns>
        public static bool NeedsAttention(CsvPlanState state)
            => state == CsvPlanState.Problem || state == CsvPlanState.Blocked;
    }
}
