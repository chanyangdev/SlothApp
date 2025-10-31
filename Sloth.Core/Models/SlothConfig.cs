using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sloth.Core.Models
{
    public class SlothConfig
    {
        [JsonPropertyName("documentSets")]
        public Dictionary<string, List<DocItem>> DocumentSets { get; set; } = new();

        [JsonPropertyName("matching")]
        public Matching MatchingSettings { get; set; } = new();

        [JsonPropertyName("dest")]
        public Dest DestSettings { get; set; } = new();

        public sealed class DocItem
        {
            [JsonPropertyName("order")]
            public int Order { get; set; }

            [JsonPropertyName("code")]
            public string Code { get; set; } = string.Empty;

            [JsonPropertyName("pattern")]
            public string Pattern { get; set; } = string.Empty;
        }

        public sealed class Matching
        {
            [JsonPropertyName("folderNameFormats")]
            public List<string> FolderNameFormats { get; set; } = new();

            [JsonPropertyName("addressFallback")]
            public bool AddressFallback { get; set; } = true;
        }

        public sealed class Dest
        {
            [JsonPropertyName("installDocsFolderName")]
            public string InstallDocsFolderName { get; set; } = "설치완료서류";
        }
    }
}