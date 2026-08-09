using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// CSV → ScriptableObject 임포터들이 공통으로 쓰는 에셋 폴더 라이프사이클 유틸입니다.
    /// </summary>
    public static class CsvAssetPipeline
    {
        /// <summary>
        /// 지정한 파일명(예: "LootTables.csv")을 가진 표 파일의 에셋 경로를 찾습니다.
        /// </summary>
        /// <param name="fileName">찾을 파일 이름입니다. 확장자를 포함합니다.</param>
        /// <returns>찾은 에셋 경로이거나, 없으면 null입니다.</returns>
        public static string FindCsvPath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
            foreach (string guid in AssetDatabase.FindAssets($"{nameNoExt} t:TextAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).Equals(fileName, StringComparison.OrdinalIgnoreCase)) return path;
            }

            // Unity는 .tsv 를 TextAsset으로 임포트하지 않아 위 검색에 걸리지 않습니다.
            // 설정된 CSV 루트 안을 직접 훑어 폴백합니다.
            return FindOnDisk(fileName);
        }

        /// <summary>설정된 CSV 루트 폴더 안에서 파일을 직접 찾습니다.</summary>
        /// <param name="fileName">찾을 파일 이름입니다.</param>
        /// <returns>프로젝트 상대 경로이거나, 없으면 null입니다.</returns>
        private static string FindOnDisk(string fileName)
        {
            string root = CsvPipelineSettings.Instance.CsvRootFolder;
            if (!Directory.Exists(root)) return null;

            try
            {
                foreach (string file in Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories))
                {
                    return file.Replace('\\', '/');
                }
            }
            catch (IOException)
            {
                // 폴더를 읽지 못하면 못 찾은 것으로 둡니다.
            }
            return null;
        }

        /// <summary>경로가 없으면 부모부터 순차적으로 폴더를 생성해 보장합니다. (AssetDatabase 기반)</summary>
        /// <param name="folderPath">보장할 폴더 경로입니다. (예: Assets/Data/Items)</param>
        public static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        /// <summary>
        /// CSV 원본이 사라졌음을 알립니다. 산출물은 건드리지 않습니다.
        /// </summary>
        /// <param name="folder">해당 CSV가 굽던 산출물 폴더입니다.</param>
        /// <param name="csvFile">사라진 CSV 파일 이름입니다.</param>
        /// <param name="logTag">로그 접두 태그입니다.</param>
        public static void WarnSourceRemoved(string folder, string csvFile, string logTag)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;

            // 원본이 사라졌다고 산출물 폴더를 지우지 않습니다. CSV를 잠깐 옮기거나 실수로 지우기만 해도
            // 확인 없이 폴더가 사라지는데, 그 안에는 CSV로 재생성할 수 없는 수작업 데이터(참조·아이콘·
            // 프리팹 배선)가 들어 있습니다. 게다가 에셋이 삭제되면 GUID가 바뀌어, git으로 파일을 되돌려도
            // 이를 참조하던 프리팹/씬의 링크는 돌아오지 않습니다.
            // 산출물 정리는 CSV가 실제로 존재할 때 유효 목록과 대조하는 ReconcileFolder* 경로에서만 합니다.
            Debug.LogWarning(
                $"{logTag} 원본 CSV '{csvFile}'가 사라졌지만 산출물 폴더는 보존합니다: {folder}\n" +
                "의도한 삭제라면 폴더를 직접 지우십시오. (수작업 배선이 남아 있을 수 있어 자동 삭제하지 않습니다)");
        }

        /// <summary>지정 경로의 SO를 로드하거나, 없으면 새로 생성해 반환합니다.</summary>
        /// <typeparam name="T">대상 ScriptableObject 타입입니다.</typeparam>
        /// <param name="path">에셋 경로입니다.</param>
        /// <returns>로드하거나 새로 만든 에셋입니다.</returns>
        public static T CreateOrLoad<T>(string path) where T : ScriptableObject
            => CreateOrLoad<T>(path, out _);

        /// <summary>지정 경로의 SO를 로드하거나, 없으면 새로 생성해 반환합니다.</summary>
        /// <typeparam name="T">대상 ScriptableObject 타입입니다.</typeparam>
        /// <param name="path">에셋 경로입니다.</param>
        /// <param name="created">새로 만들었으면 true를 받습니다. (생성/갱신 집계용)</param>
        /// <returns>로드하거나 새로 만든 에셋입니다.</returns>
        public static T CreateOrLoad<T>(string path, out bool created) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;

            if (created)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        /// <summary>
        /// 타입을 런타임에 정하는 경우의 <see cref="CreateOrLoad{T}(string, out bool)"/>입니다.
        /// </summary>
        /// <param name="type">만들 ScriptableObject 타입입니다.</param>
        /// <param name="path">에셋 경로입니다.</param>
        /// <param name="created">새로 만들었으면 true를 받습니다.</param>
        /// <returns>로드하거나 새로 만든 에셋입니다.</returns>
        public static ScriptableObject CreateOrLoad(Type type, string path, out bool created)
        {
            var asset = AssetDatabase.LoadAssetAtPath(path, type) as ScriptableObject;
            created = asset == null;

            if (created)
            {
                asset = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        /// <summary>
        /// 폴더에서 이번 임포트로 확정된 <paramref name="validNames"/>(파일명, 확장자 제외)에 없는 에셋을 삭제합니다.
        /// </summary>
        /// <param name="folder">정리할 산출물 폴더입니다.</param>
        /// <param name="typeFilter">AssetDatabase 검색 필터입니다. (예: "t:ItemData")</param>
        /// <param name="validNames">이번 임포트로 확정된 에셋 이름들입니다.</param>
        /// <param name="logTag">로그 접두 태그입니다.</param>
        /// <param name="report">결과를 기록할 리포트입니다. null이면 Console에 바로 남깁니다.</param>
        public static void ReconcileFolderByName(string folder, string typeFilter, ICollection<string> validNames,
                                                 string logTag, CsvImportReport report = null)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;

            var candidates = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets(typeFilter, new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!validNames.Contains(name)) candidates.Add(path);
            }

            DeleteUnreferenced(candidates, logTag, report);
        }

        /// <summary>
        /// 폴더에서 이번 임포트로 확정된 <paramref name="validPaths"/>(에셋 경로)에 없는 에셋을 삭제합니다.
        /// (인플레이스 갱신 에셋을 오삭제하지 않도록 경로 기반으로 대조)
        /// </summary>
        /// <param name="folder">정리할 산출물 폴더입니다.</param>
        /// <param name="typeFilter">AssetDatabase 검색 필터입니다. (예: "t:CardData")</param>
        /// <param name="validPaths">이번 임포트로 확정된 에셋 경로들입니다.</param>
        /// <param name="logTag">로그 접두 태그입니다.</param>
        /// <param name="report">결과를 기록할 리포트입니다. null이면 Console에 바로 남깁니다.</param>
        public static void ReconcileFolderByPath(string folder, string typeFilter, ICollection<string> validPaths,
                                                 string logTag, CsvImportReport report = null)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;

            var candidates = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets(typeFilter, new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!validPaths.Contains(path)) candidates.Add(path);
            }

            DeleteUnreferenced(candidates, logTag, report);
        }

        /// <summary>
        /// 삭제 후보 중 <b>아무도 참조하지 않는 것만</b> 지웁니다. 참조가 남은 것은 경고만 남기고 보존합니다.
        /// </summary>
        /// <param name="candidates">CSV에서 사라져 삭제 대상이 된 에셋 경로들입니다.</param>
        /// <param name="logTag">로그 접두 태그입니다.</param>
        /// <param name="report">결과를 기록할 리포트입니다. null이면 Console에 바로 남깁니다.</param>
        private static void DeleteUnreferenced(List<string> candidates, string logTag, CsvImportReport report)
        {
            if (candidates.Count == 0) return;

            HashSet<string> referenced = FindReferenced(candidates);

            foreach (string path in candidates)
            {
                if (referenced.Contains(path))
                {
                    string keep = $"표에서 사라졌지만 아직 참조 중이라 보존합니다: {path}. "
                                + "Id 오타일 수 있습니다. 정말 삭제하려면 참조를 먼저 끊고 직접 지우십시오.";

                    if (report != null)
                    {
                        report.CountPreserved();
                        report.Warn(keep, 0, null, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path));
                    }
                    else
                    {
                        Debug.LogWarning($"{logTag} {keep}");
                    }
                    continue;
                }

                AssetDatabase.DeleteAsset(path);

                if (report != null) report.CountDeleted();
                else Debug.Log($"{logTag} Deleted obsolete asset: {path}");
            }
        }

        /// <summary>
        /// 후보 에셋들의 GUID가 프로젝트의 씬·프리팹·에셋 어딘가에 등장하는지 한 번의 훑기로 조사합니다.
        /// </summary>
        /// <param name="candidates">조사할 에셋 경로들입니다.</param>
        /// <returns>참조가 발견된 에셋 경로 집합입니다.</returns>
        private static HashSet<string> FindReferenced(List<string> candidates)
        {
            var referenced = new HashSet<string>();
            var guidToPath = new Dictionary<string, string>();
            var candidateSet = new HashSet<string>(candidates, StringComparer.OrdinalIgnoreCase);

            foreach (string path in candidates)
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid)) guidToPath[guid] = path;
            }
            if (guidToPath.Count == 0) return referenced;

            foreach (string file in EnumerateReferenceFiles())
            {
                // 후보 자신과 후보끼리의 참조는 세지 않습니다.
                if (candidateSet.Contains(file)) continue;

                string text;
                try { text = File.ReadAllText(file); }
                catch (IOException) { continue; }   // 다른 프로그램이 잡고 있는 파일은 건너뜁니다.

                foreach (KeyValuePair<string, string> pair in guidToPath)
                {
                    if (referenced.Contains(pair.Value)) continue;
                    if (text.Contains(pair.Key)) referenced.Add(pair.Value);
                }

                if (referenced.Count == guidToPath.Count) break;   // 전부 찾았으면 더 볼 필요가 없습니다.
            }

            return referenced;
        }

        /// <summary>참조를 담을 수 있는 파일들을 열거합니다. (씬·프리팹·에셋과 프로젝트 설정)</summary>
        /// <returns>조사 대상 파일 경로들입니다.</returns>
        private static IEnumerable<string> EnumerateReferenceFiles()
        {
            foreach (string file in Directory.EnumerateFiles("Assets", "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file);
                if (ext.Equals(".unity", StringComparison.OrdinalIgnoreCase)
                 || ext.Equals(".prefab", StringComparison.OrdinalIgnoreCase)
                 || ext.Equals(".asset", StringComparison.OrdinalIgnoreCase))
                {
                    yield return file.Replace('\\', '/');
                }
            }

            // 빌드 씬 목록·프리로드 에셋처럼 프로젝트 설정이 직접 붙잡고 있는 참조도 있습니다.
            if (!Directory.Exists("ProjectSettings")) yield break;

            foreach (string file in Directory.EnumerateFiles("ProjectSettings", "*.asset"))
            {
                yield return file.Replace('\\', '/');
            }
        }
    }
}
