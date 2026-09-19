using System.Text.RegularExpressions;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// Settings that link <b>one</b> CSV file to one Google Spreadsheet tab. <b>Editor only</b>.
    /// </summary>
    [CreateAssetMenu(fileName = "SheetSync_", menuName = "CSV Pipeline/Google Sheet Sync Settings")]
    public class GoogleSheetSyncSettings : ScriptableObject
    {
        /// <summary>Name of the target CSV file. (With extension, e.g. Monster_Stats.csv)</summary>
        [Tooltip("Name of the target file inside the CSV root folder. Includes the extension.")]
        public string csvFileName;

        /// <summary>
        /// The Google Sheet address. <b>Paste the link from the browser address bar as is.</b>
        /// </summary>
        [Tooltip("Paste the sheet link from the browser address bar as is. The tab you have open becomes the target.")]
        [TextArea(2, 4)]
        public string sheetUrl;

        /// <summary>Whether this file is included in sheet sync.</summary>
        [Tooltip("Turn this off and the file is not pulled. (Keep it off until the link is filled in)")]
        public bool enabled = true;

        [Header("Auto Pull")]
        /// <summary>Whether the editor checks the sheet periodically and pulls it.</summary>
        [Tooltip("When on, checks at the interval below and pulls only when something changed.")]
        public bool autoPull;

        /// <summary>Auto pull interval, in seconds.</summary>
        [Tooltip("Auto pull interval, in seconds. Too short and half-finished values from an ongoing edit come in.")]
        [Min(10f)]
        public float autoPullIntervalSeconds = 120f;

        [Header("Safeguards")]
        /// <summary>Whether to ask for confirmation when the first line (header) of the pull differs from the existing file.</summary>
        [Tooltip("Confirms before overwriting when the header differs from the existing one. This catches pointing at the wrong tab.")]
        public bool confirmOnHeaderChange = true;

        /// <summary>Pulls the spreadsheet ID out of the address. Empty string when none is found.</summary>
        public string SpreadsheetId
        {
            get
            {
                if (string.IsNullOrWhiteSpace(sheetUrl)) return string.Empty;

                Match match = Regex.Match(sheetUrl, @"/spreadsheets/d/([a-zA-Z0-9\-_]+)");
                return match.Success ? match.Groups[1].Value : string.Empty;
            }
        }

        /// <summary>Pulls the tab number (gid) out of the address. Without one it is "0", the first tab.</summary>
        public string Gid
        {
            get
            {
                if (string.IsNullOrWhiteSpace(sheetUrl)) return "0";

                Match match = Regex.Match(sheetUrl, @"[?&#]gid=([0-9]+)");
                return match.Success ? match.Groups[1].Value : "0";
            }
        }

        /// <summary>Whether every value sheet sync needs is filled in.</summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(csvFileName) && !string.IsNullOrEmpty(SpreadsheetId);

        /// <summary>The CSV export address used for downloading.</summary>
        public string ExportUrl =>
            $"https://docs.google.com/spreadsheets/d/{SpreadsheetId}/export?format=csv&gid={Gid}";
    }
}
