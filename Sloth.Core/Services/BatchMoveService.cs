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

        // Build customer lookup (skip null/empty ids)
        var byId = customers
            .Where(c => !string.IsNullOrWhiteSpace(c.CustomerId))
            .ToDictionary(k => k.CustomerId!, k => k, StringComparer.OrdinalIgnoreCase);

        // Keep a planned-name set per directory to simulate uniqueness in Preview
        var plannedByDir = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

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

                var doc = docSet.FirstOrDefault(d =>
                    string.Equals(d.Code, s.DocCode, StringComparison.OrdinalIgnoreCase));
                if (doc is null)
                    throw new InvalidOperationException($"DocCode '{s.DocCode}' not found in set for '{cust.Category}'.");

                var ext = Path.GetExtension(s.SourcePath);

                // Build the target name ONCE
                var baseName = NamingService.Apply(doc.Pattern, cust, doc.Order);
                var fileName = baseName + ext;
                r.FileName = fileName;

                var custRoot = MatchingService.FindCustomerRoot(destRoot, cust, cfg);
                if (custRoot is null)
                    throw new InvalidOperationException($"Customer root folder not found for '{cust.Name}' under '{destRoot}'.");

                var installFolderName = cfg.DestSettings?.InstallDocsFolderName ?? "설치완료서류";
                var destDir = Path.Combine(custRoot, installFolderName);

                if (!execute)
                {
                    // Preview mode: simulate unique naming without touching disk
                    var plannedPath = EnsureUniquePlanned(destDir, fileName, plannedByDir);
                    r.DestPath = plannedPath ?? string.Empty; // coalesce to silence nullable warnings
                    r.Executed = false;
                    r.Success = true;
                    r.Message = "Preview OK";
                    results.Add(r);
                    continue;
                }

                // Execute move
                Directory.CreateDirectory(destDir);
                var desiredPath = Path.Combine(destDir, fileName);
                var finalPath = EnsureUnique(desiredPath);
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

    private static string EnsureUniquePlanned(
        string destDir,
        string fileName,
        Dictionary<string, HashSet<string>> plannedByDir)
    {
        if (!plannedByDir.TryGetValue(destDir, out var used))
        {
            used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (Directory.Exists(destDir))
                {
                    foreach (var f in Directory.EnumerateFiles(destDir))
                        used.Add(Path.GetFileName(f));
                }
            }
            catch { /* ignore */ }
            plannedByDir[destDir] = used;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var candidate = fileName;
        int i = 1;

        while (used.Contains(candidate))
        {
            candidate = $"{baseName} ({i}){ext}";
            i++;
        }

        used.Add(candidate);
        return Path.Combine(destDir, candidate);
    }
}