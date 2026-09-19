using System.IO;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// Opens the documentation shipped with the package.
    /// <para>
    /// This used to open the README of the public repository in a browser. Since the release repository went
    /// private, that address is <b>a 404 even for people who hold a license</b>. A help button that opens an
    /// error page is worse than no documentation — the buyer reads it as documentation that has disappeared.
    /// So it opens the copy that sits inside the package. No internet and no access rights are needed.
    /// </para>
    /// </summary>
    public static class CsvDocs
    {
        /// <summary>Documentation paths inside the package. The first one that exists is opened.</summary>
        private static readonly string[] Candidates =
        {
            "Documentation~/manual-en.md",
            "README.md",
        };

        /// <summary>
        /// Opens the documentation. When nothing is found, it logs where it looked. (Doing nothing silently
        /// makes the button look broken)
        /// </summary>
        public static void Open()
        {
            string root = PackageRoot();

            foreach (string relative in Candidates)
            {
                string full = string.IsNullOrEmpty(root) ? null : Path.Combine(root, relative);
                if (full == null || !File.Exists(full)) continue;

                // file: 스킴으로 넘겨야 브라우저·편집기 연결이 걸립니다. 경로 그대로 넘기면
                // 윈도우의 역슬래시에서 열리지 않습니다.
                Application.OpenURL("file:///" + full.Replace('\\', '/'));
                return;
            }

            Debug.LogWarning(
                $"[CsvPipeline] The documentation was not found. Package location: {(string.IsNullOrEmpty(root) ? "(unknown)" : root)}\n"
                + $"Looked in: {string.Join(", ", Candidates)}");
        }

        /// <summary>
        /// The absolute path of the folder where this package actually sits.
        /// <b>The path is never written as a constant</b> — consumers put this package under <c>Packages/</c>,
        /// link it by a local path, or drop the whole thing inside <c>Assets/</c>.
        /// </summary>
        /// <returns>The absolute path, or null when it could not be determined.</returns>
        private static string PackageRoot()
        {
            // UnityEditor 에도 같은 이름의 구형 타입이 있어 전체 이름으로 적습니다.
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(CsvDocs).Assembly);
            if (package != null && !string.IsNullOrEmpty(package.resolvedPath)) return package.resolvedPath;

            // Assets/ 안에 복사해 넣은 배포본에서는 패키지로 잡히지 않습니다. 이 파일의 위치에서 거슬러 올라갑니다.
            foreach (string guid in AssetDatabase.FindAssets($"{nameof(CsvDocs)} t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith($"/{nameof(CsvDocs)}.cs", System.StringComparison.Ordinal)) continue;

                // <루트>/Editor/Core/CsvDocs.cs 에서 <루트>까지 셋을 올라갑니다.
                string root = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(path)));
                return string.IsNullOrEmpty(root) ? null : Path.GetFullPath(root);
            }

            return null;
        }
    }
}
