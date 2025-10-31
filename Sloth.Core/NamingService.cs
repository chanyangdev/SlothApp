using System.Text.RegularExpressions;

namespace Sloth.Core
{
    public static class NamingService
    {
        public static string GetFileName(int no, string name, string? nameSuffix, string docType, string extension)
        {
            string safeName = MakeSafeFilePart(name + (nameSuffix ?? ""));
            string safeDoc = MakeSafeFilePart(docType);
            return $"{no:D2}. {safeName} {safeDoc}.{extension}";
        }

        private static string MakeSafeFilePart(string input)
        {
            var invalid = new string(System.IO.Path.GetInvalidFileNameChars());
            var pattern = $"[{Regex.Escape(invalid)}]";
            return Regex.Replace(input, pattern, "").Trim();
        }
    }
}