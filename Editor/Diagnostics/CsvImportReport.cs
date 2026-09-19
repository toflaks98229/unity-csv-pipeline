using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>Severity of a single issue.</summary>
    public enum CsvIssueSeverity
    {
        /// <summary>Worth knowing about. It does not affect the result.</summary>
        Info,

        /// <summary>May not be what you intended. The import continues.</summary>
        Warning,

        /// <summary>That row or table did not make it into the output.</summary>
        Error
    }

    /// <summary>One issue found during an import. It carries where the issue came from.</summary>
    public sealed class CsvIssue
    {
        /// <summary>Creates an issue.</summary>
        /// <param name="severity">Severity.</param>
        /// <param name="message">Human-readable explanation.</param>
        /// <param name="line">Line number in the source. 0 when unknown.</param>
        /// <param name="column">Name of the column involved. null when there is none.</param>
        /// <param name="context">Asset to ping on click. null when there is none.</param>
        public CsvIssue(CsvIssueSeverity severity, string message, int line = 0, string column = null, Object context = null)
        {
            Severity = severity;
            Message = message;
            Line = line;
            Column = column;
            Context = context;
        }

        /// <summary>Severity.</summary>
        public CsvIssueSeverity Severity { get; }

        /// <summary>Human-readable explanation.</summary>
        public string Message { get; }

        /// <summary>Line number in the source. 0 when unknown.</summary>
        public int Line { get; }

        /// <summary>Name of the column involved. null when there is none.</summary>
        public string Column { get; }

        /// <summary>Asset to ping on click.</summary>
        public Object Context { get; }

        /// <summary>Location in the form "row 12 · MaxSpeed". Empty string when the location is unknown.</summary>
        public string Where
        {
            get
            {
                if (Line > 0 && !string.IsNullOrEmpty(Column)) return $"row {Line} · {Column}";
                if (Line > 0) return $"row {Line}";
                return string.IsNullOrEmpty(Column) ? string.Empty : Column;
            }
        }
    }

    /// <summary>
    /// Result of importing one table. It gathers the logs that used to scatter across the Console
    /// and reports them <b>all at once</b>.
    /// </summary>
    public sealed class CsvImportReport
    {
        /// <summary>Most issues listed in one log. Beyond that only the count is reported.</summary>
        private const int MaxListedIssues = 20;

        private readonly List<CsvIssue> _issues = new List<CsvIssue>();

        /// <summary>Creates a report.</summary>
        /// <param name="fileName">Name of the source file.</param>
        /// <param name="logTag">Tag prefixed to the log.</param>
        public CsvImportReport(string fileName, string logTag)
        {
            FileName = fileName;
            LogTag = logTag;
        }

        /// <summary>Name of the source file.</summary>
        public string FileName { get; }

        /// <summary>Tag prefixed to the log.</summary>
        public string LogTag { get; }

        /// <summary>Number of assets created.</summary>
        public int Created { get; private set; }

        /// <summary>Number of existing assets updated.</summary>
        public int Updated { get; private set; }

        /// <summary>Number of rows skipped without being applied.</summary>
        public int Skipped { get; private set; }

        /// <summary>Number of assets deleted by cleanup.</summary>
        public int Deleted { get; private set; }

        /// <summary>Number of assets that were due for deletion but stayed because they are still referenced.</summary>
        public int Preserved { get; private set; }

        /// <summary>Issues found.</summary>
        public IReadOnlyList<CsvIssue> Issues => _issues;

        /// <summary>True when at least one error blocked the import.</summary>
        public bool HasErrors { get; private set; }

        /// <summary>True when at least one asset was touched.</summary>
        public bool Touched => Created + Updated + Deleted > 0;

        /// <summary>Counts a newly created asset.</summary>
        public void CountCreated() => Created++;

        /// <summary>Counts an updated existing asset.</summary>
        public void CountUpdated() => Updated++;

        /// <summary>Counts a skipped row.</summary>
        public void CountSkipped() => Skipped++;

        /// <summary>Counts a deleted asset.</summary>
        public void CountDeleted() => Deleted++;

        /// <summary>Counts an asset preserved because it is still referenced.</summary>
        public void CountPreserved() => Preserved++;

        /// <summary>Records a note.</summary>
        /// <param name="message">Explanation.</param>
        /// <param name="line">Line number in the source.</param>
        /// <param name="column">Name of the column involved.</param>
        public void Info(string message, int line = 0, string column = null)
            => _issues.Add(new CsvIssue(CsvIssueSeverity.Info, message, line, column));

        /// <summary>Records a warning. The import continues.</summary>
        /// <param name="message">Explanation.</param>
        /// <param name="line">Line number in the source.</param>
        /// <param name="column">Name of the column involved.</param>
        /// <param name="context">Asset to ping on click.</param>
        public void Warn(string message, int line = 0, string column = null, Object context = null)
            => _issues.Add(new CsvIssue(CsvIssueSeverity.Warning, message, line, column, context));

        /// <summary>Records an error. It means that row or table did not make it into the output.</summary>
        /// <param name="message">Explanation.</param>
        /// <param name="line">Line number in the source.</param>
        /// <param name="column">Name of the column involved.</param>
        /// <param name="context">Asset to ping on click.</param>
        public void Error(string message, int line = 0, string column = null, Object context = null)
        {
            _issues.Add(new CsvIssue(CsvIssueSeverity.Error, message, line, column, context));
            HasErrors = true;
        }

        /// <summary>
        /// Writes the result to the Console as <b>a single log</b>. Stays quiet when there is nothing to report.
        /// </summary>
        /// <param name="alwaysLog">Whether to log even when there are no issues and no changes.</param>
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
                        text.Append("  … and ").Append(_issues.Count - listed).Append(" more");
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

        /// <summary>Summary in the form "created 3 / updated 12 / skipped 1".</summary>
        /// <returns>Summary string.</returns>
        public string Summary()
        {
            var parts = new List<string>(5);
            if (Created > 0) parts.Add($"created {Created}");
            if (Updated > 0) parts.Add($"updated {Updated}");
            if (Skipped > 0) parts.Add($"skipped {Skipped}");
            if (Deleted > 0) parts.Add($"deleted {Deleted}");
            if (Preserved > 0) parts.Add($"preserved {Preserved}");
            return parts.Count == 0 ? "no changes" : string.Join(" / ", parts);
        }

        /// <summary>Picks what the log pings when clicked. The source CSV wins.</summary>
        /// <returns>Asset to ping, or null.</returns>
        private Object FindContext()
        {
            foreach (CsvIssue issue in _issues)
            {
                if (issue.Context != null) return issue.Context;
            }

            string path = CsvAssetPipeline.FindCsvPath(FileName);
            return path == null ? null : CsvAssets.Current.Load(path, typeof(Object));
        }

        /// <summary>Short label for a severity.</summary>
        /// <param name="severity">Severity to label.</param>
        /// <returns>Label string.</returns>
        private static string Label(CsvIssueSeverity severity)
        {
            switch (severity)
            {
                case CsvIssueSeverity.Error: return "Error";
                case CsvIssueSeverity.Warning: return "Warning";
                default: return "Info";
            }
        }
    }
}
