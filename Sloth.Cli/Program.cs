using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sloth.Core.Models;
using Sloth.Core.Services;

static string Arg(string key)
{
    var a = Environment.GetCommandLineArgs();
    for (int i = 0; i < a.Length - 1; i++)
        if (a[i].Equals(key, StringComparison.OrdinalIgnoreCase))
            return a[i + 1];
    return "";
}

static void Usage()
{
    Console.WriteLine(@"
Usage (preview):
  dotnet run --project Sloth.Cli -- \
    --mode preview \
    --config /path/SlothConfig.json \
    --excel /path/Customers.xlsx \
    --dest-root /DestRoot \
    --src-dir /SourceFiles \
    --customer-id C24001 \
    --doc-code ""현장점검표""

Usage (run with log):
  dotnet run --project Sloth.Cli -- \
    --mode run \
    --config /path/SlothConfig.json \
    --excel /path/Customers.xlsx \
    --dest-root /DestRoot \
    --src-dir /SourceFiles \
    --customer-id C24001 \
    --doc-code ""현장점검표"" \
    --log /path/SlothLog.xlsx

Optional:
  --search-depth 3     # directory search depth for matching (default 3)
");
}

// ---- read args ----
var mode        = Arg("--mode");
var cfgPath     = Arg("--config");
var xlsPath     = Arg("--excel");
var destRoot    = Arg("--dest-root");
var srcDir      = Arg("--src-dir");
var custId      = Arg("--customer-id");
var docCode     = Arg("--doc-code");
var logPath     = Arg("--log");
var depthArg    = Arg("--search-depth");
if (string.IsNullOrWhiteSpace(mode)) mode = "preview";
int maxDepth = 3;
_ = int.TryParse(depthArg, out maxDepth);

// ---- validate args ----
bool Missing(params (string key, string val)[] reqs)
{
    foreach (var (k, v) in reqs)
        if (string.IsNullOrWhiteSpace(v)) { Console.WriteLine($"Missing required arg: {k}"); return true; }
    return false;
}

if (Missing(("--config", cfgPath), ("--excel", xlsPath), ("--dest-root", destRoot),
            ("--src-dir", srcDir), ("--customer-id", custId), ("--doc-code", docCode)))
{
    Usage();
    return;
}
if (!File.Exists(cfgPath))  { Console.WriteLine($"Config not found: {cfgPath}"); return; }
if (!File.Exists(xlsPath))  { Console.WriteLine($"Excel not found: {xlsPath}"); return; }
if (!Directory.Exists(destRoot)) { Console.WriteLine($"Dest root not found: {destRoot}"); return; }
if (!Directory.Exists(srcDir))   { Console.WriteLine($"Source dir not found: {srcDir}"); return; }

// ---- load config & customers ----
var cfg = ConfigService.Load(cfgPath);
var customers = ExcelService.ReadCustomers(xlsPath);
if (customers.Count == 0) { Console.WriteLine("No customers loaded from Excel."); return; }

var customer = customers.FirstOrDefault(c => string.Equals(c.CustomerId, custId, StringComparison.OrdinalIgnoreCase));
if (customer is null) { Console.WriteLine($"CustomerId not found in Excel: {custId}"); return; }

if (!cfg.DocumentSets.TryGetValue(customer.Category ?? "", out var docSet) || docSet is null || docSet.Count == 0)
{
    Console.WriteLine($"No document set for category '{customer.Category}'. Check SlothConfig.json.");
    return;
}

var doc = docSet.FirstOrDefault(d => string.Equals(d.Code, docCode, StringComparison.OrdinalIgnoreCase));
if (doc is null)
{
    Console.WriteLine($"DocCode '{docCode}' not found in document set for '{customer.Category}'.");
    Console.WriteLine("Available codes: " + string.Join(", ", docSet.Select(d => d.Code)));
    return;
}

// ---- collect source files ----
var files = Directory.EnumerateFiles(srcDir).ToList();
if (files.Count == 0) { Console.WriteLine($"No files found in {srcDir}"); return; }

var sources = files.Select(f => new SourceDoc
{
    SourcePath = f,
    CustomerId = custId,
    DocCode = docCode
}).ToList();

// ---- preview or run ----
bool execute = string.Equals(mode, "run", StringComparison.OrdinalIgnoreCase);

// Run the pipeline (preview = no actual move; run = move + log)
var results = BatchMoveService.PreviewAndExecute(sources, customers, cfg, destRoot, execute);

// ---- print summary ----
foreach (var r in results)
{
    var flag = r.Success ? "OK " : "ERR";
    var kind = r.Executed ? "EXEC" : "PREV";
    Console.WriteLine($"{flag} | {kind} | {r.CustomerName} [{r.CustomerId}] | {r.DocCode} | {r.FileName}");
    if (!r.Success) Console.WriteLine("   -> " + r.Message);
}

if (execute && !string.IsNullOrWhiteSpace(logPath))
{
    try
    {
        ExcelServiceExtensions.WriteMoveLog(logPath, results);
        Console.WriteLine("Log written: " + logPath);
    }
    catch (Exception ex)
    {
        Console.WriteLine("Failed to write log: " + ex.Message);
    }
}