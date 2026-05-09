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

[JsonDerivedType(typeof(CustomNormalAssortData), "normal")]
[JsonDerivedType(typeof(CustomLockedAssortData), "locked")]
public class CustomAssortData
{
    [JsonPropertyName("ID")]
    public string Id { get; set; }

    [JsonPropertyName("Trader")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId Trader { get; set; }

    [JsonPropertyName("Item")]
    public List<CustomItem> Item { get; set; }

    [JsonPropertyName("DogTag")]
    public Dictionary<string, CustomDogTag> DogTag { get; set; }

    [JsonPropertyName("Barter")]
    public Dictionary<string, double> Barter { get; set; }

    [JsonPropertyName("TrustLevel")]
    public int TrustLevel { get; set; }

    [JsonPropertyName("isWeapon")]
    public bool? IsWeapon { get; set; }

}

public class CustomNormalAssortData: CustomAssortData
{

}

public class CustomLockedAssortData : CustomAssortData
{
    [JsonPropertyName("Locked")]
    public bool? Locked { get; set; }

    [JsonPropertyName("Quest")]
    public string QuestId { get; set; }

    [JsonPropertyName("QuestStage")]
    public int QuestStage { get; set; }

    [JsonPropertyName("Unknown")]
    public bool IsUnknownReward { get; set; }
}
