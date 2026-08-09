using System.Collections.Generic;
using UnityEngine;

namespace CsvPipeline.Tests
{
    /// <summary>테스트에서 쓰는 열거형입니다. 인덱스와 값이 어긋나 있어 이름 매칭을 검사할 수 있습니다.</summary>
    public enum Grade
    {
        /// <summary>작음입니다.</summary>
        Small = 10,

        /// <summary>중간입니다.</summary>
        Medium = 20,

        /// <summary>큼입니다.</summary>
        Large = 30
    }

    /// <summary>
    /// 값 변환기가 다뤄야 하는 타입을 한 벌 모아 둔 대상입니다. 에셋으로 저장하지 않고 메모리에서만 씁니다.
    /// </summary>
    public sealed class BinderTarget : ScriptableObject
    {
        /// <summary>문자열 필드입니다.</summary>
        public string text = "기존값";

        /// <summary>정수 필드입니다.</summary>
        public int count = 7;

        /// <summary>실수 필드입니다.</summary>
        public float speed = 1.5f;

        /// <summary>배정밀도 필드입니다.</summary>
        public double precise = 2.5;

        /// <summary>불리언 필드입니다.</summary>
        public bool flag;

        /// <summary>열거형 필드입니다.</summary>
        public Grade grade = Grade.Small;

        /// <summary>색상 필드입니다.</summary>
        public Color tint = Color.black;

        /// <summary>3차원 벡터 필드입니다.</summary>
        public Vector3 offset;

        /// <summary>문자열 리스트 필드입니다.</summary>
        public List<string> tags = new List<string>();

        /// <summary>열거형 배열 필드입니다.</summary>
        public Grade[] grades = new Grade[0];

        /// <summary>오브젝트 참조 필드입니다.</summary>
        public Texture2D icon;
    }

    /// <summary>자동 연결을 검사할 대상입니다. 필드는 camelCase, 표의 열은 PascalCase입니다.</summary>
    [CsvAsset("CsvPipelineTests_Widgets.csv", "Id")]
    public sealed class WidgetData : ScriptableObject
    {
        /// <summary>표시 이름입니다. Title 열에 붙습니다.</summary>
        public string title;

        /// <summary>최고 속도입니다. MaxSpeed 열에 붙습니다.</summary>
        public float maxSpeed;

        /// <summary>재고 수량입니다. Stock 열에 붙습니다.</summary>
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
