using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using System.Text.Json;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Buffers;
using System.Text.Json.Serialization;
using static VulcanCore.VulcanUtil;
using SPTarkov.Server.Core.Models.Enums.Hideout;

namespace VulcanCore;
[JsonDerivedType(typeof(CustomNormalRecipeData), "normal")]
[JsonDerivedType(typeof(CustomLockedRecipeData), "locked")]
public class CustomRecipeData
{
    [JsonPropertyName("ID")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId Id { get; set; }
    [JsonPropertyName("Area")]
    public HideoutAreas AreaType { get; set; }
    [JsonPropertyName("AreaLevel")]
    public int AreaLevel { get; set; }
    [JsonPropertyName("Output")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId Output { get; set; }
    [JsonPropertyName("IsEncoded")]
    public bool? IsEncoded { get; set; }
    [JsonPropertyName("OutputCount")]
    public int OutputCount { get; set; }
    [JsonPropertyName("Time")]
    public int Time { get; set; }
    [JsonPropertyName("NeedFuel")]
    public bool NeedFuel { get; set; }
    [JsonPropertyName("Require")]
    public CustomRecipeRequire Require { get; set; }
}
public class CustomRecipeRequire
{
    [JsonPropertyName("Tool")]
    public Dictionary<string, int> ToolsRequire { get; set; }
    [JsonPropertyName("Item")]
    public Dictionary<string, int> ItemsRequire { get; set; }
}
public class CustomNormalRecipeData : CustomRecipeData
{

}
public class CustomLockedRecipeData : CustomRecipeData
{
    [JsonPropertyName("Locked")]
    public bool Locked { get; set; }
    [JsonPropertyName("Quest")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId QuestId { get; set; }
    [JsonPropertyName("QuestStage")]
    public int QuestStage { get; set; }

    [JsonPropertyName("Unknown")]
    public bool IsUnknownReward { get; set; }
}
public class CustomScavCaseRecipeData
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("time")]
    public int Time {  set; get; }
    [JsonPropertyName("requires")]
    public Dictionary<string, int> Requirement { get; set; }
    [JsonPropertyName("rewards")]
    public CustomScavCaseRecipeReward Reward { get; set; }
}
public class CustomScavCaseRecipeReward
{
    [JsonPropertyName("common")]
    public List<int> Common { get; set; }
    [JsonPropertyName("rare")]
    public List<int> Rare { get; set; }
    [JsonPropertyName("superrare")]
    public List<int> SuperRare { get; set; }
}