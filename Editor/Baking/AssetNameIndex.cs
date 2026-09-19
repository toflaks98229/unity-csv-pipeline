using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// A general-purpose index that indexes one asset type (<typeparamref name="T"/>) by asset name.
    /// (Use it when a name written in a CSV cell has to reference another asset. Optionally limited to a folder.)
    /// </summary>
    /// <typeparam name="T">Asset type to index.</typeparam>
    public class AssetNameIndex<T> where T : UnityEngine.Object
    {
        private readonly Dictionary<string, T> _byName
            = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Indexes the T assets by name in the given folder, or across the whole project when there is none.</summary>
        /// <param name="folder">Folder to search within. Empty searches the whole project.</param>
        public void Build(string folder = null)
        {
            _byName.Clear();
            ICsvAssetGateway assets = CsvAssets.Current;

            foreach (string path in assets.FindPaths($"t:{typeof(T).Name}", folder))
            {
                if (assets.Load(path, typeof(T)) is T asset) _byName[Path.GetFileNameWithoutExtension(path)] = asset;
            }
        }

        /// <summary>Looks up an asset by name. Returns null when there is none, optionally with a warning.</summary>
        /// <param name="name">Asset name to find. (without the extension)</param>
        /// <param name="logTag">When given, logs a warning under this tag if nothing is found.</param>
        /// <returns>The asset found, or null.</returns>
        public T Resolve(string name, string logTag = null)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_byName.TryGetValue(name, out T asset)) return asset;
            if (!string.IsNullOrEmpty(logTag))
                Debug.LogWarning($"{logTag} Could not find the '{typeof(T).Name}' asset '{name}'.");
            return null;
        }
    }
}
