using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CsvPipeline
{
    /// <summary>
    /// The implementation that touches the real Unity AssetDatabase. In the pipeline, <b>Unity-bound code lives only here</b>.
    /// </summary>
    public sealed class UnityAssetGateway : ICsvAssetGateway
    {
        /// <summary>Finds the path of the table file with the given name.</summary>
        /// <param name="fileName">File name to find.</param>
        /// <returns>The path found, or null.</returns>
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

        /// <summary>Actually searches for the table file. Called only when nothing is held in the cache.</summary>
        /// <param name="fileName">File name to find.</param>
        /// <returns>The path found, or null.</returns>
        private static string SearchTablePath(string fileName)
        {
            string nameNoExt = Path.GetFileNameWithoutExtension(fileName);

            var matches = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets($"{nameNoExt} t:TextAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).Equals(fileName, StringComparison.OrdinalIgnoreCase)) matches.Add(path);
            }

            if (matches.Count == 0)
            {
                // Unity는 .tsv 를 TextAsset으로 임포트하지 않아 위 검색에 걸리지 않습니다.
                // 설정된 CSV 루트 안을 직접 훑어 폴백합니다.
                return FindOnDisk(fileName);
            }

            // 하나뿐이면 물어볼 것이 없습니다.
            if (matches.Count == 1) return matches[0];

            // 둘 이상이면 어느 것을 굽는지가 FindAssets의 검색 순서로 정해지고, 그 순서는 보장되지
            // 않습니다. 같은 표를 다시 구우면 다른 파일을 읽을 수 있고 아무 말도 남지 않았습니다.
            // 산출물 폴더를 적지 않은 선언은 이 경로를 기준으로 폴더까지 정하므로, 고른 것이 바뀌면
            // 구워지는 자리도 함께 옮겨 갑니다. 샘플을 새 판으로 다시 가져오면 바로 이 상태가 됩니다.
            //
            // 참조 에셋 이름이 겹칠 때와 달리 여기서는 <b>고르지 않을 수가 없습니다</b> — 아무것도
            // 굽지 않으면 사람에게 남는 길이 없습니다. 그래서 순서에 맡기는 대신 경로순으로 못박고,
            // 무엇을 골랐고 무엇이 더 있는지 전부 적어 알립니다.
            matches.Sort(StringComparer.Ordinal);

            Debug.LogWarning(
                $"[CsvPipeline] There are {matches.Count} tables named '{fileName}'. Using the first one in path order.\n"
                + $"  Using → {matches[0]}\n"
                + string.Join("\n", matches.GetRange(1, matches.Count - 1).ConvertAll(p => $"  Other → {p}"))
                + "\n  Delete one of them or rename it. If [CsvAsset] does not state an OutputFolder,"
                + " the output assets sit next to the table that was picked, so which one is picked also moves where they are baked.");

            return matches[0];
        }

        /// <summary>Searches the configured CSV root folder for the file directly.</summary>
        /// <param name="fileName">File name to find.</param>
        /// <returns>A project-relative path, or null.</returns>
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

        /// <summary>Reads the raw text of a table file.</summary>
        /// <param name="path">Path to read.</param>
        /// <returns>The raw text, or null.</returns>
        public string ReadText(string path) => ReadText(path, out _);

        /// <summary>
        /// Reads the raw text of a table file, and reports the reason when it cannot be read.
        /// <para>
        /// <b>Reads the bytes from disk first.</b> <see cref="TextAsset"/> always interprets content as
        /// UTF-8, so it returns a table saved in CP949 or UTF-16 as <b>mojibake, silently</b>.
        /// Baking in that state puts the broken values into the assets, and the next export writes them
        /// back to the table, overwriting the source as well. Looking at the bytes directly lets
        /// <see cref="CsvText"/> stop right there.
        /// </para>
        /// <para>
        /// Only when the read from disk fails does it fall back to <see cref="TextAsset"/>. That covers
        /// assets that exist only in memory, and running on top of a virtual file system.
        /// </para>
        /// </summary>
        /// <param name="path">Path to read.</param>
        /// <param name="problem">Receives the reason the read failed.</param>
        /// <returns>The raw text, or null.</returns>
        public string ReadText(string path, out string problem)
        {
            problem = null;
            if (string.IsNullOrEmpty(path)) return null;

            byte[] bytes = null;
            try
            {
                string full = Path.GetFullPath(path);
                if (File.Exists(full)) bytes = File.ReadAllBytes(full);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                problem = $"Could not open the file: {e.Message}";
                return null;
            }

            if (bytes != null)
            {
                if (CsvText.TryDecode(bytes, out string text, out problem)) return text;
                return null;
            }

            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            return asset != null ? asset.text : null;
        }

        /// <summary>Whether the folder exists.</summary>
        /// <param name="folder">Folder to check.</param>
        /// <returns>True if it exists.</returns>
        public bool FolderExists(string folder)
            => !string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder);

        /// <summary>Creates the folder, and each missing parent before it, when it does not exist.</summary>
        /// <param name="folder">Folder to ensure.</param>
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

        /// <summary>Loads the asset at the given path, or creates it when there is none.</summary>
        /// <param name="type">Type to create.</param>
        /// <param name="path">Asset path.</param>
        /// <param name="created">Receives true when the asset was newly created.</param>
        /// <returns>The asset that was loaded or created.</returns>
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

        /// <summary>Loads the asset at the given path.</summary>
        /// <param name="path">Asset path.</param>
        /// <param name="type">Expected type.</param>
        /// <returns>The asset found, or null.</returns>
        public UnityEngine.Object Load(string path, Type type)
            => string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath(path, type);

        /// <summary>The path of the asset.</summary>
        /// <param name="asset">Target asset.</param>
        /// <returns>The path.</returns>
        public string PathOf(UnityEngine.Object asset)
            => asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset);

        /// <summary>Finds the asset paths that match the type filter.</summary>
        /// <param name="typeFilter">Search filter.</param>
        /// <param name="folder">Folder to search within. Null searches the whole project.</param>
        /// <returns>The paths found.</returns>
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

        /// <summary>Marks the asset as changed.</summary>
        /// <param name="asset">Target asset.</param>
        public void MarkDirty(UnityEngine.Object asset)
        {
            if (asset != null) EditorUtility.SetDirty(asset);
        }

        /// <summary>Writes the values out immediately when the asset was just created.</summary>
        /// <param name="asset">Target asset.</param>
        /// <param name="created">Whether the asset was newly created.</param>
        public void FlushIfCreated(UnityEngine.Object asset, bool created)
        {
            if (!created || asset == null) return;

            // 되임포트를 미루는 중이라면 끼어들 재임포트가 없습니다. 그때의 저장은 막으려던 사고를
            // 막지 못하면서 행마다 에셋 파이프라인을 한 번씩 돌리기만 합니다. 범위가 닫힐 때
            // 한꺼번에 내려가고, 그 뒤 SaveAll이 다시 훑습니다.
            if (Batching) return;

            AssetDatabase.SaveAssetIfDirty(asset);
        }

        /// <summary>Deletes the asset.</summary>
        /// <param name="path">Path to delete.</param>
        public void Delete(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            AssetDatabase.DeleteAsset(path);
            InvalidateCaches();
        }

        /// <summary>Saves every dirty asset.</summary>
        public void SaveAll()
        {
            AssetDatabase.SaveAssets();
            InvalidateCaches();
        }

        /// <summary>Depth of the scopes currently deferring reimport. Zero means nothing is deferred.</summary>
        private int _batchDepth;

        /// <summary>
        /// Opens a scope that defers reimport. Nested scopes release only once, when the outermost one closes.
        /// </summary>
        /// <returns>A handle that closes the scope.</returns>
        public IDisposable BatchEdits() => new BatchScope(this);

        /// <summary>Whether reimport is currently deferred.</summary>
        internal bool Batching => _batchDepth > 0;

        /// <summary>A scope that counts <see cref="AssetDatabase.StartAssetEditing"/> calls to keep them paired.</summary>
        private sealed class BatchScope : IDisposable
        {
            private readonly UnityAssetGateway _owner;
            private bool _closed;

            /// <summary>Opens the scope, and stops the asset database when this is the outermost one.</summary>
            /// <param name="owner">The gateway that counts the scopes.</param>
            public BatchScope(UnityAssetGateway owner)
            {
                _owner = owner;
                if (_owner._batchDepth++ == 0) AssetDatabase.StartAssetEditing();
            }

            /// <summary>Closes the scope, and releases the asset database when this is the outermost one.</summary>
            public void Dispose()
            {
                if (_closed) return;
                _closed = true;

                // 짝이 어긋나면 에디터가 임포트를 멈춘 채 잠긴 것처럼 보입니다. 예외 경로에서도
                // 반드시 내려가도록 finally 안에서 풉니다.
                try
                {
                    if (_owner._batchDepth == 1) AssetDatabase.StopAssetEditing();
                }
                finally
                {
                    _owner._batchDepth--;
                    if (_owner._batchDepth == 0) _owner.InvalidateCaches();
                }
            }
        }

        // ====================================================================================================
        // 참조 조사
        // ====================================================================================================

        /// <summary>
        /// The reason the reference scan cannot be trusted. <b>In this implementation it is always null.</b>
        /// <para>
        /// It used to <b>read scenes and prefabs as text</b> and look for GUID strings, so whenever the
        /// project's Asset Serialization was not <c>Force Text</c>, every question came back "not referenced".
        /// It now asks the <b>dependency graph</b> the AssetDatabase builds at import time.
        /// That graph does not care whether a file was saved as text or as binary.
        /// </para>
        /// <para>
        /// <b>One untrustworthy spot still remains.</b> The dependency graph is built at import time, that is,
        /// from <b>what is saved on disk</b>. A wiring you just made by hand in an open scene or prefab stage
        /// and have not saved yet is not in that graph. Running cleanup in that state reports "not referenced"
        /// and deletes the output asset you just wired up — the accident this tool works hardest to avoid.
        /// So <b>nothing is deleted while unsaved edits exist.</b>
        /// </para>
        /// </summary>
        public string ReferenceScanBlocked
        {
            get
            {
                if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                {
                    return "References cannot be scanned while a prefab is being edited. "
                         + "Deleting now could miss an output asset that prefab uses, so nothing was deleted.\n"
                         + "  Finish editing the prefab, save it, and bake again.";
                }

                string dirty = FirstDirtyScene();
                if (dirty != null)
                {
                    return $"References cannot be scanned because a scene is unsaved: {dirty}\n"
                         + "  A wiring you just made in a scene is invisible to the scan until the scene is saved. "
                         + "Deleting now would break that wiring, so nothing was deleted.\n"
                         + "  Save the scene and bake again, and cleanup continues.";
                }
                return null;
            }
        }

        /// <summary>The name of the first unsaved scene among the open ones. Null when there is none.</summary>
        /// <returns>The scene name, or null.</returns>
        private static string FirstDirtyScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                // 한 번도 저장한 적 없는 빈 씬(경로 없음)은 산출물을 붙잡고 있을 수 없습니다.
                // 그것까지 막으면 새 프로젝트에서 정리가 영영 돌지 않습니다.
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path)) return scene.path;
            }
            return null;
        }

        /// <summary>
        /// Picks out the candidates that are <b>still used somewhere else</b>.
        /// <para>
        /// Asks the AssetDatabase what the project's scenes, prefabs, and assets use, then looks for the
        /// candidates in that answer. <b>References between candidates do not count</b> — if things that are
        /// about to disappear together hold each other up, nothing ever gets cleaned up.
        /// </para>
        /// </summary>
        /// <param name="candidates">Asset paths to scan.</param>
        /// <returns>The paths that were found to be referenced.</returns>
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
        /// Adds the references the project settings hold directly.
        /// The preloaded-assets list lives in the settings rather than in an asset, so scanning assets alone never sees it.
        /// </summary>
        /// <param name="candidates">The candidates being scanned.</param>
        /// <param name="referenced">The paths found to be referenced. Additions go here.</param>
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
        /// What each asset <b>uses</b>, collected in one place. Built here on first use.
        /// <para>
        /// Asking the whole project again for every single table that gets cleaned up was this tool's
        /// largest cost. The window's rescan repeats that once per table. Collect it once, and
        /// <b>throw it away when an asset changes.</b>
        /// </para>
        /// </summary>
        /// <returns>Holder path → the paths that asset uses.</returns>
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

        /// <summary>
        /// Checks whether an asset can hold references.
        /// <para>
        /// <b>It does not count what can hold them; it excludes only what cannot.</b> It used to count just
        /// three kinds — scenes, prefabs, and <c>.asset</c> — and whenever an extension missing from that list
        /// held an output asset, the scan reported "not referenced" and the asset <b>was deleted.</b> Timeline
        /// (<c>.playable</c>), presets (<c>.preset</c>), and animators are exactly such places.
        /// Growing the list means the same accident returns every time Unity adds one more extension.
        /// </para>
        /// <para>
        /// Erring on the side of excluding costs a little more time; erring on the side of counting loses data.
        /// So it excludes only <b>what can be trusted not to hold references</b> (images, audio, video, models,
        /// fonts, scripts, plain text, shaders). Most of a project's bulk is here, so most of the cost goes with it.
        /// </para>
        /// </summary>
        /// <param name="path">Path to check.</param>
        /// <returns>True if it can hold references.</returns>
        private static bool CanHoldReferences(string path)
        {
            if (CannotHoldReferences(path)) return false;

            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return true;

            // 패키지도 봐야 합니다. 규모 있는 프로젝트는 자기 코드를 임베디드 패키지로 갈라 두고,
            // 그 안의 프리팹이 소비 프로젝트의 산출물을 참조합니다. 레지스트리에서 받은 패키지는
            // 읽기 전용이라 그럴 수 없으므로 그것만 뺍니다.
            return path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) && IsMutablePackage(path);
        }

        /// <summary>
        /// Checks whether a file can never hold an object reference under any circumstances.
        /// <b>When you add an extension here, confirm that it truly cannot hold one.</b> Add a wrong one and
        /// the output assets that file was holding get deleted silently.
        /// </summary>
        /// <param name="path">Path to check.</param>
        /// <returns>True if it cannot hold references.</returns>
        private static bool CannotHoldReferences(string path)
        {
            string extension = Path.GetExtension(path);
            if (string.IsNullOrEmpty(extension)) return true;   // 폴더입니다.

            return Array.IndexOf(ReferencelessExtensions, extension.ToLowerInvariant()) >= 0;
        }

        /// <summary>
        /// The extensions that cannot hold object references. Most of a project's files fall in here.
        /// <para>
        /// <b>Left open so tests can see it.</b> This list is the heart of the deletion safeguard, and if a single
        /// extension that can hold references slips in, the output assets that file was holding get deleted
        /// silently. And that accident <b>is caught by no test at all</b> — with no way to read the list, there is
        /// no way to pin it down.
        /// </para>
        /// </summary>
        internal static readonly string[] ReferencelessExtensions =
        {
            // 코드·정의
            ".cs", ".js", ".dll", ".asmdef", ".asmref", ".rsp", ".xaml",
            // 그림
            ".png", ".jpg", ".jpeg", ".tga", ".psd", ".psb", ".tif", ".tiff", ".exr", ".hdr",
            ".gif", ".bmp", ".svg", ".webp", ".ico", ".dds", ".pict",
            // 소리·영상
            ".wav", ".mp3", ".ogg", ".aiff", ".aif", ".flac", ".mod", ".it", ".s3m", ".xm",
            ".mp4", ".mov", ".webm", ".avi", ".m4v",
            // 모델·글꼴
            ".fbx", ".obj", ".blend", ".dae", ".3ds", ".max", ".ma", ".mb", ".c4d",
            ".ttf", ".otf", ".fon", ".dfont",
            // 순수 텍스트
            ".txt", ".json", ".xml", ".csv", ".tsv", ".tab", ".md", ".yaml", ".yml", ".html", ".htm",
            // 셰이더
            ".shader", ".cginc", ".hlsl", ".glslinc", ".compute", ".raytrace",
            ".shadergraph", ".shadersubgraph",
        };

        /// <summary>Package name → whether that package is editable inside this project.</summary>
        private static Dictionary<string, bool> _mutablePackages;

        /// <summary>
        /// Checks whether the package this path belongs to is edited alongside the project. (embedded or local)
        /// Asks once per package and holds the answer — asking per path turns into thousands of calls in a single scan.
        /// </summary>
        /// <param name="path">Asset path inside a package.</param>
        /// <returns>True if the package is editable.</returns>
        private static bool IsMutablePackage(string path)
        {
            int nameStart = "Packages/".Length;
            int nameEnd = path.IndexOf('/', nameStart);
            if (nameEnd < 0) return false;

            string name = path.Substring(nameStart, nameEnd - nameStart);

            if (_mutablePackages == null) _mutablePackages = new Dictionary<string, bool>(StringComparer.Ordinal);
            if (_mutablePackages.TryGetValue(name, out bool mutable)) return mutable;

            UnityEditor.PackageManager.PackageInfo info =
                UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);

            mutable = info != null
                   && (info.source == UnityEditor.PackageManager.PackageSource.Embedded
                    || info.source == UnityEditor.PackageManager.PackageSource.Local);

            _mutablePackages[name] = mutable;
            return mutable;
        }

        // ====================================================================================================
        // 들고 있는 것
        // ====================================================================================================

        /// <summary>What each asset uses. Null means it has not been collected yet, or was thrown away.</summary>
        private Dictionary<string, string[]> _dependencies;

        /// <summary>Table file name → path. A name that was not found is remembered as null.</summary>
        private Dictionary<string, string> _tablePaths;

        /// <summary>
        /// Throws away what is held. <b>Must be called whenever any asset changes.</b>
        /// Left stale, it either keeps an asset alive on the strength of a reference that is already gone, or
        /// misses a newly created reference and deletes the asset.
        /// </summary>
        public void InvalidateCaches()
        {
            _dependencies = null;
            _tablePaths = null;
            _mutablePackages = null;
        }
    }
}
