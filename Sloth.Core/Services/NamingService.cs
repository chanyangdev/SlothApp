using System.Text.RegularExpressions;
using Sloth.Core.Models;

namespace Sloth.Core.Services
{
    /// <summary>
    /// Expands filename patterns with tokens for a given customer & doc order.
    /// Supports:
    ///  {order}, {order:00}, {no}, {no:000}, {name}, {customerId}, {idDigits}, {idDigits:000}
    /// </summary>
    public static class NamingService
    {
        private static string DigitsOnly(string? s) =>
            string.IsNullOrEmpty(s) ? string.Empty : Regex.Replace(s, "[^0-9]", "");

        private static string ReplaceIntToken(string input, string token, int value)
        {
            // {token} or {token:format}
            return Regex.Replace(input, $@"\{{{token}(?::([^}}]+))?\}}", m =>
            {
                var fmt = m.Groups[1].Success ? m.Groups[1].Value : null;
                return fmt is null ? value.ToString() : value.ToString(fmt);
            });
        }

        private static string ReplaceStringToken(string input, string token, string value)
        {
            // {token} or {token:format} (format ignored for strings)
            return Regex.Replace(input, $@"\{{{token}(?::[^}}]+)?\}}", value ?? string.Empty);
        }

        /// <summary>Return the final base name (without extension) for a pattern.</summary>
        public static string Apply(string pattern, Customer cust, int order)
        {
            var result = pattern;

            // integers
            result = ReplaceIntToken(result, "order", order);
            result = ReplaceIntToken(result, "no", cust.No);

            // strings
            result = ReplaceStringToken(result, "name", cust.Name ?? "");
            result = ReplaceStringToken(result, "customerId", cust.CustomerId ?? "");

            // digits-only of CustomerId with optional padding
            var idDigits = DigitsOnly(cust.CustomerId);
            result = Regex.Replace(result, @"\{idDigits(?::([^}]+))?\}", m =>
            {
                if (!m.Groups[1].Success) return idDigits;
                return int.TryParse(idDigits, out var n) ? n.ToString(m.Groups[1].Value) : idDigits;
            });

            return result.Trim();
        }
    }
}