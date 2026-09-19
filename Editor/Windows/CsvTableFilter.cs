using System;

namespace CsvPipeline
{
    /// <summary>What the table list shows.</summary>
    public enum CsvTableView
    {
        /// <summary>Shows only tables that have something changing. This is where the window opens.</summary>
        Changed = 0,

        /// <summary>Shows only tables that have problems to fix.</summary>
        Problems = 1,

        /// <summary>Shows every registered table.</summary>
        All = 2,
    }

    /// <summary>
    /// The rules that filter the table list.
    /// <para>
    /// What to hide is an <b>invisible judgment</b>, so it is kept apart from the drawing code.
    /// Filter it wrong and the person only sees "there are no tables", with no way to learn what was hidden.
    /// </para>
    /// </summary>
    public static class CsvTableFilter
    {
        /// <summary>Checks whether this table should be shown.</summary>
        /// <param name="plan">Plan to check.</param>
        /// <param name="view">Currently selected view.</param>
        /// <param name="search">Search term. When empty, nothing is filtered by name.</param>
        /// <returns>True if it should be shown.</returns>
        public static bool Matches(CsvImportPlan plan, CsvTableView view, string search)
        {
            if (plan == null) return false;
            if (!MatchesView(plan, view)) return false;

            return MatchesSearch(plan, search);
        }

        /// <summary>Checks whether it matches the view condition.</summary>
        /// <param name="plan">Plan to check.</param>
        /// <param name="view">Currently selected view.</param>
        /// <returns>True if it matches.</returns>
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
        /// Checks whether it matches the search term. It looks at the table file name and the output type name, <b>ignoring case</b>.
        /// </summary>
        /// <param name="plan">Plan to check.</param>
        /// <param name="search">Search term.</param>
        /// <returns>True if it matches.</returns>
        public static bool MatchesSearch(CsvImportPlan plan, string search)
        {
            if (plan == null) return false;
            if (string.IsNullOrWhiteSpace(search)) return true;

            string trimmed = search.Trim();

            return Contains(plan.FileName, trimmed)
                || Contains(plan.Label, trimmed)
                || Contains(plan.OutputFolder, trimmed);
        }

        /// <summary>Name of the view. The toolbar uses it as is.</summary>
        /// <param name="view">View to name.</param>
        /// <returns>Display string.</returns>
        public static string Label(CsvTableView view)
        {
            switch (view)
            {
                case CsvTableView.Problems: return "Problems only";
                case CsvTableView.All: return "Everything";
                default: return "Changed only";
            }
        }

        /// <summary>An explanation of what the view hides. The tooltip and the empty-state guidance both use it.</summary>
        /// <param name="view">View to explain.</param>
        /// <returns>Explanation string.</returns>
        public static string Describe(CsvTableView view)
        {
            switch (view)
            {
                case CsvTableView.Problems:
                    return "Only tables with a problem, or with no plan at all, are shown. The rest are hidden.";
                case CsvTableView.All:
                    return "Every registered table is shown. Nothing is hidden.";
                default:
                    return "Only tables that would change if you baked now are shown. Output assets that already match their table are hidden.";
            }
        }

        /// <summary>The next view. The toolbar uses it to cycle through the views one step at a time.</summary>
        /// <param name="view">Current view.</param>
        /// <returns>The next view.</returns>
        public static CsvTableView Next(CsvTableView view)
            => view == CsvTableView.All ? CsvTableView.Changed : view + 1;

        /// <summary>Checks whether it contains the fragment, ignoring case.</summary>
        /// <param name="haystack">Text to search. Null gives false.</param>
        /// <param name="needle">Fragment to find.</param>
        /// <returns>True if it contains the fragment.</returns>
        private static bool Contains(string haystack, string needle)
            => !string.IsNullOrEmpty(haystack)
            && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
