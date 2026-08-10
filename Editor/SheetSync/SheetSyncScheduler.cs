using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// 자동 받기의 <b>시점만</b> 정합니다. 무엇을 받을지와 어떻게 받을지는 <see cref="GoogleSheetSync"/>가 맡습니다.
    /// 연동 설정이 하나도 없으면 콜백 자체를 걸지 않습니다.
    /// 이 기능을 쓰지 않는 프로젝트에까지 매 프레임 콜백을 남겨 둘 이유가 없습니다.
    /// </summary>
    public static class SheetSyncScheduler
    {
        /// <summary>대상이 없을 때 다음 확인까지 미룰 초입니다.</summary>
        private const float IdleInterval = 300f;

        /// <summary>설정이 요구하더라도 이보다 자주는 확인하지 않습니다.</summary>
        private const float MinInterval = 10f;

        /// <summary>지금 자동 받기 루프가 걸려 있는지 여부입니다.</summary>
        private static bool _hooked;

        /// <summary>다음 확인 예정 시각입니다. (에디터 기동 후 경과 초)</summary>
        private static double _nextTime;

        /// <summary>에디터 기동 시 자동 받기 루프를 겁니다.</summary>
        [InitializeOnLoadMethod]
        private static void Install() => Refresh();

        /// <summary>연동 설정의 존재 여부에 맞춰 자동 받기 루프를 걸거나 뗍니다.</summary>
        public static void Refresh()
        {
            bool wanted = CsvAssets.Current.FindPaths($"t:{nameof(GoogleSheetSyncSettings)}").Count > 0;
            if (wanted == _hooked) return;

            if (wanted) EditorApplication.update += Tick;
            else EditorApplication.update -= Tick;

            _hooked = wanted;
        }

        /// <summary>간격이 찼는지 보고, 찼으면 받을 대상을 골라 넘깁니다.</summary>
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

        /// <summary>자동 받기가 켜진 설정들을 고르고, 그중 가장 짧은 간격을 함께 돌려줍니다.</summary>
        /// <param name="all">전체 설정 목록입니다.</param>
        /// <param name="interval">고른 것들 중 가장 짧은 간격을 받습니다.</param>
        /// <returns>이번에 받을 설정들입니다.</returns>
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
