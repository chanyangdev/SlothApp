using System;
using System.Linq;
using Sloth.Core.Models;

namespace Sloth.Core.Services
{
    public static class DocumentClassifierService
    {
        /// <summary>Return a docCode based on filename keywords. Null if no match.</summary>
        public static string? GuessDocCode(string fileName, SlothConfig cfg)
        {
            if (cfg.DocCodeRules is null || cfg.DocCodeRules.Count == 0) return null;
            var lower = fileName.ToLowerInvariant();

            foreach (var r in cfg.DocCodeRules)
            {
                if (string.IsNullOrWhiteSpace(r.DocCode) || r.Keywords is null || r.Keywords.Count == 0)
                    continue;

                if (r.Keywords.Any(k => lower.Contains(k.ToLowerInvariant())))
                    return r.DocCode;
            }
            return null;
        }
    }
}