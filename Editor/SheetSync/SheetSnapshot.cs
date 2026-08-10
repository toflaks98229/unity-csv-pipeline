using System.IO;
using System.Text;

namespace CsvPipeline
{
    /// <summary>
    /// 마지막으로 받아 기록한 내용의 사본을 관리합니다.
    /// 이 사본이 있어야 <b>"시트가 바뀐 것"과 "로컬을 손으로 고친 것"을 구분</b>할 수 있습니다.
    /// 버전 관리 대상이 아닌 곳에 두므로, 없어도 동작은 하고 경고만 사라집니다.
    /// </summary>
    public static class SheetSnapshot
    {
        /// <summary>사본들이 놓이는 폴더입니다.</summary>
        private static string Root => CsvPipelineSettings.Instance.SnapshotFolder;

        /// <summary>사본 폴더가 있게 합니다.</summary>
        public static void EnsureFolder() => Directory.CreateDirectory(Root);

        /// <summary>지정 표의 사본 경로입니다.</summary>
        /// <param name="csvFileName">표 파일 이름입니다.</param>
        /// <returns>사본 파일 경로입니다.</returns>
        public static string PathFor(string csvFileName) => Path.Combine(Root, csvFileName);

        /// <summary>
        /// 마지막 동기화 이후 로컬이 직접 수정됐는지 여부입니다.
        /// 사본이 없으면 <b>판단할 수 없으므로 false</b>입니다. 없는 것을 수정으로 몰지 않습니다.
        /// </summary>
        /// <param name="csvFileName">표 파일 이름입니다.</param>
        /// <param name="localText">지금 로컬 파일의 정규화된 내용입니다.</param>
        /// <returns>수정된 흔적이 있으면 true입니다.</returns>
        public static bool DivergedFromLocal(string csvFileName, string localText)
        {
            string path = PathFor(csvFileName);
            if (!File.Exists(path)) return false;

            return SheetDiff.Normalize(File.ReadAllText(path)) != localText;
        }

        /// <summary>이번에 받은 내용을 사본으로 남깁니다.</summary>
        /// <param name="csvFileName">표 파일 이름입니다.</param>
        /// <param name="text">기록할 내용입니다.</param>
        public static void Write(string csvFileName, string text)
            => File.WriteAllText(PathFor(csvFileName), text, new UTF8Encoding(false));
    }
}
