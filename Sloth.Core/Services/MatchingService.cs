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

        var formats = cfg.MatchingSettings?.FolderNameFormats ?? new List<string>();
        var idDigits = Regex.Replace(cust.CustomerId ?? "", "[^0-9]", "");

        string Cat(string name) => Path.Combine(destRoot, cust.Category ?? string.Empty, name);

        // Try with category folder
        foreach (var fmt in formats)
        {
            var name = fmt
                .Replace("{name}", cust.Name ?? "")
                .Replace("{customerId}", cust.CustomerId ?? "")
                .Replace("{idDigits}", idDigits)
                .Replace("{no}", cust.No.ToString());

            var candidate = Cat(name);
            if (Directory.Exists(candidate))
                return candidate;
        }

        // Try directly under destRoot
        foreach (var fmt in formats)
        {
            var name = fmt
                .Replace("{name}", cust.Name ?? "")
                .Replace("{customerId}", cust.CustomerId ?? "")
                .Replace("{idDigits}", idDigits)
                .Replace("{no}", cust.No.ToString());

            var candidate = Path.Combine(destRoot, name);
            if (Directory.Exists(candidate))
                return candidate;
        }

        if (cfg.MatchingSettings?.AddressFallback == true)
        {
            try
            {
                var level1 = Directory.EnumerateDirectories(destRoot);
                var byName = level1.FirstOrDefault(d =>
                    Path.GetFileName(d).Contains(cust.Name ?? "", StringComparison.OrdinalIgnoreCase));
                if (byName != null) return byName;
            }
            catch { /* ignore */ }
        }

        return null;
    }
}