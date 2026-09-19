using System;
using UnityEditor;

namespace CsvPipeline
{
    /// <summary>
    /// Entry point that runs a <see cref="CsvImportDefinition"/> from the static callbacks of
    /// <c>AssetPostprocessor</c>.
    /// </summary>
    public static class CsvImport
    {
        /// <summary>Nesting depth of the <see cref="Suppress"/> scopes that stopped automatic imports.</summary>
        private static int _suppressDepth;

        /// <summary>
        /// Clears any leftover suppression depth every time the editor returns to edit mode.
        /// In projects with Domain Reload turned off, static values survive, and if this depth never gets
        /// back to zero <b>automatic imports stay stopped forever</b> with no way for a person to see why.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void InstallSuppressionReset()
        {
            EditorApplication.playModeStateChanged -= ReleaseSuppressionOnEditMode;
            EditorApplication.playModeStateChanged += ReleaseSuppressionOnEditMode;
        }

        /// <summary>Clears the leftover suppression depth once the editor is back in edit mode.</summary>
        /// <param name="change">The play mode transition.</param>
        private static void ReleaseSuppressionOnEditMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode) _suppressDepth = 0;
        }

        /// <summary>Whether automatic imports are stopped right now.</summary>
        public static bool IsSuppressed => _suppressDepth > 0;

        /// <summary>
        /// Stops tables from <b>baking automatically</b> when they are imported. Wrap it in <c>using</c>.
        /// When several tables are written at once, it avoids the waste of rebaking after every write.
        /// <see cref="CsvImportDefinition.Run"/>, which takes a path directly, is an explicit call and is
        /// not affected.
        /// </summary>
        /// <returns>An object that restores automatic imports when the scope ends.</returns>
        public static IDisposable Suppress() => new SuppressionScope();

        /// <summary>
        /// Runs an importer definition. Call it as a single line from <c>OnPostprocessAllAssets</c>.
        /// </summary>
        /// <typeparam name="TDefinition">Type of the importer definition to run.</typeparam>
        /// <param name="imported">Paths of the imported assets.</param>
        /// <param name="deleted">Paths of the deleted assets.</param>
        /// <param name="moved">New paths of the moved assets.</param>
        public static void Run<TDefinition>(string[] imported, string[] deleted, string[] moved)
            where TDefinition : CsvImportDefinition, new()
        {
            // 정의는 상태를 들고 있지 않으므로 매번 새로 만듭니다. 임포트 배치당 한 번뿐이라 비용이 없고,
            // 캐싱하면 도메인 리로드를 건너뛴 세션에서 낡은 상태가 남습니다.
            new TDefinition().Execute(imported, deleted, moved);
        }

        /// <summary>Scope that stops automatic imports and restores them afterwards.</summary>
        private sealed class SuppressionScope : IDisposable
        {
            private bool _released;

            /// <summary>Enters the scope.</summary>
            public SuppressionScope() => _suppressDepth++;

            /// <summary>Leaves the scope. Imports are restored even when an exception unwinds through it.</summary>
            public void Dispose()
            {
                if (_released) return;

                _released = true;
                if (_suppressDepth > 0) _suppressDepth--;
            }
        }
    }
}
