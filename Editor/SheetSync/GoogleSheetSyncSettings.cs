using System.Text.RegularExpressions;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// CSV 파일 <b>하나</b>를 구글 스프레드시트 탭 하나에 연결하는 설정입니다. <b>에디터 전용</b>입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "SheetSync_", menuName = "CSV Pipeline/Google Sheet Sync Settings")]
    public class GoogleSheetSyncSettings : ScriptableObject
    {
        /// <summary>대상 CSV 파일 이름입니다. (확장자 포함, 예: Monster_Stats.csv)</summary>
        [Tooltip("CSV 루트 폴더 안의 대상 파일 이름입니다. 확장자를 포함합니다.")]
        public string csvFileName;

        /// <summary>
        /// 구글 시트 주소입니다. <b>브라우저 주소창의 링크를 그대로 붙여넣으면 됩니다.</b>
        /// </summary>
        [Tooltip("브라우저 주소창의 시트 링크를 그대로 붙여넣으세요. 열어 둔 탭이 대상이 됩니다.")]
        [TextArea(2, 4)]
        public string sheetUrl;

        /// <summary>이 파일을 동기화 대상에 포함할지 여부입니다.</summary>
        [Tooltip("끄면 이 파일은 받아오지 않습니다. (링크를 채우기 전에는 꺼 두세요)")]
        public bool enabled = true;

        [Header("자동 받기")]
        /// <summary>에디터가 주기적으로 시트를 확인해 받아올지 여부입니다.</summary>
        [Tooltip("켜면 아래 간격마다 자동으로 확인해 바뀌었을 때만 받아옵니다.")]
        public bool autoPull;

        /// <summary>자동 받기 간격(초)입니다.</summary>
        [Tooltip("자동 받기 간격(초)입니다. 너무 짧으면 편집 도중의 미완성 값이 들어옵니다.")]
        [Min(10f)]
        public float autoPullIntervalSeconds = 120f;

        [Header("안전장치")]
        /// <summary>받은 내용의 첫 줄(헤더)이 기존 파일과 다르면 확인을 받을지 여부입니다.</summary>
        [Tooltip("헤더가 기존과 다르면 덮어쓰기 전에 확인합니다. 엉뚱한 탭을 가리키는 실수를 잡아 줍니다.")]
        public bool confirmOnHeaderChange = true;

        /// <summary>주소에서 스프레드시트 ID를 뽑아 냅니다. 찾지 못하면 빈 문자열입니다.</summary>
        public string SpreadsheetId
        {
            get
            {
                if (string.IsNullOrWhiteSpace(sheetUrl)) return string.Empty;

                Match match = Regex.Match(sheetUrl, @"/spreadsheets/d/([a-zA-Z0-9\-_]+)");
                return match.Success ? match.Groups[1].Value : string.Empty;
            }
        }

        /// <summary>주소에서 탭 번호(gid)를 뽑아 냅니다. 없으면 첫 번째 탭인 "0"입니다.</summary>
        public string Gid
        {
            get
            {
                if (string.IsNullOrWhiteSpace(sheetUrl)) return "0";

                Match match = Regex.Match(sheetUrl, @"[?&#]gid=([0-9]+)");
                return match.Success ? match.Groups[1].Value : "0";
            }
        }

        /// <summary>동기화에 필요한 값이 모두 채워졌는지 여부입니다.</summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(csvFileName) && !string.IsNullOrEmpty(SpreadsheetId);

        /// <summary>내려받기에 사용할 CSV 내보내기 주소입니다.</summary>
        public string ExportUrl =>
            $"https://docs.google.com/spreadsheets/d/{SpreadsheetId}/export?format=csv&gid={Gid}";
    }
}
