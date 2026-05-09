using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using System.Text.Json;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Buffers;
using System.Text.Json.Serialization;

namespace VulcanCore;

public class CustomPresetData
{
    [JsonPropertyName("Name")]
    public string Name { get; set; }
    [JsonPropertyName("PresetName")]
    public string PresetName { get; set; }
    [JsonPropertyName("IsBasePreset")]
    public bool IsBasePreset { get; set; }
    [JsonPropertyName("ChangePresetName")]
    public bool ChangePresetName { get; set; }
    [JsonPropertyName("SpawnInRaid")]
    public bool SpawnInRaid { get; set; }
    [JsonPropertyName("SpawnTarget")]
    public MongoId SpawnTarget {  get; set; }
    [JsonPropertyName("Preset")]
    public List<CustomItem> PresetData { get; set; }
}
