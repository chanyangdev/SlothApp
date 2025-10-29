using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sloth.Core.Models;

namespace Sloth.Core.Services;

public static class BatchMoveService
{
    public static List<MoveResult> PreviewAndExecute(
        IEnumerable<SourceDoc> sources,
        List<Customer> customers,
        SlothConfig cfg,
        string destRoot,
        bool execute)
    {
        var results = new List<MoveResult>();
        var byId = customers.ToDictionary(k => k.CustomerId, k => k, StringComparer.OrdinalIgnoreCase);

        foreach (var s in sources)
        {
            var r = new MoveResult
            {
                SourcePath = s.SourcePath,
                CustomerId = s.CustomerId,
                DocCode = s.DocCode
            };

            try
            {
                if (string.IsNullOrWhiteSpace(s.SourcePath) || !File.Exists(s.SourcePath))
                    throw new FileNotFoundException("Source file not found.", s.SourcePath);

                if (!byId.TryGetValue(s.CustomerId ?? "", out var cust))
                    throw new InvalidOperationException($"CustomerId '{s.CustomerId}' not found in Excel.");

                r.CustomerName = cust.Name ?? "";

                if (!cfg.DocumentSets.TryGetValue(cust.Category ?? "", out var docSet) || docSet is null)
                    throw new InvalidOperationException($"No document set for category '{cust.Category}'.");

                var doc = docSet.FirstOrDefault(d => string.Equals(d.Code, s.DocCode, StringComparison.OrdinalIgnoreCase));
                if (doc is null)
                    throw new InvalidOperationException($"DocCode '{s.DocCode}' not found in set for '{cust.Category}'.");

                var ext = Path.GetExtension(s.SourcePath);
                var fileName = NamingService.GenerateFileName(doc, cust, ext);
                r.FileName = fileName;

                var custRoot = MatchingService.FindCustomerRoot(destRoot, cust, cfg);
                if (custRoot is null)
                    throw new InvalidOperationException($"Customer root folder not found for '{cust.Name}' under '{destRoot}'.");

                var installFolderName = cfg.Dest.InstallDocsFolderName ?? "설치완료서류";
                var destDir = Path.Combine(custRoot, installFolderName);
                var destPath = Path.Combine(destDir, fileName);
                r.DestPath = destPath;

                if (!execute)
                {
                    r.Executed = false;
                    r.Success = true;
                    r.Message = "Preview OK";
                    results.Add(r);
                    continue;
                }

                Directory.CreateDirectory(destDir);

                var finalPath = EnsureUnique(destPath);
                File.Move(s.SourcePath, finalPath);

                r.DestPath = finalPath;
                r.Executed = true;
                r.Success = true;
                r.Message = "Moved";
            }
            catch (Exception ex)
            {
                r.Executed = execute;
                r.Success = false;
                r.Message = ex.Message;
            }

            results.Add(r);
        }

        return results;
    }

    private static string EnsureUnique(string path)
    {
        if (!File.Exists(path)) return path;

        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);

        int i = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            i++;
        } while (File.Exists(candidate));

        return candidate;
    }
}