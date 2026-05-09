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



public record TraderBaseWithDesc : TraderBase
{
    [JsonPropertyName("_id")]
    [JsonConverter(typeof(MongoIdConverter))]
    public override required MongoId Id { get; set; }
    [JsonPropertyName("description")]
    public string Description { get; set; }
    [JsonPropertyName("insurance_locale")]
    public Dictionary<string, List<string>?>? Dialogue { get; init; }
    [JsonPropertyName("insuranceChance")]
    public int? InsuranceChance { get; set; }
    [JsonPropertyName("minReflashTime")]
    public int? ReflashMinTime {  get; set; }
    [JsonPropertyName("maxReflashTime")]
    public int? ReflashMaxTime { get; set; }
    [JsonPropertyName("showInRagfair")]
    public bool? ShowInRagfair { get; set; }
}

