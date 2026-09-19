using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// Menu that force-reimports the managed CSV files and rebuilds every CSV → ScriptableObject pipeline in one pass.
    /// The AssetPostprocessor importers only fire when a CSV "changes", so use this to verify that the output assets
    /// stay identical after you edit the pipeline, or to bake a first batch of seed CSV files at once.
    /// </summary>
    public static class CsvRebuildMenu
    {
        /// <summary>Tag prefixed to the log.</summary>
        private const string TAG = "[CsvRebuild]";

        /// <summary>
        /// Raised after a full rebuild finishes. This is where you hook the project-specific finishing work that a
        /// single importer cannot do, such as gathering the baked assets to refill a catalog.
        /// </summary>
        public static event Action AfterRebuildAll;

        /// <summary>Force-reimports every CSV under the CSV root.</summary>
        [MenuItem("Tools/CSV Pipeline/Rebuild All Tables", false, 20)]
        public static void RebuildAllMenu()
        {
            int count = RebuildAll();
            if (count < 0) return;

            Debug.Log($"{TAG} Force-reimported {count} CSV files. (see the importer logs in the Console)");
        }

        /// <summary>
        /// Force-reimports every CSV under the CSV root.
        /// </summary>
        /// <returns>Number of CSV files reimported, or -1 when the root folder was not found.</returns>
        public static int RebuildAll()
        {
            string csvRoot = CsvPipelineSettings.Instance.CsvRootFolder;
            if (!AssetDatabase.IsValidFolder(csvRoot))
            {
                Debug.LogWarning(
                    $"{TAG} Could not find the CSV root folder: {csvRoot}\n"
                    + "Point it at the real folder in Project Settings ▸ CSV Pipeline.");
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
