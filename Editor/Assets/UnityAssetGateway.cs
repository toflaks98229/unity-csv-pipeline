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

            // 이 조회는 프로젝트 전체 검색입니다. 창의 훑기가 표마다 한 번씩 부르므로,
            // 한 번 찾은 것은 들고 있다가 에셋이 바뀔 때 버립니다.
            if (_tablePaths == null) _tablePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (_tablePaths.TryGetValue(fileName, out string cached)) return cached;

            string found = SearchTablePath(fileName);
            _tablePaths[fileName] = found;
            return found;
        }

        /// <summary>표 파일을 실제로 찾습니다. 들고 있는 것이 없을 때만 불립니다.</summary>
        /// <param name="fileName">찾을 파일 이름입니다.</param>
        /// <returns>찾은 경로이거나 null입니다.</returns>
        private static string SearchTablePath(string fileName)
        {
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
                InvalidateCaches();
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
            if (string.IsNullOrEmpty(path)) return;

            AssetDatabase.DeleteAsset(path);
            InvalidateCaches();
        }

        /// <summary>더럽혀진 에셋을 모두 저장합니다.</summary>
        public void SaveAll()
        {
            AssetDatabase.SaveAssets();
            InvalidateCaches();
        }

        // ====================================================================================================
        // 참조 조사
        // ====================================================================================================

        /// <summary>
        /// 참조 조사를 믿을 수 없으면 그 이유입니다. <b>이 구현에서는 언제나 null입니다.</b>
        /// <para>
        /// 예전에는 씬·프리팹을 <b>글자로 읽어</b> GUID 문자열을 찾았고, 그래서 프로젝트의
        /// Asset Serialization 이 <c>Force Text</c> 가 아니면 무엇을 물어도 "참조 없음" 이 나왔습니다.
        /// 지금은 AssetDatabase 가 임포트할 때 만들어 둔 <b>의존성 그래프</b>에 묻습니다.
        /// 그 그래프는 파일이 글자로 저장됐는지 이진으로 저장됐는지와 무관합니다.
        /// </para>
        /// <para>
        /// 그래도 이 자리를 남겨 둡니다. 게이트웨이가 <b>"모른다"고 말할 수 있어야</b> 정리를
        /// 멈출 수 있고, 그 규칙은 이 구현 하나의 사정이 아니라 계약이기 때문입니다.
        /// </para>
        /// </summary>
        public string ReferenceScanBlocked => null;

        /// <summary>
        /// 후보 중 <b>다른 곳에서 쓰이고 있는 것</b>을 가려냅니다.
        /// <para>
        /// 프로젝트의 씬·프리팹·에셋이 무엇을 쓰는지 AssetDatabase 에 묻고, 그 답 안에 후보가
        /// 있는지 봅니다. <b>후보끼리의 참조는 세지 않습니다</b> — 함께 사라질 것들이 서로를
        /// 붙잡아 주면 아무것도 정리되지 않습니다.
        /// </para>
        /// </summary>
        /// <param name="candidates">조사할 에셋 경로들입니다.</param>
        /// <returns>참조가 발견된 경로들입니다.</returns>
        public HashSet<string> FindReferenced(IReadOnlyList<string> candidates)
        {
            var referenced = new HashSet<string>();
            if (candidates == null || candidates.Count == 0) return referenced;

            var candidateSet = new HashSet<string>(candidates, StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, string[]> holder in Dependencies())
            {
                if (candidateSet.Contains(holder.Key)) continue;

                foreach (string used in holder.Value)
                {
                    if (candidateSet.Contains(used)) referenced.Add(used);
                }

                if (referenced.Count == candidateSet.Count) return referenced;   // 더 볼 것이 없습니다.
            }

            AddPreloaded(candidateSet, referenced);
            return referenced;
        }

        /// <summary>
        /// 프로젝트 설정이 직접 붙잡고 있는 참조를 더합니다.
        /// 프리로드 목록은 에셋이 아니라 설정에 적혀 있어, 에셋만 훑어서는 보이지 않습니다.
        /// </summary>
        /// <param name="candidates">조사 중인 후보들입니다.</param>
        /// <param name="referenced">참조가 발견된 경로들입니다. 여기에 더합니다.</param>
        private static void AddPreloaded(HashSet<string> candidates, HashSet<string> referenced)
        {
            foreach (UnityEngine.Object preloaded in PlayerSettings.GetPreloadedAssets())
            {
                if (preloaded == null) continue;

                string path = AssetDatabase.GetAssetPath(preloaded);
                if (!string.IsNullOrEmpty(path) && candidates.Contains(path)) referenced.Add(path);
            }
        }

        /// <summary>
        /// 에셋마다 <b>무엇을 쓰는지</b>를 모아 둔 것입니다. 없으면 이때 만듭니다.
        /// <para>
        /// 표 한 장을 정리할 때마다 프로젝트 전체를 다시 묻는 것이 이 도구의 가장 큰 비용이었습니다.
        /// 창의 훑기는 표 수만큼 그것을 되풀이합니다. 한 번 모아 두고, <b>에셋이 바뀌면 버립니다.</b>
        /// </para>
        /// </summary>
        /// <returns>홀더 경로 → 그 에셋이 쓰는 경로들입니다.</returns>
        private Dictionary<string, string[]> Dependencies()
        {
            if (_dependencies != null) return _dependencies;

            var map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!CanHoldReferences(path)) continue;

                map[path] = AssetDatabase.GetDependencies(path, false);
            }

            _dependencies = map;
            return map;
        }

        /// <summary>참조를 담을 수 있는 에셋인지 봅니다. (씬·프리팹·에셋)</summary>
        /// <param name="path">볼 경로입니다.</param>
        /// <returns>담을 수 있으면 true입니다.</returns>
        private static bool CanHoldReferences(string path)
        {
            // 패키지 안의 에셋은 소비 프로젝트가 굽는 산출물을 참조하지 않습니다.
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return false;

            return path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase);
        }

        // ====================================================================================================
        // 들고 있는 것
        // ====================================================================================================

        /// <summary>에셋마다 무엇을 쓰는지입니다. null이면 아직 모으지 않았거나 버린 것입니다.</summary>
        private Dictionary<string, string[]> _dependencies;

        /// <summary>표 파일 이름 → 경로입니다. 찾지 못한 것은 null로 기억합니다.</summary>
        private Dictionary<string, string> _tablePaths;

        /// <summary>
        /// 들고 있던 것을 버립니다. <b>에셋이 하나라도 바뀌면 불려야 합니다.</b>
        /// 낡은 채로 두면 이미 사라진 참조를 근거로 지우지 않거나, 새로 생긴 참조를 못 보고 지웁니다.
        /// </summary>
        public void InvalidateCaches()
        {
            _dependencies = null;
            _tablePaths = null;
        }
    }
}
