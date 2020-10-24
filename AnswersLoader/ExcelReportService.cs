using System;
using System.Collections.Generic;
using System.IO;
using OfficeOpenXml;

namespace AnswersLoader
{
    public class ExcelReportService
    {
        private List<GroupReport> _reports;

        public ExcelReportService(List<GroupReport> reports)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            _reports = reports;
        }

        public void GenerateExcelReport(string filename)
        {
            var file = new FileInfo(filename);
            if (file.Exists) { file.Delete(); }

            using var package = new ExcelPackage(file);
            foreach (var report in _reports)
            {
                WriteGroupReport(report, package);
            }
                
            package.Save();
        }

        private void WriteGroupReport(GroupReport report, ExcelPackage package)
        {
            var ws = package.Workbook.Worksheets.Add(report.Group.Name);
            for (int i = 0; i < report.Group.GetTestsDates().Count; i++)
            {
                ws.Cells[1, i + 2].Value = report.Group.GetTestsDates()[i].ToShortDateString();
            }

            int row = 2;
            foreach (var kv in report.UsersWithMarks)
            {
                ws.Cells[row, 1].Value = kv.Key;

                for (int i = 0; i < kv.Value.Count; i++)
                {
                    ws.Cells[row, i + 2].Value = kv.Value[i] ? "+" : "-";
                }
                
                row++;
            }
        }
    }
}