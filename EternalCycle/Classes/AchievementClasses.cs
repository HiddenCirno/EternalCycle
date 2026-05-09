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
public class CustomAchievementData //: Achievement
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId Id { get; set; }
    [JsonPropertyName("img")]
    public string ImagePath { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("description")]
    public string Description { get; set; }
    [JsonPropertyName("rarity")]
    public string Rarity { get; set; }
    [JsonPropertyName("side")]
    public string Side { get; set; }
    [JsonPropertyName("instantComplete")]
    public bool InstantComplete { get; set; }
    [JsonPropertyName("showNotificationsInGame")]
    public bool ShowNotificationsInGame { get; set; }
    [JsonPropertyName("showProgress")]   
    public bool ShowProgress { get; set; }
    [JsonPropertyName("hidden")]
    public bool IsHidden { get; set; }
    [JsonPropertyName("showConditions")]
    public bool ShowConditions { get; set; }
    [JsonPropertyName("progressBarEnabled")]
    public bool ProgressBarEnabled { get; set; }
    [JsonPropertyName("conditions")]
    public CustomAchievementConditionsData Conditions { get; set; }
    [JsonPropertyName("rewards")]
    public List<CustomQuestRewardData> AchievementRewards { get; set; }
}
public class CustomAchievementConditionsData
{
    [JsonPropertyName("finish")]
    public List<CustomQuestData> AchievementFinishData { get; set; }
    [JsonPropertyName("failed")]
    public List<CustomQuestData> AchievementFailedData { get; set; }
}