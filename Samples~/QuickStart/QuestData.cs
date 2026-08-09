using System.Collections.Generic;
using CsvPipeline;
using UnityEngine;

namespace CsvPipeline.Samples.QuickStart
{
    /// <summary>퀘스트의 난이도입니다. 표에는 이 이름을 그대로 적습니다. (대소문자 무관)</summary>
    public enum QuestDifficulty
    {
        /// <summary>쉬움입니다.</summary>
        Easy,

        /// <summary>보통입니다.</summary>
        Normal,

        /// <summary>어려움입니다.</summary>
        Hard
    }

    /// <summary>
    /// <c>Quests.csv</c> 한 행이 이 에셋 하나가 됩니다.
    /// 산출물 폴더를 적지 않았으므로 <b>표 옆의 QuestData 폴더</b>에 놓입니다.
    /// (샘플은 설치 위치를 미리 알 수 없어 이렇게 두었습니다. 보통은
    ///  <c>OutputFolder = "Assets/Data/Quests"</c> 처럼 명시하는 편이 낫습니다.)
    /// </summary>
    [CsvAsset("Quests.csv", "Id")]
    public sealed class QuestData : ScriptableObject
    {
        /// <summary>표시 이름입니다. 표의 <c>Title</c> 열이 붙습니다. (필드는 camelCase, 열은 PascalCase)</summary>
        public string title;

        /// <summary>설명입니다. 쉼표가 들어가므로 표에서는 큰따옴표로 감쌉니다.</summary>
        public string description;

        /// <summary>권장 레벨입니다.</summary>
        public int recommendedLevel;

        /// <summary>제한 시간입니다. 값이 <c>600</c>처럼 정수로 보여도 실수로 들어갑니다.</summary>
        public float timeLimit;

        /// <summary>반복 가능 여부입니다. 표에는 <c>TRUE</c>/<c>FALSE</c>로 적습니다.</summary>
        public bool repeatable;

        /// <summary>난이도입니다. 열거형 이름으로 적습니다.</summary>
        public QuestDifficulty difficulty;

        /// <summary>보상 아이템 코드들입니다. 한 셀에 <c>;</c> 로 나눠 적습니다.</summary>
        public List<string> rewards = new List<string>();

        /// <summary>선행 퀘스트입니다. 열 이름이 필드 이름과 달라 직접 지정했습니다.</summary>
        [CsvColumn("Requires")] public string requiredQuestId;

        /// <summary>
        /// 표로 저작하지 않는 필드입니다. 인스펙터에서 붙이며, 재임포트해도 유지됩니다.
        /// </summary>
        [CsvIgnore] public Sprite banner;
    }
}
