using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sloth.Core.Models
{
    /// <summary>
    /// Global configuration (document sets, matching rules, destination options, doc-code rules).
    /// </summary>
    public sealed class SlothConfig
    {
        [JsonPropertyName("documentSets")]
        public Dictionary<string, List<DocItem>> DocumentSets { get; set; } = new();

        // Keep JSON key "matching", but use a clear property name in code.
        [JsonPropertyName("matching")]
        public MatchingSettings MatchingSettings { get; set; } = new();

        // Keep JSON key "dest"
        [JsonPropertyName("dest")]
        public DestSettings DestSettings { get; set; } = new();

        [JsonPropertyName("docCodeRules")]
        public List<DocCodeRule>? DocCodeRules { get; set; }

        public sealed class DocItem
        {
            [JsonPropertyName("order")] public int Order { get; set; }
            [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
            [JsonPropertyName("pattern")] public string Pattern { get; set; } = string.Empty;
        }

        public sealed class MatchingSettings
        {
            [JsonPropertyName("folderNameFormats")] public List<string> FolderNameFormats { get; set; } = new();
            [JsonPropertyName("addressFallback")] public bool AddressFallback { get; set; } = true;
        }

        public sealed class DestSettings
        {
            [JsonPropertyName("installDocsFolderName")] public string InstallDocsFolderName { get; set; } = "설치완료서류";
        }

        public sealed class DocCodeRule
        {
            [JsonPropertyName("docCode")] public string DocCode { get; set; } = "";
            [JsonPropertyName("keywords")] public List<string>? Keywords { get; set; }
        }
    }
}