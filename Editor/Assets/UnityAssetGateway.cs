using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// 실제 Unity AssetDatabase에 닿는 구현입니다. 파이프라인에서 <b>Unity에 매인 코드는 여기에만</b> 있습니다.
    /// </summary>
    public sealed class UnityAssetGateway : ICsvAssetGateway
    {
        /// <summary>지정한 이름의 표 파일 경로를 찾습니다.</summary>
        /// <param name="fileName">찾을 파일 이름입니다.</param>
        /// <returns>찾은 경로이거나 null입니다.</returns>
        public string FindTablePath(string fileName)
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
        /// <returns>프로젝트 상대 경로이거나 null입니다.</returns>
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

        /// <summary>표 파일의 원문을 읽습니다. TextAsset이 아니면 디스크에서 직접 읽습니다.</summary>
        /// <param name="path">읽을 경로입니다.</param>
        /// <returns>원문이거나 null입니다.</returns>
        public string ReadText(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset != null) return asset.text;

            try
            {
                string full = Path.GetFullPath(path);
                return File.Exists(full) ? File.ReadAllText(full) : null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        /// <summary>폴더가 있는지 여부입니다.</summary>
        /// <param name="folder">확인할 폴더입니다.</param>
        /// <returns>있으면 true입니다.</returns>
        public bool FolderExists(string folder)
            => !string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder);

        /// <summary>없으면 부모부터 순차적으로 폴더를 만듭니다.</summary>
        /// <param name="folder">보장할 폴더입니다.</param>
        public void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        /// <summary>지정 경로의 에셋을 로드하거나, 없으면 만듭니다.</summary>
        /// <param name="type">만들 타입입니다.</param>
        /// <param name="path">에셋 경로입니다.</param>
        /// <param name="created">새로 만들었으면 true를 받습니다.</param>
        /// <returns>로드하거나 만든 에셋입니다.</returns>
        public ScriptableObject CreateOrLoad(Type type, string path, out bool created)
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

        /// <summary>지정 경로의 에셋을 로드합니다.</summary>
        /// <param name="path">에셋 경로입니다.</param>
        /// <param name="type">기대하는 타입입니다.</param>
        /// <returns>찾은 에셋이거나 null입니다.</returns>
        public UnityEngine.Object Load(string path, Type type)
            => string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath(path, type);

        /// <summary>에셋의 경로입니다.</summary>
        /// <param name="asset">대상 에셋입니다.</param>
        /// <returns>경로입니다.</returns>
        public string PathOf(UnityEngine.Object asset)
            => asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset);

        /// <summary>타입 필터에 맞는 에셋 경로들을 찾습니다.</summary>
        /// <param name="typeFilter">검색 필터입니다.</param>
        /// <param name="folder">검색 범위 폴더입니다. null이면 전체입니다.</param>
        /// <returns>찾은 경로들입니다.</returns>
        public IReadOnlyList<string> FindPaths(string typeFilter, string folder = null)
        {
            string[] guids = string.IsNullOrEmpty(folder)
                ? AssetDatabase.FindAssets(typeFilter)
                : (AssetDatabase.IsValidFolder(folder)
                    ? AssetDatabase.FindAssets(typeFilter, new[] { folder })
                    : Array.Empty<string>());

            var paths = new List<string>(guids.Length);
            foreach (string guid in guids) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            return paths;
        }

        /// <summary>에셋이 바뀌었음을 표시합니다.</summary>
        /// <param name="asset">대상 에셋입니다.</param>
        public void MarkDirty(UnityEngine.Object asset)
        {
            if (asset != null) EditorUtility.SetDirty(asset);
        }

        /// <summary>방금 만든 에셋이면 값을 곧바로 씁니다.</summary>
        /// <param name="asset">대상 에셋입니다.</param>
        /// <param name="created">새로 만든 것인지 여부입니다.</param>
        public void FlushIfCreated(UnityEngine.Object asset, bool created)
        {
            if (created && asset != null) AssetDatabase.SaveAssetIfDirty(asset);
        }

        /// <summary>에셋을 지웁니다.</summary>
        /// <param name="path">지울 경로입니다.</param>
        public void Delete(string path)
        {
            if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
        }

        /// <summary>더럽혀진 에셋을 모두 저장합니다.</summary>
        public void SaveAll() => AssetDatabase.SaveAssets();

        /// <summary>
        /// 참조 조사를 믿을 수 없으면 그 이유입니다. 믿을 수 있으면 null입니다.
        /// <para>
        /// 이 조사는 씬·프리팹·에셋을 <b>글자로 읽어</b> GUID 문자열을 찾습니다. 그래서 프로젝트의
        /// Asset Serialization이 <c>Force Text</c>가 아니면 GUID가 글자로 존재하지 않아 <b>무엇을 찾든
        /// 언제나 "참조 없음"</b>이 나옵니다. 그 답을 그대로 받으면 씬이 쓰고 있는 에셋을 경고 없이
        /// 지우게 되고, 그러면 GUID가 사라져 git으로 파일을 되돌려도 배선이 돌아오지 않습니다.
        /// </para>
        /// <para>안전장치가 꺼진 줄 모르는 것보다 <b>정리를 멈추는 편</b>이 낫습니다.</para>
        /// </summary>
        public string ReferenceScanBlocked
        {
            get
            {
                if (EditorSettings.serializationMode == SerializationMode.ForceText) return null;

                return "Asset Serialization이 Force Text가 아니라 참조를 조사할 수 없습니다. "
                     + "무엇이 이 에셋을 쓰고 있는지 알 수 없으므로 지우지 않고 남깁니다. "
                     + "Project Settings ▸ Editor ▸ Asset Serialization을 Force Text로 두면 조사할 수 있습니다.";
            }
        }

        /// <summary>
        /// 후보들의 GUID가 프로젝트의 씬·프리팹·에셋 어딘가에 등장하는지 한 번의 훑기로 조사합니다.
        /// </summary>
        /// <param name="candidates">조사할 에셋 경로들입니다.</param>
        /// <returns>참조가 발견된 경로들입니다.</returns>
        public HashSet<string> FindReferenced(IReadOnlyList<string> candidates)
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
