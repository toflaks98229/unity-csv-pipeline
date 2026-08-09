using System.Collections.Generic;
using UnityEditor;

namespace CsvPipeline
{
    /// <summary>
    /// 고정 파일명 CSV 하나를 감시해 굽는 임포터의 뼈대입니다.
    /// 파일 감지·원본 삭제 경고·행 파싱·저장까지의 반복되는 절차를 담고, 실제 굽기만 파생 클래스에 맡깁니다.
    /// </summary>
    public abstract class CsvImportDefinition
    {
        /// <summary>감시할 원본 CSV 파일 이름입니다. (확장자 포함)</summary>
        protected abstract string FileName { get; }

        /// <summary>
        /// 산출물이 놓이는 폴더입니다. null이면 원본 CSV가 사라져도 경고하지 않습니다.
        /// (기존 에셋만 갱신하고 새로 만들지 않는 임포터용)
        /// </summary>
        protected virtual string OutputFolder => null;

        /// <summary>
        /// 로그 접두 태그입니다. 기본값은 이 정의를 감싸는 바깥 클래스 이름이며, 없으면 자기 이름입니다.
        /// </summary>
        protected virtual string LogTag
        {
            get
            {
                System.Type type = GetType();
                return $"[{(type.DeclaringType != null ? type.DeclaringType.Name : type.Name)}]";
            }
        }

        /// <summary>
        /// 에셋 임포트 일괄 통지를 받아, 이 임포터가 감시하는 CSV가 관련됐을 때만 굽기를 수행합니다.
        /// </summary>
        /// <param name="imported">임포트된 에셋 경로들입니다.</param>
        /// <param name="deleted">삭제된 에셋 경로들입니다.</param>
        /// <param name="moved">이동된 에셋의 새 경로들입니다.</param>
        public void Execute(string[] imported, string[] deleted, string[] moved)
        {
            string fileName = FileName;
            string outputFolder = OutputFolder;

            if (outputFolder != null)
            {
                foreach (string path in deleted)
                {
                    if (CsvImportUtil.IsFile(path, fileName))
                        CsvAssetPipeline.WarnSourceRemoved(outputFolder, fileName, LogTag);
                }
            }

            if (!CsvImportUtil.Touched(imported, moved, fileName)) return;

            string csvPath = CsvAssetPipeline.FindCsvPath(fileName);
            if (csvPath == null) return;

            List<Dictionary<string, object>> raw = CsvImportUtil.ReadRows(csvPath);
            if (raw == null) return;

            var rows = new List<CsvRow>(raw.Count);
            foreach (Dictionary<string, object> cells in raw) rows.Add(new CsvRow(cells));

            if (!Process(rows)) return;

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 파싱된 전 행을 받아 에셋을 굽습니다.
        /// </summary>
        /// <param name="rows">파싱된 CSV 행들입니다. 비어 있지 않습니다.</param>
        /// <returns>에셋을 하나라도 건드렸으면 true입니다. false면 저장을 건너뜁니다.</returns>
        protected abstract bool Process(IReadOnlyList<CsvRow> rows);
    }
}
