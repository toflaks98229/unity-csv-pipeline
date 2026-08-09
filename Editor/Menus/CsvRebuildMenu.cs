using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// 관리 대상 CSV를 강제 재임포트해 모든 CSV → ScriptableObject 파이프라인을 일괄 재생성하는 메뉴입니다.
    /// AssetPostprocessor 임포터는 CSV가 "변경"될 때만 발화하므로, 파이프라인 수정 후 산출물 동일성 검증이나
    /// 최초 시드 CSV의 일괄 생성에 사용합니다.
    /// </summary>
    public static class CsvRebuildMenu
    {
        /// <summary>로그 접두 태그입니다.</summary>
        private const string TAG = "[CsvRebuild]";

        /// <summary>
        /// 전체 재빌드가 끝난 뒤 불립니다. 구운 에셋을 모아 카탈로그를 다시 채우는 등,
        /// 임포터 하나로는 할 수 없는 프로젝트별 마무리 작업을 붙이는 자리입니다.
        /// </summary>
        public static event Action AfterRebuildAll;

        /// <summary>CSV 루트의 모든 CSV를 강제 재임포트합니다.</summary>
        [MenuItem("Tools/CSV Pipeline/Rebuild All Data")]
        public static void RebuildAllMenu()
        {
            int count = RebuildAll();
            if (count < 0) return;

            Debug.Log($"{TAG} {count}개 CSV를 강제 재임포트했습니다. (Console의 임포터 로그 확인)");
        }

        /// <summary>
        /// CSV 루트의 모든 CSV를 강제 재임포트합니다.
        /// </summary>
        /// <returns>재임포트한 CSV 개수이거나, 루트 폴더를 찾지 못했으면 -1입니다.</returns>
        public static int RebuildAll()
        {
            string csvRoot = CsvPipelineSettings.Instance.CsvRootFolder;
            if (!AssetDatabase.IsValidFolder(csvRoot))
            {
                Debug.LogWarning(
                    $"{TAG} CSV 루트 폴더를 찾지 못했습니다: {csvRoot}\n"
                    + "Project Settings ▸ CSV Pipeline 에서 실제 폴더를 지정하십시오.");
                return -1;
            }

            // Unity가 .tsv 를 TextAsset으로 임포트하지 않으므로 에셋 검색이 아니라 디스크를 훑습니다.
            var paths = new List<string>();
            foreach (string file in Directory.EnumerateFiles(csvRoot, "*.*", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                if (CsvImportUtil.IsTableFile(path)) paths.Add(path);
            }
            paths.Sort(StringComparer.Ordinal);

            int count = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in paths)
                {
                    // ForceUpdate 재임포트 → 각 임포터의 OnPostprocessAllAssets가 이 경로를 처리
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    count++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();

            // 재임포트로 새 에셋이 생겼을 수 있으므로 프로젝트별 마무리 작업에 알립니다.
            AfterRebuildAll?.Invoke();

            return count;
        }
    }
}
