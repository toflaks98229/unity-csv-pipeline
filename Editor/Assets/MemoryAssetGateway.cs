using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// 아무것도 디스크에 쓰지 않는 게이트웨이입니다. 표와 에셋을 메모리에만 둡니다.
    /// 굽기·계획·정리의 규칙을 <b>Unity 프로젝트 없이</b> 검사할 때 씁니다.
    /// 만든 객체는 <see cref="Dispose"/>가 정리하므로 <c>using</c>으로 감싸십시오.
    /// </summary>
    public sealed class MemoryAssetGateway : ICsvAssetGateway, IDisposable
    {
        private readonly Dictionary<string, string> _texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, UnityEngine.Object> _assets = new Dictionary<string, UnityEngine.Object>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>참조가 남았다고 볼 경로들입니다. 정리 규칙을 검사할 때 채웁니다.</summary>
        public HashSet<string> Referenced { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 참조 조사를 믿을 수 없는 상황을 흉내 냅니다. null이면 조사할 수 있는 평소 상태입니다.
        /// </summary>
        public string ReferenceScanBlocked { get; private set; }

        /// <summary><see cref="SaveAll"/>이 불린 횟수입니다.</summary>
        public int SaveCount { get; private set; }

        /// <summary>
        /// <see cref="FindPaths"/>가 불린 횟수입니다.
        /// 실제 저장소에서 이것은 프로젝트 전체 검색이라, <b>그리기마다 부르면 창을 열어 둔 것만으로
        /// 메모리가 계속 늘어납니다.</b> 그 사고를 한 번 겪어 세어 두게 했습니다.
        /// </summary>
        public int FindPathsCount { get; private set; }

        /// <summary>지금 들고 있는 에셋 경로들입니다.</summary>
        public IEnumerable<string> Paths => _assets.Keys;

        // ====================================================================================================
        // 준비
        // ====================================================================================================

        /// <summary>표 원문을 놓습니다. 폴더도 함께 만들어집니다.</summary>
        /// <param name="path">표의 경로입니다. (예: "Assets/Data/Widgets.csv")</param>
        /// <param name="text">표 원문입니다.</param>
        /// <returns>이어 쓰기 좋도록 자기 자신입니다.</returns>
        public MemoryAssetGateway WithTable(string path, string text)
        {
            _texts[path] = text;
            EnsureFolder(ParentOf(path));
            return this;
        }

        /// <summary>
        /// 참조 조사를 할 수 없는 프로젝트를 흉내 냅니다. 그런 프로젝트에서 정리가 멈추는지 검사할 때 씁니다.
        /// </summary>
        /// <param name="reason">조사할 수 없는 이유입니다. null이면 평소대로 조사합니다.</param>
        /// <returns>이어 쓰기 좋도록 자기 자신입니다.</returns>
        public MemoryAssetGateway WithReferenceScanBlocked(string reason)
        {
            ReferenceScanBlocked = reason;
            return this;
        }

        /// <summary>이미 있는 에셋을 놓습니다. 참조 해석이나 갱신 경로를 검사할 때 씁니다.</summary>
        /// <param name="path">에셋 경로입니다.</param>
        /// <param name="asset">놓을 에셋입니다.</param>
        /// <returns>이어 쓰기 좋도록 자기 자신입니다.</returns>
        public MemoryAssetGateway WithAsset(string path, UnityEngine.Object asset)
        {
            _assets[path] = asset;
            EnsureFolder(ParentOf(path));
            return this;
        }

        /// <summary>지정 타입의 에셋을 만들어 놓습니다.</summary>
        /// <typeparam name="T">만들 타입입니다.</typeparam>
        /// <param name="path">에셋 경로입니다.</param>
        /// <returns>만든 에셋입니다.</returns>
        public T Add<T>(string path) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = Path.GetFileNameWithoutExtension(path);
            WithAsset(path, asset);
            return asset;
        }

        /// <summary>세어 둔 호출 횟수를 0으로 되돌립니다. 준비 단계의 호출을 빼고 셀 때 씁니다.</summary>
        public void ResetCounters()
        {
            SaveCount = 0;
            FindPathsCount = 0;
        }

        /// <summary>경로의 에셋을 지정 타입으로 읽습니다. 검사에서 결과를 확인할 때 씁니다.</summary>
        /// <typeparam name="T">기대하는 타입입니다.</typeparam>
        /// <param name="path">에셋 경로입니다.</param>
        /// <returns>찾은 에셋이거나 null입니다.</returns>
        public T Get<T>(string path) where T : UnityEngine.Object
            => _assets.TryGetValue(path, out UnityEngine.Object asset) ? asset as T : null;

        // ====================================================================================================
        // ICsvAssetGateway
        // ====================================================================================================

        /// <summary>놓아 둔 표 중 이름이 맞는 것의 경로입니다.</summary>
        /// <param name="fileName">찾을 파일 이름입니다.</param>
        /// <returns>찾은 경로이거나 null입니다.</returns>
        public string FindTablePath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            foreach (string path in _texts.Keys)
            {
                if (Path.GetFileName(path).Equals(fileName, StringComparison.OrdinalIgnoreCase)) return path;
            }
            return null;
        }

        /// <summary>놓아 둔 표의 원문입니다.</summary>
        /// <param name="path">읽을 경로입니다.</param>
        /// <returns>원문이거나 null입니다.</returns>
        public string ReadText(string path)
            => path != null && _texts.TryGetValue(path, out string text) ? text : null;

        /// <summary>폴더가 있는지 여부입니다.</summary>
        /// <param name="folder">확인할 폴더입니다.</param>
        /// <returns>있으면 true입니다.</returns>
        public bool FolderExists(string folder) => !string.IsNullOrEmpty(folder) && _folders.Contains(folder);

        /// <summary>폴더를 부모까지 등록합니다.</summary>
        /// <param name="folder">보장할 폴더입니다.</param>
        public void EnsureFolder(string folder)
        {
            while (!string.IsNullOrEmpty(folder) && _folders.Add(folder))
            {
                folder = ParentOf(folder);
            }
        }

        /// <summary>에셋을 로드하거나, 없으면 메모리에 만듭니다.</summary>
        /// <param name="type">만들 타입입니다.</param>
        /// <param name="path">에셋 경로입니다.</param>
        /// <param name="created">새로 만들었으면 true를 받습니다.</param>
        /// <returns>로드하거나 만든 에셋입니다.</returns>
        public ScriptableObject CreateOrLoad(Type type, string path, out bool created)
        {
            if (_assets.TryGetValue(path, out UnityEngine.Object existing) && existing is ScriptableObject found)
            {
                created = false;
                return found;
            }

            created = true;
            var asset = ScriptableObject.CreateInstance(type);
            asset.name = Path.GetFileNameWithoutExtension(path);
            WithAsset(path, asset);
            return asset;
        }

        /// <summary>경로의 에셋입니다. 타입이 맞지 않으면 null입니다.</summary>
        /// <param name="path">에셋 경로입니다.</param>
        /// <param name="type">기대하는 타입입니다.</param>
        /// <returns>찾은 에셋이거나 null입니다.</returns>
        public UnityEngine.Object Load(string path, Type type)
        {
            if (path == null || !_assets.TryGetValue(path, out UnityEngine.Object asset) || asset == null) return null;
            return type == null || type.IsInstanceOfType(asset) ? asset : null;
        }

        /// <summary>에셋의 경로입니다.</summary>
        /// <param name="asset">대상 에셋입니다.</param>
        /// <returns>경로이거나, 없으면 빈 문자열입니다.</returns>
        public string PathOf(UnityEngine.Object asset)
        {
            if (asset == null) return string.Empty;

            foreach (KeyValuePair<string, UnityEngine.Object> pair in _assets)
            {
                if (ReferenceEquals(pair.Value, asset)) return pair.Key;
            }
            return string.Empty;
        }

        /// <summary>타입 필터에 맞는 에셋 경로들입니다. 순서는 경로순으로 고정합니다.</summary>
        /// <param name="typeFilter">검색 필터입니다. (예: "t:WidgetData")</param>
        /// <param name="folder">검색 범위 폴더입니다. null이면 전체입니다.</param>
        /// <returns>찾은 경로들입니다.</returns>
        public IReadOnlyList<string> FindPaths(string typeFilter, string folder = null)
        {
            FindPathsCount++;

            string typeName = TypeNameOf(typeFilter);
            var paths = new List<string>();

            foreach (KeyValuePair<string, UnityEngine.Object> pair in _assets)
            {
                if (pair.Value == null) continue;
                if (folder != null && !IsInside(pair.Key, folder)) continue;
                if (typeName != null && !IsOfType(pair.Value, typeName)) continue;

                paths.Add(pair.Key);
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        /// <summary>메모리에는 더럽힘이 없어 아무것도 하지 않습니다.</summary>
        /// <param name="asset">대상 에셋입니다.</param>
        public void MarkDirty(UnityEngine.Object asset) { }

        /// <summary>메모리에는 쓸 곳이 없어 아무것도 하지 않습니다.</summary>
        /// <param name="asset">대상 에셋입니다.</param>
        /// <param name="created">새로 만든 것인지 여부입니다.</param>
        public void FlushIfCreated(UnityEngine.Object asset, bool created) { }

        /// <summary>에셋을 목록에서 지웁니다.</summary>
        /// <param name="path">지울 경로입니다.</param>
        public void Delete(string path)
        {
            if (path == null || !_assets.TryGetValue(path, out UnityEngine.Object asset)) return;

            _assets.Remove(path);
            if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
        }

        /// <summary>저장 호출만 셉니다.</summary>
        public void SaveAll() => SaveCount++;

        /// <summary>후보 중 <see cref="Referenced"/>에 등록된 것을 돌려줍니다.</summary>
        /// <param name="candidates">조사할 경로들입니다.</param>
        /// <returns>참조가 남은 경로들입니다.</returns>
        public HashSet<string> FindReferenced(IReadOnlyList<string> candidates)
        {
            var found = new HashSet<string>();
            foreach (string path in candidates)
            {
                if (Referenced.Contains(path)) found.Add(path);
            }
            return found;
        }

        /// <summary>만들어 둔 에셋 객체를 모두 정리합니다.</summary>
        public void Dispose()
        {
            foreach (UnityEngine.Object asset in _assets.Values)
            {
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            }
            _assets.Clear();
        }

        // ====================================================================================================
        // 보조
        // ====================================================================================================

        /// <summary>경로의 부모 폴더입니다.</summary>
        /// <param name="path">대상 경로입니다.</param>
        /// <returns>부모 폴더이거나, 없으면 null입니다.</returns>
        private static string ParentOf(string path)
        {
            int cut = path?.LastIndexOf('/') ?? -1;
            return cut <= 0 ? null : path.Substring(0, cut);
        }

        /// <summary>경로가 폴더 안에 있는지 여부입니다.</summary>
        /// <param name="path">대상 경로입니다.</param>
        /// <param name="folder">범위 폴더입니다.</param>
        /// <returns>안에 있으면 true입니다.</returns>
        private static bool IsInside(string path, string folder)
            => path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase);

        /// <summary>"t:TypeName" 필터에서 타입 이름만 뽑습니다.</summary>
        /// <param name="typeFilter">검색 필터입니다.</param>
        /// <returns>타입 이름이거나, 타입 조건이 없으면 null입니다.</returns>
        private static string TypeNameOf(string typeFilter)
        {
            if (string.IsNullOrEmpty(typeFilter)) return null;

            int cut = typeFilter.IndexOf("t:", StringComparison.OrdinalIgnoreCase);
            if (cut < 0) return null;

            string name = typeFilter.Substring(cut + 2).Trim();
            int space = name.IndexOf(' ');
            if (space >= 0) name = name.Substring(0, space);

            return name.Equals("Object", StringComparison.Ordinal) ? null : name;
        }

        /// <summary>에셋이 그 타입이거나 그 타입을 상속하는지 여부입니다.</summary>
        /// <param name="asset">검사할 에셋입니다.</param>
        /// <param name="typeName">기대하는 타입 이름입니다.</param>
        /// <returns>맞으면 true입니다.</returns>
        private static bool IsOfType(UnityEngine.Object asset, string typeName)
        {
            for (Type type = asset.GetType(); type != null; type = type.BaseType)
            {
                if (type.Name.Equals(typeName, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
