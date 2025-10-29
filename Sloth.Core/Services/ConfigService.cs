using System.IO;
using System.Text.Json;

namespace Sloth.Core.Services
{
    public static class ConfigService
    {
        public static SlothConfig Load(string path)
        {
            var json = File.ReadAllText(path);
            var cfg = JsonSerializer.Deserialize<SlothConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return cfg ?? new SlothConfig();
        }
    }
}
