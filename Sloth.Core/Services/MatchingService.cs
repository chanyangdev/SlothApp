using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sloth.Core.Models;

namespace Sloth.Core.Services;

public static class MatchingService
{
    /// <summary>
    /// Returns the customer's *root* folder (not the '설치완료서류' child).
    /// Strategy:
    ///   1) If Customer.FolderPath exists, use it.
    ///   2) Try exact candidates from config.Matching.FolderNameFormats under:
    ///        - destRoot
    ///        - destRoot/{Category}
    ///   3) Fallback: breadth-first search up to depth=3 for exact name matches.
    ///   4) If AddressFallback=true, allow matches where path contains Name AND
    ///      either RoadAddress or LotAddress (substring).
    /// Returns null if not found.
    /// </summary>
    public static string? FindCustomerRoot(string destRoot, Customer c, SlothConfig cfg, int maxDepth = 3)
    {
        if (!Directory.Exists(destRoot))
            return null;

        // (1)
        if (!string.IsNullOrWhiteSpace(c.FolderPath) && Directory.Exists(c.FolderPath))
            return c.FolderPath;

        var names = ExpandFormats(cfg.Matching.FolderNameFormats, c);
        var candidates = new List<string>();

        // category may be "주택" or "건물" etc.
        var catRoot = !string.IsNullOrWhiteSpace(c.Category) ? Path.Combine(destRoot, c.Category) : null;

        foreach (var n in names)
        {
            candidates.Add(Path.Combine(destRoot, n));
            if (catRoot is not null) candidates.Add(Path.Combine(catRoot, n));
        }

        // (2) direct check
        foreach (var p in candidates)
            if (Directory.Exists(p)) return p;

        // (3) BFS search up to depth
        var exact = BfsDirs(destRoot, maxDepth)
            .FirstOrDefault(d => names.Contains(Path.GetFileName(d), StringComparer.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        // (4) address fallback
        if (cfg.Matching.AddressFallback)
        {
            var road = (c.RoadAddress ?? "").Trim();
            var lot  = (c.LotAddress  ?? "").Trim();
            foreach (var d in BfsDirs(destRoot, maxDepth))
            {
                var nameHit = d.IndexOf(c.Name ?? "", StringComparison.OrdinalIgnoreCase) >= 0;
                var roadHit = !string.IsNullOrEmpty(road) && d.IndexOf(road, StringComparison.OrdinalIgnoreCase) >= 0;
                var lotHit  = !string.IsNullOrEmpty(lot)  && d.IndexOf(lot,  StringComparison.OrdinalIgnoreCase) >= 0;
                if (nameHit && (roadHit || lotHit)) return d;
            }
        }

        return null;
    }

    private static IEnumerable<string> BfsDirs(string root, int maxDepth)
    {
        var q = new Queue<(string path, int depth)>();
        q.Enqueue((root, 0));
        while (q.Count > 0)
        {
            var (p, d) = q.Dequeue();
            if (d >= maxDepth) continue;

            IEnumerable<string> subs = Array.Empty<string>();
            try { subs = Directory.EnumerateDirectories(p); } catch { /* ignore */ }

            foreach (var s in subs)
            {
                yield return s;
                q.Enqueue((s, d + 1));
            }
        }
    }

    private static List<string> ExpandFormats(IEnumerable<string> formats, Customer c)
    {
        var list = new List<string>();
        foreach (var f in formats)
        {
            if (string.IsNullOrWhiteSpace(f)) continue;
            var n = f
                .Replace("{name}", c.Name ?? "")
                .Replace("{customerId}", c.CustomerId ?? "")
                .Replace("{corp}", c.Corp ?? "")
                .Replace("{category}", c.Category ?? "");
            list.Add(n);
        }
        return list;
    }
}