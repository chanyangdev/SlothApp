using System; 
using System.Collections.Generic;
using ClosedXML.Excel;
using Sloth.Core.Models;

namespace Sloth.Core.Services;

public static partial class ExcelServiceExtensions
{
    public static void WriteMoveLog(string outputXlsxPath, IEnumerable<MoveResult> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Log");

         // Header
        ws.Cell(1, 1).Value = "Time";
        ws.Cell(1, 2).Value = "Executed";
        ws.Cell(1, 3).Value = "Success";
        ws.Cell(1, 4).Value = "CustomerId";
        ws.Cell(1, 5).Value = "CustomerName";
        ws.Cell(1, 6).Value = "DocCode";
        ws.Cell(1, 7).Value = "FileName";
        ws.Cell(1, 8).Value = "SourcePath";
        ws.Cell(1, 9).Value = "DestPath";
        ws.Cell(1,10).Value = "Message";

        int r = 2;
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
            ws.Cell(r,10).Value = x.Message;
            r++;
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(outputXlsxPath);
    }
}

public static class ExcelService
{
    // Expected headers (row 1):
    // No | CustomerId | Name | Category | 법인 | 고객_도로명 | 고객_지번 | FolderPath
    public static List<Customer> ReadCustomers(string excelPath)
    {
        var list = new List<Customer>();
        using var wb = new XLWorkbook(excelPath);
        var ws = wb.Worksheet(1);
        var used = ws.RangeUsed();
        if (used is null) return list;

        int lastRow = used.RowCount();
        for (int r = 2; r <= lastRow; r++)
        {
            int no = ws.Cell(r, 1).GetValue<int>();
            string id = ws.Cell(r, 2).GetString();
            string name = ws.Cell(r, 3).GetString();
            string cat = ws.Cell(r, 4).GetString();
            string corp = ws.Cell(r, 5).GetString();
            string road = ws.Cell(r, 6).GetString();
            string lot = ws.Cell(r, 7).GetString();
            string folder = ws.Cell(r, 8).GetString();

            list.Add(new Customer(
                no, id, name, cat, corp, road, lot,
                string.IsNullOrWhiteSpace(folder) ? null : folder
            ));
        }
        return list;
    }
}