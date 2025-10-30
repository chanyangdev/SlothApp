using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using Microsoft.VisualBasic.FileIO;   // CSV parser
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
        ws.Cell(1, 10).Value = "Message";

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
            ws.Cell(r, 10).Value = x.Message;
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
    public static List<Customer> ReadCustomers(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".csv" => ReadCustomersFromCsv(path),
            ".xlsx" or ".xlsm" or ".xls" => ReadCustomersFromXlsx(path),
            _ => throw new InvalidOperationException($"Unsupported file type: {ext}")
        };
    }

    private static List<Customer> ReadCustomersFromXlsx(string excelPath)
    {
        var list = new List<Customer>();
        using var wb = new XLWorkbook(excelPath);
        var ws = wb.Worksheet(1);
        var used = ws.RangeUsed();
        if (used is null) return list;

        int lastRow = used.RowCount();
        for (int r = 2; r <= lastRow; r++)
        {
            // stop early if the key fields are empty
            var id = ws.Cell(r, 2).GetString();
            var name = ws.Cell(r, 3).GetString();
            if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(name))
                continue;

            int no = ws.Cell(r, 1).GetValue<int>();
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

    private static List<Customer> ReadCustomersFromCsv(string csvPath)
    {
        var rows = new List<Customer>();

        using var parser = new TextFieldParser(csvPath, Encoding.UTF8)
        {
            TextFieldType = FieldType.Delimited,
            Delimiters = new[] { "," },
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };

        // Read header and basic validation
        string[] header = Array.Empty<string>();
        if (!parser.EndOfData)
        {
            header = parser.ReadFields() ?? Array.Empty<string>();
            string[] must = { "No", "CustomerId", "Name", "Category", "법인", "고객_도로명", "고객_지번", "FolderPath" };
            foreach (var m in must)
                if (!header.Contains(m))
                    throw new InvalidOperationException($"CSV header missing required column: {m}");
        }

        while (!parser.EndOfData)
        {
            var f = parser.ReadFields() ?? Array.Empty<string>();
            if (f.Length == 0) continue;

            // map by index (CSV is created with the same column order)
            int no = int.TryParse(Get(f, 0), out var n) ? n : 0;
            string id = Get(f, 1);
            string name = Get(f, 2);
            string cat = Get(f, 3);
            string corp = Get(f, 4);
            string road = Get(f, 5);
            string lot = Get(f, 6);
            string? folder = Get(f, 7);

            if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(name))
                continue;

            rows.Add(new Customer(no, id, name, cat, corp, road, lot,
                                  string.IsNullOrWhiteSpace(folder) ? null : folder));
        }

        return rows;

        static string Get(string[] a, int i) => (i >= 0 && i < a.Length) ? (a[i] ?? string.Empty) : string.Empty;
    }
}
