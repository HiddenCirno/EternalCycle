using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Logger;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VulcanCore;


public class ConfigManager
{
    public static string modPath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    public static string configJsoncContent = File.ReadAllText(System.IO.Path.Combine(modPath, "config.jsonc"));
    public static ConfigClass GetConfig()
    {
        return JsonSerializer.Deserialize<ConfigClass>(configJsoncContent, new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip // ∆Ù”√◊¢ ÕΩ‚Œˆ
        });
    }
    public class ConfigClass
    {
        [JsonPropertyName("UseOldRagfairPrice")]
        public bool UseOldRagfairPrice { get; set; }
    }
}






