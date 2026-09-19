using UnityEngine;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 표시가 붙은 필드만 받도록 선언해 놓고 <b>아무 필드에도 표시를 붙이지 않은</b> 타입입니다.
    /// 넣을 것만 적는 방식에서 가장 흔한 첫 실수라, 그때 무엇을 말해 주는지 검사합니다.
    /// <b>파일 이름을 타입 이름과 맞춰야 합니다.</b> 그러지 않으면 스크립트 참조가 빈 에셋이 만들어집니다.
    /// </summary>
    [CsvAsset("CsvPipelineTests_OptInEmpty.csv", "Id", AutoMap = false,
              OutputFolder = "Assets/Memory/OptInEmptyTarget")]
    public sealed class OptInEmptyTarget : ScriptableObject
    {
        /// <summary>표시가 없어 빠지는 필드입니다.</summary>
        public string title;

        /// <summary>표시가 없어 빠지는 필드입니다.</summary>
        public int cost;
    }
}
