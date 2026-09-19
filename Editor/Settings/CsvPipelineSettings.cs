using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// <b>Editor-only</b> settings asset that holds the values which differ per project (folder locations).
    /// Keep one per project. Without one, the pipeline runs on defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "CsvPipelineSettings", menuName = "CSV Pipeline/Settings")]
    public sealed class CsvPipelineSettings : ScriptableObject
    {
        /// <summary>Default CSV root used when no settings asset exists.</summary>
        public const string DefaultCsvRoot = "Assets/CSV";

        /// <summary>Default folder for the copy of the last pulled sheet content. (Under Library, which is not version controlled.)</summary>
        public const string DefaultSnapshotFolder = "Library/CsvSheetSync";

        [Tooltip("Folder that holds the CSVs to import. (Asset path from the project root.)")]
        [SerializeField] private string csvRootFolder = DefaultCsvRoot;

        [Tooltip("Folder for the Google Sheet sync settings assets. Leave it empty to use the Editor folder under the CSV root.")]
        [SerializeField] private string sheetSyncSettingsFolder = string.Empty;

        [Tooltip("Folder for the copy of pulled sheet content. It is used only to detect local edits and is not version controlled.")]
        [SerializeField] private string snapshotFolder = DefaultSnapshotFolder;

        [Header("Table Writing")]
        [Tooltip("Adds a UTF-8 BOM to the files written by export, table creation, and sheet pull.\n"
               + "Turn it off and Windows Excel opens accented and non-Latin text as garbled characters. (Reading works with or without a BOM.)")]
        [SerializeField] private bool writeUtf8Bom = true;

        [Header("Private Sheets (Optional)")]
        [Tooltip("Path to the Google service account key (JSON). Relative to the project root, or absolute.\n"
               + "Keep it outside Assets and exclude it from version control. Leave it empty to pull only link-shared public sheets.")]
        [SerializeField] private string serviceAccountKeyPath = string.Empty;

        /// <summary>Folder that holds the CSVs to import.</summary>
        public string CsvRootFolder =>
            string.IsNullOrWhiteSpace(csvRootFolder) ? DefaultCsvRoot : csvRootFolder.TrimEnd('/');

        /// <summary>Folder where the Google Sheet sync settings assets live.</summary>
        public string SheetSyncSettingsFolder =>
            string.IsNullOrWhiteSpace(sheetSyncSettingsFolder)
                ? $"{CsvRootFolder}/Editor"
                : sheetSyncSettingsFolder.TrimEnd('/');

        /// <summary>Folder for the copy of pulled sheet content.</summary>
        public string SnapshotFolder =>
            string.IsNullOrWhiteSpace(snapshotFolder) ? DefaultSnapshotFolder : snapshotFolder.TrimEnd('/');

        /// <summary>
        /// Whether the table files this tool writes carry a UTF-8 BOM. The default is to write it.
        /// <para>
        /// <b>The reason writing it is the default</b> is that the program opening these tables is usually Excel.
        /// When Excel opens a UTF-8 CSV without a BOM, it reads the file in the system default encoding and non-ASCII text breaks.
        /// The point of an export is "author it again in a spreadsheet", and a file that looks broken is as good as no feature at all.
        /// </para>
        /// <para>
        /// git only sees the 3 BOM bytes added once, and nothing noisy after that.
        /// The reading side treats a file the same with or without a BOM, so the pipeline still runs when this is off.
        /// </para>
        /// </summary>
        public bool WriteUtf8Bom => writeUtf8Bom;

        /// <summary>Encoding this tool uses when it writes a table.</summary>
        public static System.Text.UTF8Encoding TableEncoding => CsvText.Utf8(Instance.WriteUtf8Bom);

        /// <summary>
        /// Path to the Google service account key file. When it is empty, no access to private sheets is attempted.
        /// </summary>
        public string ServiceAccountKeyPath =>
            string.IsNullOrWhiteSpace(serviceAccountKeyPath) ? string.Empty : serviceAccountKeyPath.Trim();

        // ====================================================================================================
        // 조회
        // ====================================================================================================

        /// <summary>The settings asset found so far. It is looked up again once it is destroyed, or when nothing has been found yet.</summary>
        private static CsvPipelineSettings _cached;

        /// <summary>Whether what was found is an asset stored in the project.</summary>
        private static bool _cachedExists;

        /// <summary>
        /// The project's settings asset. When there is none, a transient instance holding the defaults is returned, so this is never null.
        /// </summary>
        public static CsvPipelineSettings Instance
        {
            get
            {
                // 에셋이 지워지거나 리로드되면 Unity 객체가 fake-null이 되므로 == 비교로 확인합니다.
                if (_cached == null) _cached = FindOrCreateTransient();
                return _cached;
            }
        }

        /// <summary>
        /// Whether a real settings asset is stored in the project.
        /// <b>The search runs once and the result is held.</b> Several views read this value while drawing,
        /// and searching the project on each of them makes memory grow just from leaving a window open.
        /// When a settings asset appears or disappears, <see cref="CsvPipelineSettingsHook"/> drops the cache.
        /// </summary>
        public static bool ExistsInProject
        {
            get
            {
                if (_cached == null) _cached = FindOrCreateTransient();
                return _cachedExists;
            }
        }

        /// <summary>Drops the cache so the next lookup searches for the settings asset again.</summary>
        public static void InvalidateCache()
        {
            _cached = null;
            _cachedExists = false;
        }

        /// <summary>Finds every settings asset in the project, in path order.</summary>
        /// <returns>The settings assets found.</returns>
        public static List<CsvPipelineSettings> FindAll()
        {
            var found = new List<CsvPipelineSettings>();
            var paths = new List<string>(CsvAssets.Current.FindPaths($"t:{nameof(CsvPipelineSettings)}"));

            // 검색 순서는 보장되지 않습니다. 여럿일 때 매번 다른 것을 고르지 않도록 경로로 고정합니다.
            paths.Sort(System.StringComparer.Ordinal);

            foreach (string path in paths)
            {
                if (CsvAssets.Current.Load(path, typeof(CsvPipelineSettings)) is CsvPipelineSettings settings)
                {
                    found.Add(settings);
                }
            }
            return found;
        }

        /// <summary>
        /// Finds the stored settings asset, or builds a defaults instance when there is none. (It does not create a file in the project.)
        /// </summary>
        /// <returns>The settings to use.</returns>
        private static CsvPipelineSettings FindOrCreateTransient()
        {
            List<CsvPipelineSettings> found = FindAll();

            if (found.Count > 1)
            {
                // 둘 이상이면 어느 쪽이 쓰였는지 사람이 알 수 없어, 값이 조용히 어긋납니다.
                Debug.LogWarning(
                    $"[CsvPipeline] There are {found.Count} settings assets. Using the first one in path order"
                    + $" ({AssetDatabase.GetAssetPath(found[0])}). Delete the rest.");
            }
            _cachedExists = found.Count > 0;
            if (_cachedExists) return found[0];

            // 소비하는 프로젝트에 말없이 에셋을 만들지 않습니다. 기본값으로 도는 임시 인스턴스를 씁니다.
            var transientSettings = CreateInstance<CsvPipelineSettings>();
            transientSettings.hideFlags = HideFlags.HideAndDontSave;
            return transientSettings;
        }

        /// <summary>Creates a settings asset at the given path and refreshes the cache.</summary>
        /// <param name="assetPath">Path of the asset to create. (For example, Assets/CsvPipelineSettings.asset)</param>
        /// <returns>The created settings asset.</returns>
        public static CsvPipelineSettings CreateAsset(string assetPath)
        {
            var created = CreateInstance<CsvPipelineSettings>();
            AssetDatabase.CreateAsset(created, assetPath);
            AssetDatabase.SaveAssets();

            _cached = created;
            _cachedExists = true;
            return created;
        }
    }
}
