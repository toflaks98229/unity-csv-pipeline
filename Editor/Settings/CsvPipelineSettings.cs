using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// 파이프라인이 프로젝트마다 달라지는 값(폴더 위치)을 담는 <b>에디터 전용</b> 설정 에셋입니다.
    /// 프로젝트에 하나만 두며, 없으면 기본값으로 동작합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "CsvPipelineSettings", menuName = "CSV Pipeline/Settings")]
    public sealed class CsvPipelineSettings : ScriptableObject
    {
        /// <summary>설정 에셋이 하나도 없을 때 쓰는 CSV 루트 기본값입니다.</summary>
        public const string DefaultCsvRoot = "Assets/CSV";

        /// <summary>마지막으로 받은 시트 내용의 사본을 두는 폴더 기본값입니다. (버전 관리 대상이 아닌 Library 아래)</summary>
        public const string DefaultSnapshotFolder = "Library/CsvSheetSync";

        [Tooltip("임포트 대상 CSV들이 모여 있는 폴더입니다. (프로젝트 루트부터의 에셋 경로)")]
        [SerializeField] private string csvRootFolder = DefaultCsvRoot;

        [Tooltip("Google Sheet 연동 설정 에셋을 둘 폴더입니다. 비우면 CSV 루트 아래의 Editor 폴더를 씁니다.")]
        [SerializeField] private string sheetSyncSettingsFolder = string.Empty;

        [Tooltip("받아 온 시트 내용의 사본을 두는 폴더입니다. 로컬 수정 감지에만 쓰며 버전 관리 대상이 아닙니다.")]
        [SerializeField] private string snapshotFolder = DefaultSnapshotFolder;

        [Header("비공개 시트 (선택)")]
        [Tooltip("구글 서비스 계정 키(JSON)의 경로입니다. 프로젝트 루트 기준 상대 경로 또는 절대 경로.\n"
               + "Assets 밖에 두고 버전 관리에서 제외하십시오. 비우면 공개 링크 시트만 받습니다.")]
        [SerializeField] private string serviceAccountKeyPath = string.Empty;

        /// <summary>임포트 대상 CSV들이 모여 있는 폴더입니다.</summary>
        public string CsvRootFolder =>
            string.IsNullOrWhiteSpace(csvRootFolder) ? DefaultCsvRoot : csvRootFolder.TrimEnd('/');

        /// <summary>Google Sheet 연동 설정 에셋들이 놓이는 폴더입니다.</summary>
        public string SheetSyncSettingsFolder =>
            string.IsNullOrWhiteSpace(sheetSyncSettingsFolder)
                ? $"{CsvRootFolder}/Editor"
                : sheetSyncSettingsFolder.TrimEnd('/');

        /// <summary>받아 온 시트 내용의 사본을 두는 폴더입니다.</summary>
        public string SnapshotFolder =>
            string.IsNullOrWhiteSpace(snapshotFolder) ? DefaultSnapshotFolder : snapshotFolder.TrimEnd('/');

        /// <summary>
        /// 구글 서비스 계정 키 파일의 경로입니다. 비어 있으면 비공개 시트 접근을 시도하지 않습니다.
        /// </summary>
        public string ServiceAccountKeyPath =>
            string.IsNullOrWhiteSpace(serviceAccountKeyPath) ? string.Empty : serviceAccountKeyPath.Trim();

        // ====================================================================================================
        // 조회
        // ====================================================================================================

        /// <summary>찾아 둔 설정 에셋입니다. 파괴됐거나 아직 찾지 않았으면 다시 조회합니다.</summary>
        private static CsvPipelineSettings _cached;

        /// <summary>찾은 것이 프로젝트에 저장된 에셋이었는지 여부입니다.</summary>
        private static bool _cachedExists;

        /// <summary>
        /// 프로젝트의 설정 에셋입니다. 없으면 기본값을 담은 임시 인스턴스를 돌려주므로 null이 아닙니다.
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
        /// 프로젝트에 실제 설정 에셋이 저장돼 있는지 여부입니다.
        /// <b>찾기는 한 번만 하고 결과를 들고 있습니다.</b> 이 값을 그리기에서 읽는 화면이 여럿인데,
        /// 그때마다 프로젝트를 뒤지면 창을 열어 둔 것만으로 메모리가 계속 늘어납니다.
        /// 설정 에셋이 생기거나 사라지면 <see cref="CsvPipelineSettingsHook"/>가 캐시를 버립니다.
        /// </summary>
        public static bool ExistsInProject
        {
            get
            {
                if (_cached == null) _cached = FindOrCreateTransient();
                return _cachedExists;
            }
        }

        /// <summary>다음 조회에서 설정 에셋을 다시 찾도록 캐시를 버립니다.</summary>
        public static void InvalidateCache()
        {
            _cached = null;
            _cachedExists = false;
        }

        /// <summary>프로젝트의 설정 에셋을 모두 경로순으로 찾습니다.</summary>
        /// <returns>찾은 설정 에셋 목록입니다.</returns>
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
        /// 저장된 설정 에셋을 찾고, 없으면 기본값 인스턴스를 만듭니다. (프로젝트에 파일을 만들지는 않습니다)
        /// </summary>
        /// <returns>사용할 설정입니다.</returns>
        private static CsvPipelineSettings FindOrCreateTransient()
        {
            List<CsvPipelineSettings> found = FindAll();

            if (found.Count > 1)
            {
                // 둘 이상이면 어느 쪽이 쓰였는지 사람이 알 수 없어, 값이 조용히 어긋납니다.
                Debug.LogWarning(
                    $"[CsvPipeline] 설정 에셋이 {found.Count}개 있습니다. 경로순 첫 번째"
                    + $"({AssetDatabase.GetAssetPath(found[0])})를 씁니다. 나머지는 지우십시오.");
            }
            _cachedExists = found.Count > 0;
            if (_cachedExists) return found[0];

            // 소비하는 프로젝트에 말없이 에셋을 만들지 않습니다. 기본값으로 도는 임시 인스턴스를 씁니다.
            var transientSettings = CreateInstance<CsvPipelineSettings>();
            transientSettings.hideFlags = HideFlags.HideAndDontSave;
            return transientSettings;
        }

        /// <summary>지정 경로에 설정 에셋을 만들고 캐시를 갱신합니다.</summary>
        /// <param name="assetPath">만들 에셋 경로입니다. (예: Assets/CsvPipelineSettings.asset)</param>
        /// <returns>만들어진 설정 에셋입니다.</returns>
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
