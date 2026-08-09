using UnityEngine;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 자동 연결과 왕복을 검사할 대상입니다. 필드는 camelCase, 표의 열은 PascalCase입니다.
    /// <b>파일 이름을 타입 이름과 맞춰야 합니다.</b> 그러지 않으면 Unity가 이 타입의 MonoScript를 찾지 못해
    /// <c>AssetDatabase.CreateAsset</c>이 스크립트 참조가 빈 에셋을 만듭니다.
    /// </summary>
    [CsvAsset("CsvPipelineTests_Widgets.csv", "Id")]
    public sealed class WidgetData : ScriptableObject
    {
        /// <summary>표시 이름입니다. Title 열이 붙습니다.</summary>
        public string title;

        /// <summary>최고 속도입니다. MaxSpeed 열이 붙습니다.</summary>
        public float maxSpeed;

        /// <summary>재고 수량입니다. Stock 열이 붙습니다.</summary>
        public int stock;

        /// <summary>비공개 직렬화 필드도 연결되는지 봅니다. (값은 임포터가 넣습니다)</summary>
        [SerializeField] private string ownerId = string.Empty;

        /// <summary>표와 연결하지 않는 필드입니다.</summary>
        [CsvIgnore] public Sprite artwork;

        /// <summary>이름이 다른 열에 붙는 필드입니다.</summary>
        [CsvColumn("HP", Required = true)] public int health;

        /// <summary>이 테스트에서만 쓰는 읽기 접근자입니다.</summary>
        public string OwnerId => ownerId;
    }
}
