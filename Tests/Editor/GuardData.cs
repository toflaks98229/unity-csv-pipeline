using UnityEngine;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 안전장치 검사가 쓰는 표입니다. 굽고 정리하는 과정을 끝까지 지나가야 하므로 선언을 기본값으로 둡니다.
    /// <b>파일 이름을 타입 이름과 맞춰야 합니다.</b> 그러지 않으면 스크립트 참조가 빈 에셋이 만들어집니다.
    /// </summary>
    [CsvAsset("CsvPipelineTests_Guard.csv", "Id", OutputFolder = "Assets/Memory/Guard")]
    public sealed class GuardData : ScriptableObject
    {
        /// <summary>표에서 오는 값입니다.</summary>
        public string title;
    }
}
