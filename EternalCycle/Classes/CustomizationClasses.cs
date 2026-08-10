using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using System.Text.Json;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Buffers;
using System.Text.Json.Serialization;
using static EternalCycleServer.Utils;

namespace EternalCycleServer;

public record CustomCustomizationItem
{
    [JsonPropertyName("_id")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId Id { get; set; }
    [JsonPropertyName("_name")]
    public string Name { get; set; }
    [JsonPropertyName("_parent")]
    public string ParentId { get; set; }
    [JsonPropertyName("_type")]
    public string Type { get; set; }
    [JsonPropertyName("_props")]
    public CustomCustomizationProperties Properties { get; set; }
    [JsonPropertyName("_proto")]
    public string Proto { get; set; }
}
public class CustomCustomizationProperties : CustomizationProperties
{
    [JsonPropertyName("IsVoice")]
    public bool? IsVoice { get; set; }

    [JsonPropertyName("VoicePath")]
    public string? VoicePath { get; set; }

    [JsonPropertyName("IsDeco")]
    public bool? IsDeco { get; set; }

    [JsonPropertyName("IsTarget")]
    public bool? IsTarget { get; set; }
}
public class CustomHideoutCustomization
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId Id { get; set; }
    [JsonPropertyName("conditions")]
    public List<CustomQuestData> Conditions { get; set; }
    [JsonPropertyName("type")]
    public string Type { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("shortname")]
    public string ShortName { get; set; }
    [JsonPropertyName("description")]
    public string Description { get; set; }
    [JsonPropertyName("enbale")]
    public bool IsEnable { get; set; }
    [JsonPropertyName("target")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId Target {  get; set; }
}