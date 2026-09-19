using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// A gateway that writes nothing to disk. It keeps tables and assets in memory only.
    /// Use it to test the rules of baking, planning, and cleanup <b>without a Unity project</b>.
    /// <see cref="Dispose"/> cleans up the objects it created, so wrap it in a <c>using</c>.
    /// </summary>
    public sealed class MemoryAssetGateway : ICsvAssetGateway, IDisposable
    {
        private readonly Dictionary<string, string> _texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, UnityEngine.Object> _assets = new Dictionary<string, UnityEngine.Object>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Paths to treat as still referenced. Fill this in when testing the cleanup rules.</summary>
        public HashSet<string> Referenced { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Imitates a situation where the reference scan cannot be trusted. Null is the ordinary state, where it can scan.
        /// </summary>
        public string ReferenceScanBlocked { get; private set; }

        /// <summary>How many times <see cref="SaveAll"/> was called.</summary>
        public int SaveCount { get; private set; }

        /// <summary>
        /// How many times <see cref="FindPaths"/> was called.
        /// In a real store this is a whole-project search, so <b>calling it every repaint makes memory grow
        /// just from leaving the window open.</b> That accident happened once, and counting came out of it.
        /// </summary>
        public int FindPathsCount { get; private set; }

        /// <summary>The asset paths held right now.</summary>
        public IEnumerable<string> Paths => _assets.Keys;

        // ====================================================================================================
        // 준비
        // ====================================================================================================

        /// <summary>Places the table text. The folder is created along with it.</summary>
        /// <param name="path">Path of the table. (for example, "Assets/Data/Widgets.csv")</param>
        /// <param name="text">Table text.</param>
        /// <returns>Itself, so calls chain.</returns>
        public MemoryAssetGateway WithTable(string path, string text)
        {
            _texts[path] = text;
            EnsureFolder(ParentOf(path));
            return this;
        }

        /// <summary>
        /// Imitates a project where the reference scan cannot run. Use it to test that cleanup stops in such a project.
        /// </summary>
        /// <param name="reason">Reason it cannot scan. Null scans as usual.</param>
        /// <returns>Itself, so calls chain.</returns>
        public MemoryAssetGateway WithReferenceScanBlocked(string reason)
        {
            ReferenceScanBlocked = reason;
            return this;
        }

        /// <summary>Places an asset that already exists. Use it to test reference resolution or the update path.</summary>
        /// <param name="path">Asset path.</param>
        /// <param name="asset">Asset to place.</param>
        /// <returns>Itself, so calls chain.</returns>
        public MemoryAssetGateway WithAsset(string path, UnityEngine.Object asset)
        {
            _assets[path] = asset;
            EnsureFolder(ParentOf(path));
            return this;
        }

        /// <summary>Creates an asset of the given type and places it.</summary>
        /// <typeparam name="T">Type to create.</typeparam>
        /// <param name="path">Asset path.</param>
        /// <returns>The asset created.</returns>
        public T Add<T>(string path) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = Path.GetFileNameWithoutExtension(path);
            WithAsset(path, asset);
            return asset;
        }

        /// <summary>Resets the counted calls to zero. Use it to count without the calls made during setup.</summary>
        public void ResetCounters()
        {
            SaveCount = 0;
            FindPathsCount = 0;
        }

        /// <summary>Reads the asset at the path as the given type. Use it to check results in a test.</summary>
        /// <typeparam name="T">Expected type.</typeparam>
        /// <param name="path">Asset path.</param>
        /// <returns>The asset found, or null.</returns>
        public T Get<T>(string path) where T : UnityEngine.Object
            => _assets.TryGetValue(path, out UnityEngine.Object asset) ? asset as T : null;

        // ====================================================================================================
        // ICsvAssetGateway
        // ====================================================================================================

        /// <summary>Path of the placed table whose name matches.</summary>
        /// <param name="fileName">File name to look for.</param>
        /// <returns>The path found, or null.</returns>
        public string FindTablePath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            foreach (string path in _texts.Keys)
            {
                if (Path.GetFileName(path).Equals(fileName, StringComparison.OrdinalIgnoreCase)) return path;
            }
            return null;
        }

        /// <summary>Text of the placed table.</summary>
        /// <param name="path">Path to read.</param>
        /// <returns>The text, or null.</returns>
        public string ReadText(string path)
            => path != null && _texts.TryGetValue(path, out string text) ? text : null;

        /// <summary>
        /// Text of the placed table. What is placed here is already a string, so <b>an encoding problem cannot arise.</b>
        /// </summary>
        /// <param name="path">Path to read.</param>
        /// <param name="problem">Always null.</param>
        /// <returns>The text, or null.</returns>
        public string ReadText(string path, out string problem)
        {
            problem = null;
            return ReadText(path);
        }

        /// <summary>Whether the folder exists.</summary>
        /// <param name="folder">Folder to check.</param>
        /// <returns>True when it exists.</returns>
        public bool FolderExists(string folder) => !string.IsNullOrEmpty(folder) && _folders.Contains(folder);

        /// <summary>Registers the folder, up through its parents.</summary>
        /// <param name="folder">Folder to ensure.</param>
        public void EnsureFolder(string folder)
        {
            while (!string.IsNullOrEmpty(folder) && _folders.Add(folder))
            {
                folder = ParentOf(folder);
            }
        }

        /// <summary>Loads the asset, or creates it in memory when there is none.</summary>
        /// <param name="type">Type to create.</param>
        /// <param name="path">Asset path.</param>
        /// <param name="created">Receives true when it was newly created.</param>
        /// <returns>The asset loaded or created.</returns>
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

        /// <summary>Asset at the path. Null when the type does not match.</summary>
        /// <param name="path">Asset path.</param>
        /// <param name="type">Expected type.</param>
        /// <returns>The asset found, or null.</returns>
        public UnityEngine.Object Load(string path, Type type)
        {
            if (path == null || !_assets.TryGetValue(path, out UnityEngine.Object asset) || asset == null) return null;
            return type == null || type.IsInstanceOfType(asset) ? asset : null;
        }

        /// <summary>Path of the asset.</summary>
        /// <param name="asset">Target asset.</param>
        /// <returns>The path, or an empty string when there is none.</returns>
        public string PathOf(UnityEngine.Object asset)
        {
            if (asset == null) return string.Empty;

            foreach (KeyValuePair<string, UnityEngine.Object> pair in _assets)
            {
                if (ReferenceEquals(pair.Value, asset)) return pair.Key;
            }
            return string.Empty;
        }

        /// <summary>Asset paths matching the type filter. The order is fixed by path.</summary>
        /// <param name="typeFilter">Search filter. (for example, "t:WidgetData")</param>
        /// <param name="folder">Folder the search is limited to. Null searches everything.</param>
        /// <returns>The paths found.</returns>
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

        /// <summary>
        /// How many times something was dirtied. There is nowhere to write in memory, but <b>the counting itself is the value.</b>
        /// <para>
        /// The save at the end of a bake rewrites every dirtied asset. Dirtying rows whose values did not change
        /// at all means fixing one cell in a 3,000-row table rewrites 3,000 assets, and because the result is the
        /// same, that waste <b>was caught by no test.</b> Counting lets a test see it.
        /// </para>
        /// </summary>
        public int DirtyCount { get; private set; }

        /// <summary>Nothing is dirtied in memory, so it only counts the calls.</summary>
        /// <param name="asset">Target asset.</param>
        public void MarkDirty(UnityEngine.Object asset) => DirtyCount++;

        /// <summary>There is nowhere to write in memory, so it does nothing.</summary>
        /// <param name="asset">Target asset.</param>
        /// <param name="created">Whether it was newly created.</param>
        public void FlushIfCreated(UnityEngine.Object asset, bool created) { }

        /// <summary>Removes the asset from the list.</summary>
        /// <param name="path">Path to delete.</param>
        public void Delete(string path)
        {
            if (path == null || !_assets.TryGetValue(path, out UnityEngine.Object asset)) return;

            _assets.Remove(path);
            if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
        }

        /// <summary>Counts the save calls, nothing more.</summary>
        public void SaveAll() => SaveCount++;

        /// <summary>
        /// Depth of the batch scopes open right now. It counts so a test can confirm the contract that scopes nest.
        /// </summary>
        public int BatchDepth { get; private set; }

        /// <summary>How many times a batch scope was opened. A test watches this to see that baking really batches.</summary>
        public int BatchCount { get; private set; }

        /// <summary>
        /// There is no store to defer, so it only counts the depth.
        /// The counting itself is the value, because it lets a test assert that "the bake loop runs inside a batch".
        /// </summary>
        /// <returns>Handle that closes the scope.</returns>
        public IDisposable BatchEdits()
        {
            BatchCount++;
            BatchDepth++;
            return new BatchScope(this);
        }

        /// <summary>Scope handle that puts the depth back.</summary>
        private sealed class BatchScope : IDisposable
        {
            private readonly MemoryAssetGateway _owner;
            private bool _closed;

            /// <summary>Opens the scope.</summary>
            /// <param name="owner">Gateway that counts the depth.</param>
            public BatchScope(MemoryAssetGateway owner) { _owner = owner; }

            /// <summary>Closes the scope.</summary>
            public void Dispose()
            {
                if (_closed) return;
                _closed = true;
                _owner.BatchDepth--;
            }
        }

        /// <summary>Returns the candidates registered in <see cref="Referenced"/>.</summary>
        /// <param name="candidates">Paths to scan.</param>
        /// <returns>The paths that are still referenced.</returns>
        public HashSet<string> FindReferenced(IReadOnlyList<string> candidates)
        {
            var found = new HashSet<string>();
            foreach (string path in candidates)
            {
                if (Referenced.Contains(path)) found.Add(path);
            }
            return found;
        }

        /// <summary>
        /// Nothing is held. Everything lives in memory, so there is nothing to ask again and nothing to go stale.
        /// </summary>
        public void InvalidateCaches() { }

        /// <summary>Cleans up every asset object it created.</summary>
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

        /// <summary>Parent folder of the path.</summary>
        /// <param name="path">Target path.</param>
        /// <returns>The parent folder, or null when there is none.</returns>
        private static string ParentOf(string path)
        {
            int cut = path?.LastIndexOf('/') ?? -1;
            return cut <= 0 ? null : path.Substring(0, cut);
        }

        /// <summary>Whether the path sits inside the folder.</summary>
        /// <param name="path">Target path.</param>
        /// <param name="folder">Folder that bounds the search.</param>
        /// <returns>True when it is inside.</returns>
        private static bool IsInside(string path, string folder)
            => path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase);

        /// <summary>Pulls just the type name out of a "t:TypeName" filter.</summary>
        /// <param name="typeFilter">Search filter.</param>
        /// <returns>The type name, or null when the filter carries no type condition.</returns>
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

        /// <summary>Whether the asset is that type or inherits from it.</summary>
        /// <param name="asset">Asset to check.</param>
        /// <param name="typeName">Expected type name.</param>
        /// <returns>True when it matches.</returns>
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
