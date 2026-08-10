using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// 특정 에셋 타입(<typeparamref name="T"/>)을 에셋 이름으로 색인하는 범용 인덱스입니다.
    /// (CSV 셀에 적힌 이름으로 다른 에셋을 참조해야 할 때 사용. 선택적으로 폴더 범위를 한정)
    /// </summary>
    /// <typeparam name="T">색인할 에셋 타입입니다.</typeparam>
    public class AssetNameIndex<T> where T : Object
    {
        private readonly Dictionary<string, T> _byName = new Dictionary<string, T>();

        /// <summary>지정 폴더(없으면 프로젝트 전체)에서 T 에셋을 이름으로 색인합니다.</summary>
        /// <param name="folder">검색 범위 폴더입니다. 비우면 프로젝트 전체입니다.</param>
        public void Build(string folder = null)
        {
            _byName.Clear();
            ICsvAssetGateway assets = CsvAssets.Current;

            foreach (string path in assets.FindPaths($"t:{typeof(T).Name}", folder))
            {
                if (assets.Load(path, typeof(T)) is T asset) _byName[Path.GetFileNameWithoutExtension(path)] = asset;
            }
        }

        /// <summary>이름으로 에셋을 조회합니다. 없으면 null(옵션에 따라 경고).</summary>
        /// <param name="name">찾을 에셋 이름입니다. (확장자 제외)</param>
        /// <param name="logTag">지정하면 못 찾았을 때 이 태그로 경고를 남깁니다.</param>
        /// <returns>찾은 에셋이거나 null입니다.</returns>
        public T Resolve(string name, string logTag = null)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_byName.TryGetValue(name, out T asset)) return asset;
            if (!string.IsNullOrEmpty(logTag))
                Debug.LogWarning($"{logTag} '{typeof(T).Name}' 에셋 '{name}'을(를) 찾지 못했습니다.");
            return null;
        }
    }
}
