using System.IO;
using System.Text.Json;
using Sloth.Core.Models;

namespace Sloth.Core.Services;

public static class ConfigService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static SlothConfig Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SlothConfig>(json, JsonOpts) ?? new SlothConfig();
    }

    public static void Save(string path, SlothConfig cfg)
    {
        var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions(JsonOpts)
        {
            WriteIndented = true
        });
        File.WriteAllText(path, json);
    }
}