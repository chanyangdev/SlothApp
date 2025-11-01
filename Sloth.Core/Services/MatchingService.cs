using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Sloth.Core.Models;

namespace Sloth.Core.Services;

public static class MatchingService
{
    public static string? FindCustomerRoot(string destRoot, Customer cust, SlothConfig cfg)
    {
        if (!string.IsNullOrWhiteSpace(cust.FolderPath) && Directory.Exists(cust.FolderPath))
            return cust.FolderPath;

        if (string.IsNullOrWhiteSpace(destRoot) || !Directory.Exists(destRoot))
            return null;

        var settings = cfg.MatchingSettings ?? new SlothConfig.MatchingSettings();
        var formats = settings.FolderNameFormats ?? new List<string>();
        var idDigits = Regex.Replace(cust.CustomerId ?? "", "[^0-9]", "");
        var category = cust.Category ?? string.Empty;
        var corp = cust.Corp ?? string.Empty;

        // Build candidate folder names from formats
        // Allowed tokens: {name} {customerId} {idDigits} {no} {category} {corp}
        var names = BuildNames(formats, cust, idDigits, category, corp).ToList();

        // Where to try first: category folder then root
        IEnumerable<string> roots = string.IsNullOrWhiteSpace(category)
            ? new[] { destRoot }
            : new[] { Path.Combine(destRoot, category), destRoot };

        // 1) Exact path exists?
        foreach (var r in roots)
        {
            foreach (var n in names)
            {
                var candidate = Path.Combine(r, n);
                if (Directory.Exists(candidate))
                    return candidate;
            }
        }

        // 2) Fallback: shallow scan for exact folder-name equality, then Contains
        if (settings.AddressFallback)
        {
            foreach (var r in roots)
            {
                var got = ScanLevel(r, names, exact: true);
                if (got is not null) return got;
            }
            foreach (var r in roots)
            {
                var got = ScanLevel(r, names, exact: false);
                if (got is not null) return got;
            }

            // 3) Optional deeper scan up to maxSearchDepth
            var depth = Math.Max(0, settings.MaxSearchDepth);
            if (depth > 1)
            {
                foreach (var r in roots)
                {
                    var got = ScanDeep(r, names, depth);
                    if (got is not null) return got;
                }
            }

            // 4) Last-ditch: name-only contains in first level (robust for simple setups)
            try
            {
                foreach (var r in roots)
                {
                    foreach (var d in Directory.EnumerateDirectories(r))
                    {
                        if (Path.GetFileName(d)
                                .Contains(cust.Name ?? "", StringComparison.OrdinalIgnoreCase))
                            return d;
                    }
                }
            }
            catch { /* ignore */ }
        }

        return null;
    }

    private static IEnumerable<string> BuildNames(
        IEnumerable<string> formats, Customer c, string idDigits, string category, string corp)
    {
        // If no formats configured, use a sensible default set
        var fmts = (formats?.Any() == true)
            ? formats!
            : new[]
              {
                  "{name}",
                  "{idDigits}-{name}",
                  "{no}-{name}",
                  "{corp}-{name}",
              };

        foreach (var fmt in fmts)
        {
            var name = fmt
                .Replace("{name}", c.Name ?? "")
                .Replace("{customerId}", c.CustomerId ?? "")
                .Replace("{idDigits}", idDigits)
                .Replace("{no}", c.No.ToString())
                .Replace("{category}", category)
                .Replace("{corp}", corp);

            name = SanitizeSegment(name);
            if (!string.IsNullOrWhiteSpace(name))
                yield return name;
        }
    }

    private static string SanitizeSegment(string s)
    {
        // Remove invalid file-name chars and collapse whitespace
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var ch in invalid)
            s = s.Replace(ch.ToString(), " ");

        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }

    private static string? ScanLevel(string root, List<string> names, bool exact)
    {
        try
        {
            if (!Directory.Exists(root)) return null;
            var dirs = Directory.EnumerateDirectories(root);

            if (exact)
            {
                // Exact equals ignoring case
                var set = new HashSet<string>(dirs.Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);
                foreach (var n in names)
                    if (set.Contains(n))
                        return Path.Combine(root, n);
            }
            else
            {
                // Contains
                foreach (var d in dirs)
                {
                    var leaf = Path.GetFileName(d) ?? "";
                    if (names.Any(n => leaf.Contains(n, StringComparison.OrdinalIgnoreCase)))
                        return d;
                }
            }
        }
        catch { /* ignore permission issues */ }

        return null;
    }

    private static string? ScanDeep(string root, List<string> names, int maxDepth)
    {
        try
        {
            if (!Directory.Exists(root)) return null;

            var queue = new Queue<(string path, int depth)>();
            queue.Enqueue((root, 0));

            while (queue.Count > 0)
            {
                var (cur, d) = queue.Dequeue();
                if (d >= maxDepth) continue;

                IEnumerable<string> children;
                try { children = Directory.EnumerateDirectories(cur); }
                catch { continue; }

                foreach (var c in children)
                {
                    var leaf = Path.GetFileName(c) ?? "";
                    if (names.Any(n => leaf.Equals(n, StringComparison.OrdinalIgnoreCase) ||
                                       leaf.Contains(n, StringComparison.OrdinalIgnoreCase)))
                        return c;

                    queue.Enqueue((c, d + 1));
                }
            }
        }
        catch { /* ignore */ }

        return null;
    }
}