using System.Collections.Generic;
using UnityEngine;

namespace CsvPipeline.Tests
{
    /// <summary>테스트에서 쓰는 열거형입니다. 값과 인덱스가 어긋나 있어 이름 매칭을 검사할 수 있습니다.</summary>
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
}
