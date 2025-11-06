using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using Sloth.Core.Models;

namespace Sloth.Core.Services
{
    public static class ExcelServiceExtensions
    {
        public static void WriteMoveLog(string outputXlsxPath, IEnumerable<MoveResult> rows)
        {
            var exists = File.Exists(outputXlsxPath);
            using var wb = exists ? new XLWorkbook(outputXlsxPath) : new XLWorkbook();
            var ws = exists && wb.Worksheets.TryGetWorksheet("Log", out var got) ? got : wb.AddWorksheet("Log");

            if (!exists || ws.Cell(1, 1).IsEmpty())
            {
                ws.Cell(1, 1).Value = "Time";
                ws.Cell(1, 2).Value = "Executed";
                ws.Cell(1, 3).Value = "Success";
                ws.Cell(1, 4).Value = "CustomerId";
                ws.Cell(1, 5).Value = "CustomerName";
                ws.Cell(1, 6).Value = "DocCode";
                ws.Cell(1, 7).Value = "FileName";
                ws.Cell(1, 8).Value = "SourcePath";
                ws.Cell(1, 9).Value = "DestPath";
                ws.Cell(1, 10).Value = "MatchedRoot";
                ws.Cell(1, 11).Value = "Message";
            }

            var last = ws.LastRowUsed()?.RowNumber() ?? 1;
            int r = last + 1;

            foreach (var x in rows)
            {
                ws.Cell(r, 1).Value = DateTime.Now;
                ws.Cell(r, 2).Value = x.Executed;
                ws.Cell(r, 3).Value = x.Success;
                ws.Cell(r, 4).Value = x.CustomerId;
                ws.Cell(r, 5).Value = x.CustomerName;
                ws.Cell(r, 6).Value = x.DocCode;
                ws.Cell(r, 7).Value = x.FileName;
                ws.Cell(r, 8).Value = x.SourcePath;
                ws.Cell(r, 9).Value = x.DestPath ?? "";
                ws.Cell(r, 10).Value = x.MatchedRoot ?? "";
                ws.Cell(r, 11).Value = x.Message;
                r++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(outputXlsxPath);
        }
    }
}