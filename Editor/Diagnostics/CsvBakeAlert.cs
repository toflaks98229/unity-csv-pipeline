using System.Text;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// After a human presses bake, tells them <b>once</b> if there is anything to look at.
    /// <para>
    /// <b>It is not attached to the automatic import.</b> The baking code runs not only for the automatic import
    /// but also for the <b>preview that runs on every window repaint</b> (<see cref="CsvImportDefinition.Plan"/>)
    /// and for the <b>drift check that runs in batch mode</b>. Popping a window per value would turn merely
    /// looking at the list into a stream of dialogs, and CI would stall with nobody there to press anything.
    /// The alert comes <b>where a human pressed, summarized, once</b>.
    /// </para>
    /// </summary>
    public static class CsvBakeAlert
    {
        /// <summary>Most issues written into the dialog. The Console holds the rest.</summary>
        private const int MaxShown = 6;

        /// <summary>
        /// Alerts when the report holds errors or warnings. Does nothing when it does not.
        /// </summary>
        /// <param name="report">Result of the bake that just ran. null does nothing.</param>
        public static void ShowIfNeeded(CsvImportReport report)
        {
            if (report == null || report.Issues.Count == 0) return;

            // 배치 실행에는 누를 사람이 없습니다. 콘솔에는 이미 같은 내용이 남아 있습니다.
            if (Application.isBatchMode) return;

            int errors = 0, warnings = 0;
            foreach (CsvIssue issue in report.Issues)
            {
                if (issue.Severity == CsvIssueSeverity.Error) errors++;
                else if (issue.Severity == CsvIssueSeverity.Warning) warnings++;
            }

            // 참고 사항뿐이면 굳이 멈춰 세우지 않습니다. 알림이 흔해지면 읽지 않게 됩니다.
            if (errors == 0 && warnings == 0) return;

            bool acknowledged = EditorUtility.DisplayDialog(
                errors > 0 ? "Part of the table did not make it in" : "Baked, but there is something to look at",
                Body(report, errors, warnings),
                "OK", "Open Console");

            if (!acknowledged) EditorApplication.ExecuteMenuItem("Window/General/Console");
        }

        /// <summary>Body of the dialog.</summary>
        /// <param name="report">Result of the bake that just ran.</param>
        /// <param name="errors">Number of errors.</param>
        /// <param name="warnings">Number of warnings.</param>
        /// <returns>Body string.</returns>
        private static string Body(CsvImportReport report, int errors, int warnings)
        {
            var text = new StringBuilder();

            text.Append(report.FileName).Append(" — ").Append(report.Summary()).AppendLine();
            text.Append(errors > 0 ? $"Errors {errors}" : string.Empty);
            if (errors > 0 && warnings > 0) text.Append(" · ");
            text.Append(warnings > 0 ? $"Warnings {warnings}" : string.Empty);
            text.AppendLine();

            int shown = 0;
            foreach (CsvIssue issue in report.Issues)
            {
                if (issue.Severity == CsvIssueSeverity.Info) continue;
                if (shown == MaxShown) break;

                text.AppendLine();
                string where = issue.Where;
                if (!string.IsNullOrEmpty(where)) text.Append('[').Append(where).Append("] ");

                // 여러 줄짜리 설명은 첫 줄만 싣습니다. 대화상자는 훑어보는 자리이고,
                // 후보 경로처럼 긴 것은 콘솔이 온전히 갖고 있습니다.
                text.Append(FirstLine(issue.Message));
                shown++;
            }

            int remaining = errors + warnings - shown;
            if (remaining > 0) text.AppendLine().AppendLine().Append($"… and {remaining} more");

            text.AppendLine().AppendLine().Append("The full details are in the Console.");
            return text.ToString();
        }

        /// <summary>First line of a multi-line explanation.</summary>
        /// <param name="message">Explanation to cut.</param>
        /// <returns>The first line.</returns>
        private static string FirstLine(string message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;

            int breakAt = message.IndexOf('\n');
            return breakAt < 0 ? message : message.Substring(0, breakAt).TrimEnd();
        }
    }
}
