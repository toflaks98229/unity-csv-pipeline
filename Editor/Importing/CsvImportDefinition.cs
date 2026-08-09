using System.Collections.Generic;
using UnityEditor;

namespace CsvPipeline
{
    /// <summary>
    /// 고정 파일명 표 하나를 감시해 굽는 임포터의 뼈대입니다.
    /// 파일 감지·원본 삭제 경고·파싱·열 검증·결과 보고·저장까지의 반복되는 절차를 담고,
    /// 실제 굽기만 파생 클래스에 맡깁니다.
    /// </summary>
    public abstract class CsvImportDefinition
    {
        /// <summary>감시할 원본 파일 이름입니다. (확장자 포함)</summary>
        protected abstract string FileName { get; }

        /// <summary>
        /// 산출물이 놓이는 폴더입니다. null이면 원본이 사라져도 경고하지 않습니다.
        /// (기존 에셋만 갱신하고 새로 만들지 않는 임포터용)
        /// </summary>
        protected virtual string OutputFolder => null;

        /// <summary>
        /// 표에 반드시 있어야 하는 열들입니다. 하나라도 없으면 <b>아무것도 반영하지 않고</b> 오류로 보고합니다.
        /// 빠진 열을 빈 셀로 취급해 조용히 기본값을 굽는 사고를 막습니다.
        /// </summary>
        protected virtual IEnumerable<string> RequiredColumns => null;

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
        /// 에셋 임포트 일괄 통지를 받아, 이 임포터가 감시하는 표가 관련됐을 때만 굽기를 수행합니다.
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

            Run(csvPath);
        }

        /// <summary>
        /// 지정 경로의 표를 읽어 굽고 결과를 보고합니다. 메뉴에서 직접 부를 수도 있습니다.
        /// </summary>
        /// <param name="csvPath">읽을 표의 에셋 경로입니다.</param>
        /// <returns>임포트 결과 리포트입니다. 읽을 것이 없으면 null입니다.</returns>
        public CsvImportReport Run(string csvPath)
        {
            CsvTable table = CsvImportUtil.ReadTable(csvPath);
            if (table == null) return null;

            var report = new CsvImportReport(FileName, LogTag);

            if (!ValidateColumns(table, report))
            {
                report.Emit();
                return report;
            }

            Process(table, report);
            report.Emit();

            if (report.Touched) AssetDatabase.SaveAssets();
            return report;
        }

        /// <summary>
        /// 요구한 열이 전부 있는지 확인합니다. 빠진 열은 대소문자만 다른 후보까지 함께 알립니다.
        /// </summary>
        /// <param name="table">검사할 표입니다.</param>
        /// <param name="report">결과를 기록할 리포트입니다.</param>
        /// <returns>진행해도 되면 true입니다.</returns>
        private bool ValidateColumns(CsvTable table, CsvImportReport report)
        {
            List<string> missing = table.FindMissingColumns(RequiredColumns);
            if (missing.Count == 0) return true;

            foreach (string column in missing)
            {
                string similar = table.FindSimilarColumn(column);
                report.Error(similar != null
                    ? $"열 '{column}'이(가) 없습니다. 대소문자만 다른 '{similar}'이(가) 있습니다."
                    : $"열 '{column}'이(가) 없습니다. 표의 헤더: {string.Join(", ", table.Headers)}");
            }

            report.Info("빠진 열을 빈 셀로 굽지 않도록 이 표는 반영하지 않았습니다.");
            return false;
        }

        /// <summary>
        /// 파싱된 표를 받아 에셋을 굽습니다.
        /// </summary>
        /// <param name="table">파싱된 표입니다. 행이 하나 이상 있습니다.</param>
        /// <param name="report">건수와 문제를 기록할 리포트입니다.</param>
        protected abstract void Process(CsvTable table, CsvImportReport report);
    }
}
