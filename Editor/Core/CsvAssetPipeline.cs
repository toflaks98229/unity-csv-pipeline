using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// Asset folder lifecycle helpers shared by the CSV to ScriptableObject importers.
    /// Real store access goes through the <see cref="CsvAssets.Current"/> gateway, so a test
    /// can run the same flow without a Unity project.
    /// </summary>
    public static class CsvAssetPipeline
    {
        /// <summary>The store gateway in use right now.</summary>
        private static ICsvAssetGateway Assets => CsvAssets.Current;

        /// <summary>
        /// Why the reference scan cannot be trusted. Null when it can.
        /// While this has a value, cleanup <b>deletes nothing</b> and turns every candidate into a preserved one.
        /// </summary>
        public static string ReferenceScanBlocked => Assets.ReferenceScanBlocked;

        /// <summary>
        /// Finds the asset path of the table file with the given file name (for example, "LootTables.csv").
        /// </summary>
        /// <param name="fileName">File name to look for. Includes the extension.</param>
        /// <returns>The asset path found, or null when there is none.</returns>
        public static string FindCsvPath(string fileName) => Assets.FindTablePath(fileName);

        /// <summary>Ensures the path by creating the folders when they are missing, parents first, in order.</summary>
        /// <param name="folderPath">Folder path to ensure. (for example, Assets/Data/Items)</param>
        public static void EnsureFolder(string folderPath) => Assets.EnsureFolder(folderPath);

        /// <summary>
        /// Reports that the source CSV is gone. It leaves the output assets alone.
        /// </summary>
        /// <param name="folder">Output folder that CSV was baking into.</param>
        /// <param name="csvFile">Name of the CSV file that is gone.</param>
        /// <param name="logTag">Log prefix tag.</param>
        public static void WarnSourceRemoved(string folder, string csvFile, string logTag)
        {
            if (!Assets.FolderExists(folder)) return;

            // 원본이 사라졌다고 산출물 폴더를 지우지 않습니다. CSV를 잠깐 옮기거나 실수로 지우기만 해도
            // 확인 없이 폴더가 사라지는데, 그 안에는 CSV로 재생성할 수 없는 수작업 데이터(참조·아이콘·
            // 프리팹 배선)가 들어 있습니다. 게다가 에셋이 삭제되면 GUID가 바뀌어, git으로 파일을 되돌려도
            // 이를 참조하던 프리팹/씬의 링크는 돌아오지 않습니다.
            // 산출물 정리는 CSV가 실제로 존재할 때 유효 목록과 대조하는 ReconcileFolder* 경로에서만 합니다.
            Debug.LogWarning(
                $"{logTag} Source CSV '{csvFile}' is gone, but the output folder is preserved: {folder}\n" +
                "If the deletion was intended, delete the folder yourself. (It is not deleted automatically, because hand-made wiring may still be in it)");
        }

        /// <summary>Loads the ScriptableObject at the given path, or creates and returns a new one when there is none.</summary>
        /// <typeparam name="T">Target ScriptableObject type.</typeparam>
        /// <param name="path">Asset path.</param>
        /// <returns>The asset loaded or newly created.</returns>
        public static T CreateOrLoad<T>(string path) where T : ScriptableObject
            => CreateOrLoad<T>(path, out _);

        /// <summary>Loads the ScriptableObject at the given path, or creates and returns a new one when there is none.</summary>
        /// <typeparam name="T">Target ScriptableObject type.</typeparam>
        /// <param name="path">Asset path.</param>
        /// <param name="created">Receives true when it was newly created. (for the created/updated tally)</param>
        /// <returns>The asset loaded or newly created.</returns>
        public static T CreateOrLoad<T>(string path, out bool created) where T : ScriptableObject
            => (T)Assets.CreateOrLoad(typeof(T), path, out created);

        /// <summary>
        /// The <see cref="CreateOrLoad{T}(string, out bool)"/> for when the type is decided at run time.
        /// </summary>
        /// <param name="type">ScriptableObject type to create.</param>
        /// <param name="path">Asset path.</param>
        /// <param name="created">Receives true when it was newly created.</param>
        /// <returns>The asset loaded or newly created.</returns>
        public static ScriptableObject CreateOrLoad(Type type, string path, out bool created)
            => Assets.CreateOrLoad(type, path, out created);

        /// <summary>
        /// Writes the values straight to disk when the asset was just created.
        /// Recreating one at a deleted path lets a reimport cut in, and <b>edits that lived only in memory are thrown away.</b>
        /// Not deferring the save to the end of the batch closes that window. (The update path still defers)
        /// </summary>
        /// <param name="asset">Asset just baked and dirtied.</param>
        /// <param name="created">Whether it was newly created this time.</param>
        public static void FlushIfCreated(UnityEngine.Object asset, bool created)
            => Assets.FlushIfCreated(asset, created);

        /// <summary>
        /// Deletes the assets in the folder that are absent from <paramref name="validNames"/> (file names, extension excluded), the set this import settled on.
        /// </summary>
        /// <param name="folder">Output folder to clean up.</param>
        /// <param name="typeFilter">Asset search filter. (for example, "t:ItemData")</param>
        /// <param name="validNames">Asset names this import settled on.</param>
        /// <param name="logTag">Log prefix tag.</param>
        /// <param name="report">Report to record the results in. Null writes straight to the Console.</param>
        public static void ReconcileFolderByName(string folder, string typeFilter, ICollection<string> validNames,
                                                 string logTag, CsvImportReport report = null)
            => DeleteUnreferenced(FindObsolete(folder, typeFilter, validNames, byPath: false), logTag, report);

        /// <summary>
        /// Deletes the assets in the folder that are absent from <paramref name="validPaths"/> (asset paths), the set this import settled on.
        /// (Matched against by path, so assets updated in place are not deleted by mistake)
        /// </summary>
        /// <param name="folder">Output folder to clean up.</param>
        /// <param name="typeFilter">Asset search filter. (for example, "t:CardData")</param>
        /// <param name="validPaths">Asset paths this import settled on.</param>
        /// <param name="logTag">Log prefix tag.</param>
        /// <param name="report">Report to record the results in. Null writes straight to the Console.</param>
        public static void ReconcileFolderByPath(string folder, string typeFilter, ICollection<string> validPaths,
                                                 string logTag, CsvImportReport report = null)
            => DeleteUnreferenced(FindObsolete(folder, typeFilter, validPaths, byPath: true), logTag, report);

        /// <summary>
        /// Sorts the output assets that vanished from the table <b>without deleting</b> any. The preview uses this.
        /// </summary>
        /// <param name="folder">Folder to clean up.</param>
        /// <param name="typeFilter">Asset search filter.</param>
        /// <param name="valid">Names or paths this import settled on.</param>
        /// <param name="byPath">True matches <paramref name="valid"/> against paths, false against file names.</param>
        /// <param name="deletable">Receives the paths that are safe to delete.</param>
        /// <param name="preserved">Receives the paths preserved because they are still referenced.</param>
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

        /// <summary>Finds the assets in the folder that this import did not settle on.</summary>
        /// <param name="folder">Folder to search.</param>
        /// <param name="typeFilter">Asset search filter.</param>
        /// <param name="valid">Names or paths that were settled on.</param>
        /// <param name="byPath">Whether to match against paths.</param>
        /// <returns>The asset paths judged to have vanished.</returns>
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
        /// Preserves every candidate, deleting none, when the references cannot be scanned.
        /// It reports the reason <b>once</b>. Repeating the same line per candidate buries the real problem.
        /// </summary>
        /// <param name="candidates">Paths to leave in place instead of deleting.</param>
        /// <param name="reason">Reason it cannot scan.</param>
        /// <param name="logTag">Log prefix tag.</param>
        /// <param name="report">Report to record the results in. Null writes straight to the Console.</param>
        private static void PreserveAll(List<string> candidates, string reason, string logTag, CsvImportReport report)
        {
            string message = $"Did not delete {candidates.Count} output asset(s) that vanished from the table. {reason}";

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
        /// Deletes <b>only the candidates nothing references</b>. Ones that are still referenced are preserved with a warning.
        /// </summary>
        /// <param name="candidates">Asset paths that vanished from the CSV and became deletion candidates.</param>
        /// <param name="logTag">Log prefix tag.</param>
        /// <param name="report">Report to record the results in. Null writes straight to the Console.</param>
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
                    string keep = $"Gone from the table but still referenced, so it is preserved: {path}. "
                                + "This may be an Id typo. To really delete it, break the references first and delete it yourself.";

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
