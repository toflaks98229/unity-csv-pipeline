using System;

namespace CsvPipeline
{
    /// <summary>
    /// 이 ScriptableObject를 표 한 장으로 저작한다고 선언합니다. <b>임포터 코드를 쓰지 않아도 됩니다.</b>
    /// 표가 저장되면 행마다 이 타입의 에셋이 만들어지고 갱신됩니다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class CsvAssetAttribute : Attribute
    {
        /// <summary>표와 산출물의 연결을 선언합니다.</summary>
        /// <param name="fileName">원본 표의 파일 이름입니다. (확장자 포함, 예: Clues.csv)</param>
        /// <param name="idColumn">에셋 이름이 될 열입니다. 이 열이 비면 그 행은 건너뜁니다.</param>
        public CsvAssetAttribute(string fileName, string idColumn)
        {
            FileName = fileName;
            IdColumn = idColumn;
        }

        /// <summary>원본 표의 파일 이름입니다.</summary>
        public string FileName { get; }

        /// <summary>에셋 이름이 될 열입니다.</summary>
        public string IdColumn { get; }

        /// <summary>
        /// 산출물이 놓일 폴더입니다. (예: <c>Assets/Data/Clues</c>)
        /// 비우면 <b>원본 표 옆의 타입 이름 폴더</b>에 놓습니다. 표를 통째로 옮겨도 따라가므로
        /// 배포하는 예제처럼 설치 위치를 알 수 없는 경우에 씁니다.
        /// </summary>
        public string OutputFolder { get; set; }

        /// <summary>
        /// 직렬화되는 필드를 열 이름으로 자동 연결할지 여부입니다. 기본은 켜짐입니다.
        /// 대소문자를 무시하고 맞추므로 <c>maxSpeed</c> 필드가 <c>MaxSpeed</c> 열에 붙습니다.
        /// 끄면 <see cref="CsvColumnAttribute"/>를 붙인 필드만 연결합니다.
        /// </summary>
        public bool AutoMap { get; set; } = true;

        /// <summary>
        /// 표에서 사라진 행의 에셋을 정리할지 여부입니다. 기본은 켜짐입니다.
        /// 켜져 있어도 <b>다른 곳에서 참조 중인 에셋은 지우지 않고</b> 경고만 남깁니다.
        /// </summary>
        public bool DeleteMissing { get; set; } = true;

        /// <summary>
        /// 정리 대조를 에셋 <b>이름</b>이 아니라 <b>경로</b>로 할지 여부입니다. 기본은 이름입니다.
        /// <para>
        /// 산출물 폴더 안에 이 표가 만들지 않은 같은 타입의 에셋이 섞여 있을 때 켭니다.
        /// 이름으로 대조하면 그런 에셋이 "표에서 사라진 것"으로 보여 지워질 수 있습니다.
        /// (참조가 남아 있으면 보존되지만, 그것까지 기대할 일은 아닙니다)
        /// </para>
        /// </summary>
        public bool ReconcileByPath { get; set; }
    }

    /// <summary>
    /// 필드를 특정 열에 연결합니다. 이름이 다르거나 동작을 바꿔야 할 때만 붙이면 됩니다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class CsvColumnAttribute : Attribute
    {
        /// <summary>필드를 열에 연결합니다.</summary>
        /// <param name="name">열 이름입니다. 비우면 필드 이름을 그대로 씁니다.</param>
        public CsvColumnAttribute(string name = null) { Name = name; }

        /// <summary>열 이름입니다. 비어 있으면 필드 이름을 씁니다.</summary>
        public string Name { get; }

        /// <summary>이 열이 표에 반드시 있어야 하는지 여부입니다. 없으면 표 전체를 반영하지 않습니다.</summary>
        public bool Required { get; set; }

        /// <summary>
        /// 셀이 비었을 때 <b>기존 값을 덮어쓸지</b> 여부입니다. 기본은 꺼짐(=보존)입니다.
        /// 인스펙터에서 저작한 값이 빈 셀 때문에 날아가지 않게 하는 것이 기본 동작입니다.
        /// </summary>
        public bool OverwriteWhenEmpty { get; set; }

        /// <summary>
        /// 리스트 셀을 나눌 구분자들입니다. 비우면 기본값(<c>;</c> 과 <c>|</c>)을 씁니다.
        /// </summary>
        public string Separators { get; set; }

        /// <summary>
        /// 오브젝트 참조를 이름으로 찾을 때 검색 범위를 이 폴더로 한정합니다. 비우면 프로젝트 전체입니다.
        /// </summary>
        public string ReferenceFolder { get; set; }
    }

    /// <summary>이 필드는 표와 연결하지 않습니다. (<see cref="CsvAssetAttribute.AutoMap"/>이 켜진 타입에서 제외할 때)</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class CsvIgnoreAttribute : Attribute
    {
    }
}
