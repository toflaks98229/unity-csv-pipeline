using UnityEngine;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// <b>표시가 붙은 필드만</b> 표와 연결하는 선언입니다. (<c>AutoMap = false</c>)
    /// 빼야 할 것을 하나씩 <c>[CsvIgnore]</c>로 적는 대신, 넣을 것만 <c>[CsvColumn]</c>으로 적습니다.
    /// <b>파일 이름을 타입 이름과 맞춰야 합니다.</b> 그러지 않으면 스크립트 참조가 빈 에셋이 만들어집니다.
    /// </summary>
    [CsvAsset("CsvPipelineTests_OptIn.csv", "Id", AutoMap = false,
              OutputFolder = "Assets/Memory/OptInTarget")]
    public sealed class OptInTarget : ScriptableObject
    {
        /// <summary>표시가 붙어 표와 연결되는 필드입니다.</summary>
        [CsvColumn] public string title;

        /// <summary>이름이 다른 열에 붙는 필드입니다.</summary>
        [CsvColumn("HP")] public int health;

        /// <summary>표시가 없어 빠지는 필드입니다. 표에 같은 이름의 열이 있어도 건드리지 않아야 합니다.</summary>
        public string note = "손으로 적은 값";

        /// <summary>표시가 없어 빠지는 참조입니다. 표시만 붙이면 이름으로 배선할 수 있습니다.</summary>
        public Texture2D icon;

        /// <summary>빼라고 적어 둔 필드입니다. <b>잊은 것이 아니라 정한 것</b>입니다.</summary>
        [CsvIgnore] public string secret;

        /// <summary>표시도 없고 애초에 표로 저작할 수도 없는 필드입니다.</summary>
        public Rect area;
    }
}
