using UnityEngine;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 정리 대조를 <b>경로로</b> 하는 표입니다.
    /// 산출물 폴더에 이 표가 만들지 않은 같은 타입 에셋이 섞여 있을 때의 거동을 봅니다.
    /// <b>파일 이름을 타입 이름과 맞춰야 합니다.</b> 그러지 않으면 스크립트 참조가 빈 에셋이 만들어집니다.
    /// </summary>
    [CsvAsset("CsvPipelineTests_ByPath.csv", "Id",
              OutputFolder = "Assets/Memory/ByPath", ReconcileByPath = true)]
    public sealed class ByPathData : ScriptableObject
    {
        /// <summary>표에서 오는 값입니다.</summary>
        public string title;
    }
}
