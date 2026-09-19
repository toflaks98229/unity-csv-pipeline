using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// Decides <b>only when</b> an automatic pull happens. What to pull and how to pull it belongs to <see cref="GoogleSheetSync"/>.
    /// When there is not a single sync settings asset, it never installs the callback at all.
    /// There is no reason to leave a per-frame callback in a project that does not use this feature.
    /// </summary>
    public static class SheetSyncScheduler
    {
        /// <summary>Seconds to wait before the next check when there is nothing to pull.</summary>
        private const float IdleInterval = 300f;

        /// <summary>It never checks more often than this, no matter what the settings ask for.</summary>
        private const float MinInterval = 10f;

        /// <summary>Whether the automatic pull loop is installed right now.</summary>
        private static bool _hooked;

        /// <summary>Time of the next scheduled check. (Seconds since the editor started.)</summary>
        private static double _nextTime;

        /// <summary>Installs the automatic pull loop when the editor starts.</summary>
        [InitializeOnLoadMethod]
        private static void Install() => Refresh();

        /// <summary>Installs or removes the automatic pull loop according to whether any sync settings exist.</summary>
        public static void Refresh()
        {
            bool wanted = CsvAssets.Current.FindPaths($"t:{nameof(GoogleSheetSyncSettings)}").Count > 0;
            if (wanted == _hooked) return;

            if (wanted) EditorApplication.update += Tick;
            else EditorApplication.update -= Tick;

            _hooked = wanted;
        }

        /// <summary>Checks whether the interval elapsed, and if it did, picks the targets and hands them over.</summary>
        private static void Tick()
        {
            if (GoogleSheetSync.IsRunning || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup < _nextTime) return;

            List<GoogleSheetSyncSettings> all = GoogleSheetSync.FindAll();
            if (all.Count == 0)
            {
                // 설정이 전부 지워졌으면 루프를 뗍니다. 다시 생기면 임포트 통지가 걸어 줍니다.
                Refresh();
                return;
            }

            List<GoogleSheetSyncSettings> due = SelectDue(all, out float interval);

            // 대상이 없으면 다음 확인을 멀찍이 미뤄, 매 프레임 에셋을 뒤지지 않게 합니다.
            _nextTime = EditorApplication.timeSinceStartup + (due.Count > 0 ? interval : IdleInterval);
            if (due.Count == 0) return;

            GoogleSheetSync.PullAutomatically(due);
        }

        /// <summary>Picks the settings that have automatic pull turned on, and returns the shortest of their intervals with them.</summary>
        /// <param name="all">All settings.</param>
        /// <param name="interval">Receives the shortest interval among the picked settings.</param>
        /// <returns>Settings to pull this time.</returns>
        private static List<GoogleSheetSyncSettings> SelectDue(List<GoogleSheetSyncSettings> all, out float interval)
        {
            var due = new List<GoogleSheetSyncSettings>();
            interval = IdleInterval;

            foreach (GoogleSheetSyncSettings settings in all)
            {
                if (!settings.autoPull || !settings.enabled || !settings.IsConfigured) continue;

                due.Add(settings);
                interval = Mathf.Min(interval, Mathf.Max(MinInterval, settings.autoPullIntervalSeconds));
            }

            return due;
        }
    }
}
