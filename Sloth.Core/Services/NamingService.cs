using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Sloth.Core.Models;

namespace Sloth.Core.Services;

public static class NamingService
{
    // Generates something like: "03. 홍길동 현장점검표.pdf"
    // Supports tokens in pattern: {order}, {order:00}, {name}, {customerId}, {corp}, {category}
    public static string GenerateFileName(SlothConfig.DocItem doc, Customer c, string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) extension = ".pdf";
        if (!extension.StartsWith(".")) extension = "." + extension;

        string output = Regex.Replace(doc.Pattern, @"\{(?<key>\w+)(?::(?<fmt>[^}]+))?\}", m =>
        {
            var key = m.Groups["key"].Value.ToLowerInvariant();
            var fmt = m.Groups["fmt"].Success ? m.Groups["fmt"].Value : null;

            return key switch
            {
                "order"      => fmt is null ? doc.Order.ToString() : doc.Order.ToString(fmt),
                "name"       => c.Name ?? "",
                "customerid" => c.CustomerId ?? "",
                "corp"       => c.Corp ?? "",
                "category"   => c.Category ?? "",
                _            => m.Value // unknown token stays as-is
            };
        });

        // Clean/sanitize
        output = output.Trim();
        output = CollapseWhitespace(output);
        output = RemoveInvalidFileNameChars(output);
        output = TrimTrailingDotsAndSpaces(output);

        return output + extension;
    }

    private static string CollapseWhitespace(string s)
    {
        return Regex.Replace(s, @"\s{2,}", " ");
    }

    private static string RemoveInvalidFileNameChars(string name)
    {
        var bad = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
            sb.Append(bad.Contains(ch) ? '_' : ch);
        return sb.ToString();
    }

    private static string TrimTrailingDotsAndSpaces(string s)
    {
        return s.Trim().TrimEnd('.', ' ');
    }
}