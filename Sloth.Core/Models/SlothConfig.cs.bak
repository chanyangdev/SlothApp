using System.Collections.Generic;

namespace Sloth.Core.Services
{
    public class SlothConfig
    {
        public Dictionary<string, List<DocItem>> DocumentSets { get; set; } = new();
        public Matching Matching { get; set; } = new();
        public Dest Dest { get; set; } = new();

        public class DocItem
        {
            public int Order { get; set; }
            public string Code { get; set; } = "";
            public string Pattern { get; set; } = "";
        }

        public class Matching
        {
            public List<string> FolderNameFormats { get; set; } = new();
            public bool AddressFallback { get; set; } = true;
        }

        public class Dest
        {
            public string InstallDocsFolderName { get; set; } = "설치완료서류";
        }
    }
}
