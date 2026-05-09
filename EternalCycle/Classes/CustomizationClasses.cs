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
using static VulcanCore.VulcanUtil;

namespace VulcanCore;

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

    [JsonPropertyName("Body")]
    [JsonConverter(typeof(MongoIdConverter))]
    public override MongoId? Body { get; set; }
    [JsonPropertyName("Feet")]
    [JsonConverter(typeof(MongoIdConverter))]
    public override MongoId? Feet { get; set; }
    [JsonPropertyName("Hands")]
    [JsonConverter(typeof(MongoIdConverter))]
    public override MongoId? Hands { get; set; }
    [JsonPropertyName("BearTemplateId")]
    [JsonConverter(typeof(MongoIdConverter))]
    public override MongoId? BearTemplateId { get; set; }
    [JsonPropertyName("UsecTemplateId")]
    [JsonConverter(typeof(MongoIdConverter))]
    public override MongoId? UsecTemplateId { get; set; }
    [JsonPropertyName("HideGarbage")]
    public override bool? HideGarbage { get; set; }
    [JsonPropertyName("IsVoice")]
    public bool IsVoice { get; set; }
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