using System.Collections.Generic;
using CsvPipeline;
using UnityEngine;

namespace CsvPipeline.Samples.QuickStart
{
    /// <summary>Difficulty of a quest. Write this name as is in the table. (Case does not matter.)</summary>
    public enum QuestDifficulty
    {
        /// <summary>Easy.</summary>
        Easy,

        /// <summary>Normal.</summary>
        Normal,

        /// <summary>Hard.</summary>
        Hard
    }

    /// <summary>
    /// One row of <c>Quests.csv</c> becomes one of these assets.
    /// No output folder is given, so the assets land in <b>a QuestData folder next to the table</b>.
    /// (The sample cannot know its install location in advance, which is why it is left this way. Normally it is
    ///  better to spell it out, as in <c>OutputFolder = "Assets/Data/Quests"</c>.)
    /// </summary>
    [CsvAsset("Quests.csv", "Id")]
    public sealed class QuestData : ScriptableObject
    {
        /// <summary>Display name. The <c>Title</c> column of the table binds here. (Fields are camelCase, columns are PascalCase.)</summary>
        public string title;

        /// <summary>Description. It contains commas, so wrap it in double quotes in the table.</summary>
        public string description;

        /// <summary>Recommended level.</summary>
        public int recommendedLevel;

        /// <summary>Time limit. A value that looks like an integer, such as <c>600</c>, still binds as a float.</summary>
        public float timeLimit;

        /// <summary>Whether the quest is repeatable. Write <c>TRUE</c>/<c>FALSE</c> in the table.</summary>
        public bool repeatable;

        /// <summary>Difficulty. Write the enum name.</summary>
        public QuestDifficulty difficulty;

        /// <summary>Reward item codes. Separate them with <c>;</c> inside one cell.</summary>
        public List<string> rewards = new List<string>();

        /// <summary>Prerequisite quest. The column name differs from the field name, so it is given explicitly.</summary>
        [CsvColumn("Requires")] public string requiredQuestId;

        /// <summary>
        /// A field the table does not author. You set it in the Inspector, and it survives a reimport.
        /// </summary>
        [CsvIgnore] public Sprite banner;
    }
}
