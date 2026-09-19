using System;
using System.Collections.Generic;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// The <b>only path</b> through which the pipeline touches the asset store.
    /// This boundary lets the baking, planning, and export logic be tested without a live Unity project.
    /// </summary>
    public interface ICsvAssetGateway
    {
        /// <summary>Finds the path of the table file with the given name.</summary>
        /// <param name="fileName">File name to look for. Includes the extension.</param>
        /// <returns>The path found, or null when there is none.</returns>
        string FindTablePath(string fileName);

        /// <summary>Reads the text of a table file.</summary>
        /// <param name="path">Path to read.</param>
        /// <returns>The text, or null when it could not be read.</returns>
        string ReadText(string path);

        /// <summary>
        /// Reads the text of a table file, and when it cannot be read, hands back <b>why</b> as well.
        /// <para>
        /// The reason is needed because of encoding. Reading fails when the file is not UTF-8, and
        /// returning only null leaves the caller unable to tell "the file is missing" from "the characters
        /// cannot be read", so it gives the person <b>advice they cannot act on</b>.
        /// </para>
        /// </summary>
        /// <param name="path">Path to read.</param>
        /// <param name="problem">Receives the reason it could not be read. Null on success, or when the file is missing.</param>
        /// <returns>The text, or null when it could not be read.</returns>
        string ReadText(string path, out string problem);

        /// <summary>Whether the folder exists.</summary>
        /// <param name="folder">Folder path to check.</param>
        /// <returns>True when it exists.</returns>
        bool FolderExists(string folder);

        /// <summary>Creates the folder when it is missing, parents first, in order.</summary>
        /// <param name="folder">Folder path to ensure.</param>
        void EnsureFolder(string folder);

        /// <summary>Loads the asset at the given path, or creates it when there is none.</summary>
        /// <param name="type">ScriptableObject type to create.</param>
        /// <param name="path">Asset path.</param>
        /// <param name="created">Receives true when it was newly created.</param>
        /// <returns>The asset loaded or created.</returns>
        ScriptableObject CreateOrLoad(Type type, string path, out bool created);

        /// <summary>Loads the asset at the given path.</summary>
        /// <param name="path">Asset path.</param>
        /// <param name="type">Expected type.</param>
        /// <returns>The asset found, or null.</returns>
        UnityEngine.Object Load(string path, Type type);

        /// <summary>Path of the asset.</summary>
        /// <param name="asset">Target asset.</param>
        /// <returns>The path, or an empty string when it is not saved.</returns>
        string PathOf(UnityEngine.Object asset);

        /// <summary>Finds the asset paths matching a type filter.</summary>
        /// <param name="typeFilter">Search filter. (for example, "t:ItemData")</param>
        /// <param name="folder">Folder the search is limited to. Null searches everything.</param>
        /// <returns>The paths found. Their order is not guaranteed.</returns>
        IReadOnlyList<string> FindPaths(string typeFilter, string folder = null);

        /// <summary>Marks the asset as changed.</summary>
        /// <param name="asset">Target asset.</param>
        void MarkDirty(UnityEngine.Object asset);

        /// <summary>
        /// Writes the values out at once when the asset was just created.
        /// Recreating one at a deleted path lets a reimport cut in and throw away edits that lived only in memory.
        /// </summary>
        /// <param name="asset">Asset just baked and dirtied.</param>
        /// <param name="created">Whether it was newly created this time.</param>
        void FlushIfCreated(UnityEngine.Object asset, bool created);

        /// <summary>Deletes an asset.</summary>
        /// <param name="path">Path to delete.</param>
        void Delete(string path);

        /// <summary>Saves every dirtied asset.</summary>
        void SaveAll();

        /// <summary>
        /// <b>Defers the store's reimport</b> while several assets are created or changed. Dropping the return value releases it.
        /// <para>
        /// Without this the asset pipeline runs once per row. Baking a 3,000-row table spends on the order of
        /// 100ms filling in values, while that surrounding ceremony spends two minutes — fixing a single cell
        /// and saving costs the same. Baking that breaks the editing flow removes the reason to author in
        /// tables at all, so this stays a contract the skeleton keeps.
        /// </para>
        /// <para>
        /// <b>It nests.</b> Rebuilding all tables opens a scope on the outside and then opens one again per
        /// table, so if the store were released first when the inner scope closes, it would go back to running
        /// once per row from there on. An implementation that holds nothing can return a handle that does nothing.
        /// </para>
        /// </summary>
        /// <returns>Handle that closes the scope.</returns>
        IDisposable BatchEdits();

        /// <summary>
        /// Picks out the candidates that <b>something else still references</b>.
        /// Deleting an asset that is still referenced loses its GUID, and not even git brings the wiring back.
        /// <b>Do not trust this result when <see cref="ReferenceScanBlocked"/> is not null.</b>
        /// </summary>
        /// <param name="candidates">Asset paths to scan.</param>
        /// <returns>The paths where a reference was found.</returns>
        HashSet<string> FindReferenced(IReadOnlyList<string> candidates);

        /// <summary>
        /// Why the reference scan cannot be trusted. Null when it can.
        /// <para>
        /// There are situations where the scan <b>confidently gives a wrong answer</b>. Taking "no references"
        /// at face value then deletes an asset a scene is using without warning, and the GUID is gone, so git
        /// cannot undo it either. So <b>the fact that it cannot scan</b> comes back as a value, and nothing is
        /// deleted while it holds.
        /// </para>
        /// </summary>
        string ReferenceScanBlocked { get; }

        /// <summary>
        /// Drops whatever is held. <b>Called whenever any asset changes.</b>
        /// <para>
        /// A reference scan asks about the whole project, so holding on to the result is worth it. That result
        /// can then go stale, and a stale answer can be wrong in the direction of <b>missing a live reference
        /// and deleting</b>. So the contract carries a way to drop it. An implementation that holds nothing can do nothing.
        /// </para>
        /// </summary>
        void InvalidateCaches();
    }
}
