using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>Why one table drifted from its output assets.</summary>
    public enum CsvDriftKind
    {
        /// <summary>Nothing drifted.</summary>
        None,

        /// <summary>Baking now would change the output assets. Someone edited the table and forgot to bake.</summary>
        Changed,

        /// <summary>The table itself has something to fix. (duplicate identifiers, unusable names, and so on)</summary>
        Problem,

        /// <summary>No plan could be built. We cannot even tell whether it drifted.</summary>
        Unreadable,
    }

    /// <summary>One drifted table.</summary>
    public sealed class CsvDriftEntry
    {
        /// <summary>Creates one drifted table.</summary>
        /// <param name="plan">Plan for that table.</param>
        /// <param name="kind">Why it drifted.</param>
        public CsvDriftEntry(CsvImportPlan plan, CsvDriftKind kind)
        {
            Plan = plan;
            Kind = kind;
        }

        /// <summary>Plan for that table.</summary>
        public CsvImportPlan Plan { get; }

        /// <summary>Why it drifted.</summary>
        public CsvDriftKind Kind { get; }

        /// <summary>Explains in one line why it drifted.</summary>
        /// <returns>Explanation string.</returns>
        public string Reason()
        {
            switch (Kind)
            {
                case CsvDriftKind.Unreadable:
                    return Plan.Unsupported ?? "Could not build a plan.";

                case CsvDriftKind.Problem:
                    return Plan.Issues.Count > 0 ? Plan.Issues[0].Message : "There is something to fix.";

                default:
                    return Plan.Summary();
            }
        }
    }

    /// <summary>Result of checking whether tables drifted from their output assets.</summary>
    public sealed class CsvDriftReport
    {
        private readonly List<CsvDriftEntry> _drifted = new List<CsvDriftEntry>();

        /// <summary>Number of tables checked.</summary>
        public int Checked { get; private set; }

        /// <summary>Tables that drifted.</summary>
        public IReadOnlyList<CsvDriftEntry> Drifted => _drifted;

        /// <summary>True when nothing drifted.</summary>
        public bool IsClean => _drifted.Count == 0;

        /// <summary>Exit code a batch run answers with. 0 means clean.</summary>
        public int ExitCode => IsClean ? 0 : 1;

        /// <summary>Counts one checked table.</summary>
        /// <param name="plan">Plan for that table.</param>
        /// <param name="kind">Why it drifted. <see cref="CsvDriftKind.None"/> only counts it.</param>
        public void Add(CsvImportPlan plan, CsvDriftKind kind)
        {
            Checked++;
            if (kind != CsvDriftKind.None) _drifted.Add(new CsvDriftEntry(plan, kind));
        }

        /// <summary>Human-readable result. It lands in the CI log as is.</summary>
        /// <returns>A multi-line report.</returns>
        public string Describe()
        {
            if (IsClean) return $"All {Checked} tables match their output assets.";

            var text = new StringBuilder();
            text.Append($"{_drifted.Count} of {Checked} tables drifted from their output assets.");

            foreach (CsvDriftEntry entry in _drifted)
            {
                text.AppendLine();
                text.Append("  ").Append(Word(entry.Kind).PadRight(8))
                    .Append(entry.Plan.Label).Append(" · ").Append(entry.Plan.FileName)
                    .Append(" — ").Append(entry.Reason());
            }

            text.AppendLine();
            text.Append("Someone may have edited a table and forgotten to bake, or left the baked output uncommitted.");
            return text.ToString();
        }

        /// <summary>One word for the reason.</summary>
        /// <param name="kind">Reason to label.</param>
        /// <returns>Label string.</returns>
        private static string Word(CsvDriftKind kind)
        {
            switch (kind)
            {
                case CsvDriftKind.Problem: return "problem";
                case CsvDriftKind.Unreadable: return "no plan";
                default: return "unbaked";
            }
        }
    }

    /// <summary>
    /// Checks whether tables drifted from their output assets. <b>It writes nothing.</b>
    /// <para>
    /// When someone edits a table, forgets to bake, and commits, the value lives in the git history but not in
    /// the game. People rarely catch it — the changed table file shows up in the diff, but the output assets
    /// staying unchanged <b>does not show up in the diff</b> at all.
    /// </para>
    /// <para>
    /// The plan (<see cref="CsvImportDefinition.Plan"/>) already computes "what changes if I bake now",
    /// so all we ask is whether that answer is "nothing".
    /// </para>
    /// </summary>
    public static class CsvDriftCheck
    {
        /// <summary>Tag prefixed to the log.</summary>
        private const string Tag = "[CSV Pipeline]";

        /// <summary>
        /// Entry point for a batch run. Answers with a <b>non-zero exit code</b> when anything drifted.
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

        /// <summary>The check invoked from the menu. It logs instead of exiting.</summary>
        [MenuItem("Tools/CSV Pipeline/Check for Drift", false, 22)]
        public static void CheckMenu()
        {
            CsvDriftReport report = Inspect();

            if (report.IsClean) Debug.Log($"{Tag} {report.Describe()}");
            else Debug.LogWarning($"{Tag} {report.Describe()}");
        }

        /// <summary>
        /// Checks every registered table and collects the ones that drifted.
        /// </summary>
        /// <param name="definitions">
        /// Importers to check. null finds them all in the project.
        /// <b>Left open so tests can pass their own in</b> — finding them all drags the test fixtures along.
        /// </param>
        /// <returns>Result of the check.</returns>
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

        /// <summary>Finds every importer in the project, including tables declared through the attribute.</summary>
        /// <returns>Importers found.</returns>
        private static IEnumerable<CsvImportDefinition> DiscoverAll()
        {
            var found = new List<CsvImportDefinition>(CsvImportDefinition.DiscoverAll());
            foreach (CsvSchema schema in CsvSchema.All()) found.Add(new CsvSchemaImportDefinition(schema));

            return found;
        }

        /// <summary>
        /// Decides whether one plan drifted. <b>The verdict is the same one the window uses</b> —
        /// if a table reads as "no changes" on screen but fails in CI, one of the two is lying.
        /// </summary>
        /// <param name="plan">Plan to look at.</param>
        /// <returns>Why it drifted.</returns>
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
