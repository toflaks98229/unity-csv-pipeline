using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// CSV → ScriptableObject 임포터들이 공통으로 쓰는 에셋 폴더 라이프사이클 유틸입니다.
    /// 실제 저장소 접근은 <see cref="CsvAssets.Current"/> 게이트웨이에 맡기므로,
    /// 검사에서는 Unity 프로젝트 없이 같은 흐름을 돌릴 수 있습니다.
    /// </summary>
    public static class CsvAssetPipeline
    {
        /// <summary>지금 쓰이는 저장소 게이트웨이입니다.</summary>
        private static ICsvAssetGateway Assets => CsvAssets.Current;

        /// <summary>
        /// 참조 조사를 믿을 수 없으면 그 이유입니다. 믿을 수 있으면 null입니다.
        /// 이 값이 있으면 정리는 <b>아무것도 지우지 않고</b> 전부 보존으로 돌립니다.
        /// </summary>
        public static string ReferenceScanBlocked => Assets.ReferenceScanBlocked;

        /// <summary>
        /// 지정한 파일명(예: "LootTables.csv")을 가진 표 파일의 에셋 경로를 찾습니다.
        /// </summary>
        /// <param name="fileName">찾을 파일 이름입니다. 확장자를 포함합니다.</param>
        /// <returns>찾은 에셋 경로이거나, 없으면 null입니다.</returns>
        public static string FindCsvPath(string fileName) => Assets.FindTablePath(fileName);

        /// <summary>경로가 없으면 부모부터 순차적으로 폴더를 생성해 보장합니다.</summary>
        /// <param name="folderPath">보장할 폴더 경로입니다. (예: Assets/Data/Items)</param>
        public static void EnsureFolder(string folderPath) => Assets.EnsureFolder(folderPath);

        /// <summary>
        /// CSV 원본이 사라졌음을 알립니다. 산출물은 건드리지 않습니다.
        /// </summary>
        /// <param name="folder">해당 CSV가 굽던 산출물 폴더입니다.</param>
        /// <param name="csvFile">사라진 CSV 파일 이름입니다.</param>
        /// <param name="logTag">로그 접두 태그입니다.</param>
        public static void WarnSourceRemoved(string folder, string csvFile, string logTag)
        {
            if (!Assets.FolderExists(folder)) return;

            // 원본이 사라졌다고 산출물 폴더를 지우지 않습니다. CSV를 잠깐 옮기거나 실수로 지우기만 해도
            // 확인 없이 폴더가 사라지는데, 그 안에는 CSV로 재생성할 수 없는 수작업 데이터(참조·아이콘·
            // 프리팹 배선)가 들어 있습니다. 게다가 에셋이 삭제되면 GUID가 바뀌어, git으로 파일을 되돌려도
            // 이를 참조하던 프리팹/씬의 링크는 돌아오지 않습니다.
            // 산출물 정리는 CSV가 실제로 존재할 때 유효 목록과 대조하는 ReconcileFolder* 경로에서만 합니다.
            Debug.LogWarning(
                $"{logTag} 원본 CSV '{csvFile}'가 사라졌지만 산출물 폴더는 보존합니다: {folder}\n" +
                "의도한 삭제라면 폴더를 직접 지우십시오. (수작업 배선이 남아 있을 수 있어 자동 삭제하지 않습니다)");
        }

        /// <summary>지정 경로의 SO를 로드하거나, 없으면 새로 생성해 반환합니다.</summary>
        /// <typeparam name="T">대상 ScriptableObject 타입입니다.</typeparam>
        /// <param name="path">에셋 경로입니다.</param>
        /// <returns>로드하거나 새로 만든 에셋입니다.</returns>
        public static T CreateOrLoad<T>(string path) where T : ScriptableObject
            => CreateOrLoad<T>(path, out _);

        /// <summary>지정 경로의 SO를 로드하거나, 없으면 새로 생성해 반환합니다.</summary>
        /// <typeparam name="T">대상 ScriptableObject 타입입니다.</typeparam>
        /// <param name="path">에셋 경로입니다.</param>
        /// <param name="created">새로 만들었으면 true를 받습니다. (생성/갱신 집계용)</param>
        /// <returns>로드하거나 새로 만든 에셋입니다.</returns>
        public static T CreateOrLoad<T>(string path, out bool created) where T : ScriptableObject
            => (T)Assets.CreateOrLoad(typeof(T), path, out created);

        /// <summary>
        /// 타입을 런타임에 정하는 경우의 <see cref="CreateOrLoad{T}(string, out bool)"/>입니다.
        /// </summary>
        /// <param name="type">만들 ScriptableObject 타입입니다.</param>
        /// <param name="path">에셋 경로입니다.</param>
        /// <param name="created">새로 만들었으면 true를 받습니다.</param>
        /// <returns>로드하거나 새로 만든 에셋입니다.</returns>
        public static ScriptableObject CreateOrLoad(Type type, string path, out bool created)
            => Assets.CreateOrLoad(type, path, out created);

        /// <summary>
        /// 방금 만든 에셋이면 값을 곧바로 디스크에 씁니다.
        /// 지워진 경로에 다시 만들면 재임포트가 끼어들어 <b>메모리에만 있던 수정이 버려집니다.</b>
        /// 저장을 배치 끝까지 미루지 않는 것으로 그 창을 없앱니다. (갱신 경로는 그대로 미룹니다)
        /// </summary>
        /// <param name="asset">방금 굽고 더럽힌 에셋입니다.</param>
        /// <param name="created">이번에 새로 만든 것인지 여부입니다.</param>
        public static void FlushIfCreated(UnityEngine.Object asset, bool created)
            => Assets.FlushIfCreated(asset, created);

        /// <summary>
        /// 폴더에서 이번 임포트로 확정된 <paramref name="validNames"/>(파일명, 확장자 제외)에 없는 에셋을 삭제합니다.
        /// </summary>
        /// <param name="folder">정리할 산출물 폴더입니다.</param>
        /// <param name="typeFilter">에셋 검색 필터입니다. (예: "t:ItemData")</param>
        /// <param name="validNames">이번 임포트로 확정된 에셋 이름들입니다.</param>
        /// <param name="logTag">로그 접두 태그입니다.</param>
        /// <param name="report">결과를 기록할 리포트입니다. null이면 Console에 바로 남깁니다.</param>
        public static void ReconcileFolderByName(string folder, string typeFilter, ICollection<string> validNames,
                                                 string logTag, CsvImportReport report = null)
            => DeleteUnreferenced(FindObsolete(folder, typeFilter, validNames, byPath: false), logTag, report);

        /// <summary>
        /// 폴더에서 이번 임포트로 확정된 <paramref name="validPaths"/>(에셋 경로)에 없는 에셋을 삭제합니다.
        /// (인플레이스 갱신 에셋을 오삭제하지 않도록 경로 기반으로 대조)
        /// </summary>
        /// <param name="folder">정리할 산출물 폴더입니다.</param>
        /// <param name="typeFilter">에셋 검색 필터입니다. (예: "t:CardData")</param>
        /// <param name="validPaths">이번 임포트로 확정된 에셋 경로들입니다.</param>
        /// <param name="logTag">로그 접두 태그입니다.</param>
        /// <param name="report">결과를 기록할 리포트입니다. null이면 Console에 바로 남깁니다.</param>
        public static void ReconcileFolderByPath(string folder, string typeFilter, ICollection<string> validPaths,
                                                 string logTag, CsvImportReport report = null)
            => DeleteUnreferenced(FindObsolete(folder, typeFilter, validPaths, byPath: true), logTag, report);

        /// <summary>
        /// 표에서 사라진 산출물을 <b>지우지 않고</b> 분류만 합니다. 미리보기가 씁니다.
        /// </summary>
        /// <param name="folder">정리 대상 폴더입니다.</param>
        /// <param name="typeFilter">에셋 검색 필터입니다.</param>
        /// <param name="valid">이번 임포트로 확정된 이름 또는 경로들입니다.</param>
        /// <param name="byPath">true면 <paramref name="valid"/>를 경로로, false면 파일 이름으로 대조합니다.</param>
        /// <param name="deletable">지워도 되는 경로들을 받습니다.</param>
        /// <param name="preserved">참조가 남아 보존할 경로들을 받습니다.</param>
        public static void PlanReconcile(string folder, string typeFilter, ICollection<string> valid, bool byPath,
                                         out List<string> deletable, out List<string> preserved)
        {
            deletable = new List<string>();
            preserved = new List<string>();

            List<string> candidates = FindObsolete(folder, typeFilter, valid, byPath);
            if (candidates.Count == 0) return;

            // 조사할 수 없으면 전부 보존입니다. 지울 수 있는 것이 하나도 없다고 답하는 편이
            // "참조가 없다"고 잘못 답하는 것보다 낫습니다.
            if (ReferenceScanBlocked != null)
            {
                preserved.AddRange(candidates);
                return;
            }

            HashSet<string> referenced = Assets.FindReferenced(candidates);
            foreach (string path in candidates)
            {
                if (referenced.Contains(path)) preserved.Add(path);
                else deletable.Add(path);
            }
        }

        /// <summary>폴더에서 이번 임포트로 확정되지 않은 에셋들을 찾습니다.</summary>
        /// <param name="folder">검색할 폴더입니다.</param>
        /// <param name="typeFilter">에셋 검색 필터입니다.</param>
        /// <param name="valid">확정된 이름 또는 경로들입니다.</param>
        /// <param name="byPath">경로로 대조할지 여부입니다.</param>
        /// <returns>사라진 것으로 판정된 에셋 경로들입니다.</returns>
        private static List<string> FindObsolete(string folder, string typeFilter, ICollection<string> valid, bool byPath)
        {
            var candidates = new List<string>();
            if (!Assets.FolderExists(folder)) return candidates;

            foreach (string path in Assets.FindPaths(typeFilter, folder))
            {
                string key = byPath ? path : Path.GetFileNameWithoutExtension(path);
                if (!valid.Contains(key)) candidates.Add(path);
            }
            return candidates;
        }

        /// <summary>
        /// 참조를 조사할 수 없을 때, 후보를 하나도 지우지 않고 전부 보존합니다.
        /// 이유는 <b>한 번만</b> 알립니다. 후보마다 같은 말을 되풀이하면 진짜 문제가 묻힙니다.
        /// </summary>
        /// <param name="candidates">지우지 않고 남길 경로들입니다.</param>
        /// <param name="reason">조사할 수 없는 이유입니다.</param>
        /// <param name="logTag">로그 접두 태그입니다.</param>
        /// <param name="report">결과를 기록할 리포트입니다. null이면 Console에 바로 남깁니다.</param>
        private static void PreserveAll(List<string> candidates, string reason, string logTag, CsvImportReport report)
        {
            string message = $"표에서 사라진 산출물 {candidates.Count}개를 지우지 않았습니다. {reason}";

            if (report != null)
            {
                report.Warn(message);
                for (int i = 0; i < candidates.Count; i++) report.CountPreserved();
            }
            else
            {
                Debug.LogWarning($"{logTag} {message}");
            }
        }

        /// <summary>
        /// 삭제 후보 중 <b>아무도 참조하지 않는 것만</b> 지웁니다. 참조가 남은 것은 경고만 남기고 보존합니다.
        /// </summary>
        /// <param name="candidates">CSV에서 사라져 삭제 대상이 된 에셋 경로들입니다.</param>
        /// <param name="logTag">로그 접두 태그입니다.</param>
        /// <param name="report">결과를 기록할 리포트입니다. null이면 Console에 바로 남깁니다.</param>
        private static void DeleteUnreferenced(List<string> candidates, string logTag, CsvImportReport report)
        {
            if (candidates.Count == 0) return;

            string blocked = ReferenceScanBlocked;
            if (blocked != null)
            {
                PreserveAll(candidates, blocked, logTag, report);
                return;
            }

            HashSet<string> referenced = Assets.FindReferenced(candidates);

            foreach (string path in candidates)
            {
                if (referenced.Contains(path))
                {
                    string keep = $"표에서 사라졌지만 아직 참조 중이라 보존합니다: {path}. "
                                + "Id 오타일 수 있습니다. 정말 삭제하려면 참조를 먼저 끊고 직접 지우십시오.";

                    if (report != null)
                    {
                        report.CountPreserved();
                        report.Warn(keep, 0, null, Assets.Load(path, typeof(UnityEngine.Object)));
                    }
                    else
                    {
                        Debug.LogWarning($"{logTag} {keep}");
                    }
                    continue;
                }

                Assets.Delete(path);

                if (report != null) report.CountDeleted();
                else Debug.Log($"{logTag} Deleted obsolete asset: {path}");
            }
        }
    }
}
