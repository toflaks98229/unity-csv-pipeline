using System;
using System.Collections.Generic;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// 파이프라인이 에셋 저장소에 닿는 <b>유일한 통로</b>입니다.
    /// 이 경계 덕분에 굽기·계획·내보내기 로직을 살아 있는 Unity 프로젝트 없이 검사할 수 있습니다.
    /// </summary>
    public interface ICsvAssetGateway
    {
        /// <summary>지정한 이름의 표 파일 경로를 찾습니다.</summary>
        /// <param name="fileName">찾을 파일 이름입니다. 확장자를 포함합니다.</param>
        /// <returns>찾은 경로이거나, 없으면 null입니다.</returns>
        string FindTablePath(string fileName);

        /// <summary>표 파일의 원문을 읽습니다.</summary>
        /// <param name="path">읽을 경로입니다.</param>
        /// <returns>원문이거나, 읽지 못했으면 null입니다.</returns>
        string ReadText(string path);

        /// <summary>폴더가 있는지 여부입니다.</summary>
        /// <param name="folder">확인할 폴더 경로입니다.</param>
        /// <returns>있으면 true입니다.</returns>
        bool FolderExists(string folder);

        /// <summary>없으면 부모부터 순차적으로 폴더를 만듭니다.</summary>
        /// <param name="folder">보장할 폴더 경로입니다.</param>
        void EnsureFolder(string folder);

        /// <summary>지정 경로의 에셋을 로드하거나, 없으면 만듭니다.</summary>
        /// <param name="type">만들 ScriptableObject 타입입니다.</param>
        /// <param name="path">에셋 경로입니다.</param>
        /// <param name="created">새로 만들었으면 true를 받습니다.</param>
        /// <returns>로드하거나 만든 에셋입니다.</returns>
        ScriptableObject CreateOrLoad(Type type, string path, out bool created);

        /// <summary>지정 경로의 에셋을 로드합니다.</summary>
        /// <param name="path">에셋 경로입니다.</param>
        /// <param name="type">기대하는 타입입니다.</param>
        /// <returns>찾은 에셋이거나 null입니다.</returns>
        UnityEngine.Object Load(string path, Type type);

        /// <summary>에셋의 경로입니다.</summary>
        /// <param name="asset">대상 에셋입니다.</param>
        /// <returns>경로이거나, 저장돼 있지 않으면 빈 문자열입니다.</returns>
        string PathOf(UnityEngine.Object asset);

        /// <summary>타입 필터에 맞는 에셋 경로들을 찾습니다.</summary>
        /// <param name="typeFilter">검색 필터입니다. (예: "t:ItemData")</param>
        /// <param name="folder">검색 범위 폴더입니다. null이면 전체입니다.</param>
        /// <returns>찾은 경로들입니다. 정렬은 보장하지 않습니다.</returns>
        IReadOnlyList<string> FindPaths(string typeFilter, string folder = null);

        /// <summary>에셋이 바뀌었음을 표시합니다.</summary>
        /// <param name="asset">대상 에셋입니다.</param>
        void MarkDirty(UnityEngine.Object asset);

        /// <summary>
        /// 방금 만든 에셋이면 값을 곧바로 씁니다.
        /// 지워진 경로에 다시 만들면 재임포트가 끼어들어 메모리에만 있던 수정이 버려지기 때문입니다.
        /// </summary>
        /// <param name="asset">방금 굽고 더럽힌 에셋입니다.</param>
        /// <param name="created">이번에 새로 만든 것인지 여부입니다.</param>
        void FlushIfCreated(UnityEngine.Object asset, bool created);

        /// <summary>에셋을 지웁니다.</summary>
        /// <param name="path">지울 경로입니다.</param>
        void Delete(string path);

        /// <summary>더럽혀진 에셋을 모두 저장합니다.</summary>
        void SaveAll();

        /// <summary>
        /// 후보 중 <b>다른 곳에서 참조 중인 것</b>을 가려냅니다.
        /// 참조가 남은 에셋을 지우면 GUID가 사라져 git으로도 배선이 돌아오지 않습니다.
        /// <b><see cref="ReferenceScanBlocked"/>가 null이 아니면 이 결과를 믿어서는 안 됩니다.</b>
        /// </summary>
        /// <param name="candidates">조사할 에셋 경로들입니다.</param>
        /// <returns>참조가 발견된 경로들입니다.</returns>
        HashSet<string> FindReferenced(IReadOnlyList<string> candidates);

        /// <summary>
        /// 참조 조사를 믿을 수 없으면 그 이유입니다. 믿을 수 있으면 null입니다.
        /// <para>
        /// 조사가 <b>틀린 답을 자신 있게 내놓는</b> 상황이 있습니다. 그때 "참조 없음"을 그대로 받으면
        /// 씬이 쓰고 있는 에셋을 경고 없이 지우게 되고, GUID가 사라져 git으로도 되돌릴 수 없습니다.
        /// 그래서 <b>조사할 수 없다는 사실 자체</b>를 값으로 돌려주고, 그럴 때는 아무것도 지우지 않습니다.
        /// </para>
        /// </summary>
        string ReferenceScanBlocked { get; }
    }
}
