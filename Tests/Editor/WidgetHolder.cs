using UnityEngine;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 구워진 에셋을 <b>붙잡고 있는</b> 쪽입니다. "참조가 남으면 지우지 않는다" 는 규칙을
    /// 실제 AssetDatabase 위에서 확인하려면, 실제로 참조하는 에셋이 하나 있어야 합니다.
    /// <b>파일 이름을 타입 이름과 맞춰야 합니다.</b> 그러지 않으면 스크립트 참조가 빈 에셋이 만들어집니다.
    /// </summary>
    public sealed class WidgetHolder : ScriptableObject
    {
        /// <summary>붙잡고 있는 산출물입니다.</summary>
        public WidgetData widget;
    }
}
