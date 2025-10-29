using System.Collections.Generic;
using ClosedXML.Excel;
using Sloth.Core.Models;

namespace Sloth.Core.Services;

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
            int no         = ws.Cell(r, 1).GetValue<int>();
            string id      = ws.Cell(r, 2).GetString();
            string name    = ws.Cell(r, 3).GetString();
            string cat     = ws.Cell(r, 4).GetString();
            string corp    = ws.Cell(r, 5).GetString();
            string road    = ws.Cell(r, 6).GetString();
            string lot     = ws.Cell(r, 7).GetString();
            string folder  = ws.Cell(r, 8).GetString();

            list.Add(new Customer(
                no, id, name, cat, corp, road, lot,
                string.IsNullOrWhiteSpace(folder) ? null : folder
            ));
        }
        return list;
    }
}