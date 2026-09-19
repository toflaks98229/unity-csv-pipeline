using System.Collections.Generic;
using UnityEngine;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// <c>[CsvAsset]</c> 선언이 <b>아직 없는</b> 타입입니다. 표를 만들기 전의 타입을 흉내 냅니다.
    /// 표로 저작할 수 있는 필드와 없는 필드를 함께 담아, 생성기가 뒤를 빼는지 볼 수 있게 했습니다.
    /// <b>파일 이름을 타입 이름과 맞춰야 합니다.</b> 그러지 않으면 스크립트 참조가 빈 에셋이 만들어집니다.
    /// </summary>
    public sealed class TemplateTarget : ScriptableObject
    {
        /// <summary>표로 저작할 수 있는 문자열입니다.</summary>
        public string title;

        /// <summary>표로 저작할 수 있는 정수입니다.</summary>
        public int cost;

        /// <summary>표로 저작할 수 있는 목록입니다.</summary>
        public List<string> tags = new List<string>();

        /// <summary>이름이 다른 열에 붙는 필드입니다.</summary>
        [CsvColumn("HP")] public int health;

        /// <summary>표와 연결하지 않는 필드입니다.</summary>
        [CsvIgnore] public string note;

        /// <summary>표로 저작할 수 없는 필드입니다. 열에서 빠져야 합니다.</summary>
        public Rect area;

        /// <summary>표로 저작할 수 없는 필드입니다. 열에서 빠져야 합니다.</summary>
        public AnimationCurve curve = new AnimationCurve();

        /// <summary>원소가 표로 저작할 수 없는 목록입니다. 이것도 빠져야 합니다.</summary>
        public List<Rect> areas = new List<Rect>();
    }
}
