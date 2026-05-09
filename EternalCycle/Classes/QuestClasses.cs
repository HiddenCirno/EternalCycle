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
using SPTarkov.Server.Core.Models.Eft.Health;
using System.Diagnostics.Metrics;

namespace VulcanCore;

public class CustomQuest
{
    [JsonPropertyName("ID")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId QuestId { get; set; }
    [JsonPropertyName("Type")]
    public int QuestType { get; set; }
    [JsonPropertyName("ImagePath")]
    public string QuestImagePath { get; set; }
    [JsonPropertyName("TraderID")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId QuestTraderId { get; set; }
    [JsonPropertyName("Restartable")]
    public bool IsRestartableQuest { get; set; }
    [JsonPropertyName("Location")]
    public string Location { get; set; }
    [JsonPropertyName("QuestData")]
    public CustomQuestConditionsData QuestConditions { get; set; }
    [JsonPropertyName("QuestReward")]
    public List<CustomQuestRewardData> QuestRewards { get; set; }

}
public class CustomQuestConditionsData
{
    [JsonPropertyName("Finish")]
    public List<CustomQuestData> QuestFinishData { get; set; }
    [JsonPropertyName("Failed")]
    public List<CustomQuestData> QuestFailedData { get; set; }
}


[JsonDerivedType(typeof(CustomQuestData), "base")]
[JsonDerivedType(typeof(FindItemData), "find")]
[JsonDerivedType(typeof(FindItemGroupData), "findgroup")]
[JsonDerivedType(typeof(HandoverItemData), "hand")]
[JsonDerivedType(typeof(HandoverItemGroupData), "handgroup")]
[JsonDerivedType(typeof(KillTargetData), "kill")]
[JsonDerivedType(typeof(ReachLevelData), "level")]
[JsonDerivedType(typeof(VisitPlaceData), "visit")]
[JsonDerivedType(typeof(PlaceItemData), "place")]
[JsonDerivedType(typeof(PlaceItemGroupData), "placegroup")]
[JsonDerivedType(typeof(ExitLocationData), "exit")]
[JsonDerivedType(typeof(ReachSkillLevelData), "skill")]
[JsonDerivedType(typeof(ReachTraderTrustLevelData), "trust")]
[JsonDerivedType(typeof(ReachTraderStandingData), "standing")]
[JsonDerivedType(typeof(CompleteQuestData), "quest")]
[JsonDerivedType(typeof(CustomizationBlockData), "block")]
[JsonDerivedType(typeof(ReachPrestigeLevelData), "prestige")]

public class CustomQuestData
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId Id { get; set; }
    [JsonPropertyName("locale")]
    public string? Locale { get; set; }
    [JsonExtensionData]
    public Dictionary<string?, object?>? ExtensionData => _extensionData;

    [JsonIgnore]
    private readonly Dictionary<string?, object?>? _extensionData = new Dictionary<string, object>();
}

public class FindItemData : CustomQuestData
{
    [JsonPropertyName("inraid")]
    public bool FindInRaid { get; set; }
    [JsonPropertyName("itemid")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId ItemId { get; set; }
    [JsonPropertyName("count")]
    public int Count { get; set; }
    [JsonPropertyName("autolocale")]
    public bool? AutoLocale { get; set; }
}
public class FindItemGroupData : CustomQuestData
{
    [JsonPropertyName("inraid")]
    public bool FindInRaid { get; set; }
    [JsonPropertyName("itemgroup")]
    public List<string> Items { get; set; }
    [JsonPropertyName("count")]
    public int Count { get; set; }
}
public class HandoverItemData : CustomQuestData
{
    [JsonPropertyName("inraid")]
    public bool FindInRaid { get; set; }
    [JsonPropertyName("itemid")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId ItemId { get; set; }
    [JsonPropertyName("count")]
    public int Count { get; set; }
    [JsonPropertyName("autolocale")]
    public bool? AutoLocale { get; set; }
}
public class HandoverItemGroupData : CustomQuestData
{
    [JsonPropertyName("inraid")]
    public bool FindInRaid { get; set; }
    [JsonPropertyName("itemgroup")]
    public List<string> Items { get; set; }
    [JsonPropertyName("count")]
    public int Count { get; set; }
}
public class KillTargetData : CustomQuestData
{
    //TODO 
    //locationºÍbodyÎ»Í¼
    [JsonPropertyName("oneraid")]
    public bool CompleteInOneRaid { get; set; }
    [JsonPropertyName("count")]
    public int Count { get; set; }
    [JsonPropertyName("bot")]
    public string BotType { get; set; }
    [JsonPropertyName("role")]
    public List<string> BotRole { get; set; }
    [JsonPropertyName("bodyPart")]
    public int BodyPart { get; set; }
    [JsonPropertyName("daytime")]
    public int[] DayTime { get; set; } = new int[2];
    [JsonPropertyName("distance")]
    public int Distance { get; set; }
    [JsonPropertyName("distancetype")]
    public int DistanceType { get; set; }
    [JsonPropertyName("weapon")]
    public List<string> WeaponList { get; set; }
    [JsonPropertyName("mod")]
    public List<List<string>> ModList { get; set; }
    [JsonPropertyName("location")]
    public int Location { get; set; }
    [JsonPropertyName("zone")]
    public List<string> ZoneList { get; set; }
    [JsonPropertyName("equip")]
    public List<List<string>> EquipmentList { get; set; }
    [JsonPropertyName("enemyequip")]
    public List<List<string>> EnemyEquipmentList { get; set; }

}
public class ReachLevelData : CustomQuestData
{
    [JsonPropertyName("count")]
    public int Count { get; set; }
}
public class ReachPrestigeLevelData : CustomQuestData
{
    [JsonPropertyName("type")]
    public int CompareType { get; set; }
    [JsonPropertyName("level")]
    public int Level { get; set; }
}

public class VisitPlaceData : CustomQuestData
{
    [JsonPropertyName("oneraid")]
    public bool CompleteInOneRaid { get; set; }
    [JsonPropertyName("zoneid")]
    public string ZoneId { get; set; }
}
public class PlaceItemData : CustomQuestData
{
    [JsonPropertyName("time")]
    public int Time { get; set; }
    [JsonPropertyName("itemid")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId ItemId { get; set; }
    [JsonPropertyName("zoneid")]
    public string ZoneId { get; set; }
    [JsonPropertyName("count")]
    public int Count { get; set; }
}
public class PlaceItemGroupData : CustomQuestData
{
    [JsonPropertyName("time")]
    public int Time { get; set; }
    [JsonPropertyName("itemgroup")]
    public List<string> Items { get; set; }
    [JsonPropertyName("zoneid")]
    public string ZoneId { get; set; }
    [JsonPropertyName("count")]
    public int Count { get; set; }
}
public class ExitLocationData : CustomQuestData
{
    [JsonPropertyName("oneraid")]
    public bool CompleteInOneRaid { get; set; }
    [JsonPropertyName("count")]
    public int Count { get; set; }
    [JsonPropertyName("status")]
    public int ExitStatus { get; set; }
    [JsonPropertyName("location")]
    public int Locations { get; set; }
    [JsonPropertyName("chooseexitpoint")]
    public bool ChooseExitPoint { get; set; }
    [JsonPropertyName("exitpoint")]
    public string ExitPoint { get; set; }

}

public class ReachSkillLevelData : CustomQuestData
{
    [JsonPropertyName("skill")]
    public string SkillType { get; set; }
    [JsonPropertyName("level")]
    public int Level { get; set; }
}
public class ReachTraderTrustLevelData : CustomQuestData
{
    [JsonPropertyName("traderid")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId TraderId { get; set; }
    [JsonPropertyName("level")]
    public int TrustLevel { get; set; }
}
public class ReachTraderStandingData : CustomQuestData
{
    [JsonPropertyName("traderid")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId TraderId { get; set; }
    [JsonPropertyName("standing")]
    public double TrustStanding { get; set; }
}
public class CompleteQuestData : CustomQuestData
{
    [JsonPropertyName("questid")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId QuestId { get; set; }
    [JsonPropertyName("status")]
    public int QuestStatus { get; set; }
}
public class CustomizationBlockData : CustomQuestData
{

}
[JsonDerivedType(typeof(CustomQuestRewardData), "base")]
[JsonDerivedType(typeof(CustomItemRewardData), "item")]
[JsonDerivedType(typeof(CustomAssortUnlockRewardData), "assort")]
[JsonDerivedType(typeof(CustomExperienceRewardData), "experience")]
[JsonDerivedType(typeof(CustomSkillExperienceRewardData), "skillexperience")]
[JsonDerivedType(typeof(CustomTraderStandingRewardData), "standing")]
[JsonDerivedType(typeof(CustomTraderUnlockRewardData), "trader")]
[JsonDerivedType(typeof(CustomCustomizationRewardData), "customization")]
[JsonDerivedType(typeof(CustomAchievementRewardData), "achievement")]
[JsonDerivedType(typeof(CustomPocketRewardData), "pocket")]

public class CustomQuestRewardData
{
    [JsonPropertyName("ID")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId Id { get; set; }
    [JsonPropertyName("Quest")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId QuestId { get; set; }
    [JsonPropertyName("Stage")]
    public int QuestStage { get; set; }
    [JsonPropertyName("Unknown")]
    public bool IsUnknownReward { get; set; }
    [JsonPropertyName("Hidden")]
    public bool IsHiddenReward { get; set; }
    [JsonPropertyName("IsAchievement")]
    public bool IsAchievement { get; set; }
    [JsonPropertyName("AvailableGameEdition")]
    public int? AvailableGameEdition { get; set; }
    [JsonExtensionData]
    public Dictionary<string?, object?>? ExtensionData => _extensionData;

    [JsonIgnore]
    private readonly Dictionary<string?, object?>? _extensionData = new Dictionary<string, object>();
}

public class CustomItemRewardData : CustomQuestRewardData
{
    [JsonPropertyName("Items")]
    public List<CustomItem> Items { get; set; }
    [JsonPropertyName("Count")]
    public int Count { get; set; }
    [JsonPropertyName("FindInRaid")]
    public bool FindInRaid { get; set; }
}
public class CustomAssortUnlockRewardData : CustomQuestRewardData
{
    public CustomLockedAssortData AssortData { get; set; }
}
public class CustomRecipeUnlockRewardData : CustomQuestRewardData
{
    public CustomLockedRecipeData RecipeData { get; set; }
}

public class CustomExperienceRewardData : CustomQuestRewardData
{
    [JsonPropertyName("Count")]
    public int Count { get; set; }
}
public class CustomSkillExperienceRewardData : CustomQuestRewardData
{
    [JsonPropertyName("Skill")]
    public string SkillType { get; set; }
    [JsonPropertyName("Count")]
    public int Count { get; set; }
}
public class CustomTraderStandingRewardData : CustomQuestRewardData
{
    [JsonPropertyName("TraderID")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId TraderId { get; set; }
    [JsonPropertyName("Count")]
    public double Count { get; set; }
}

public class CustomTraderUnlockRewardData : CustomQuestRewardData
{
    [JsonPropertyName("Trader")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId TraderId { get; set; }
}
public class CustomCustomizationRewardData : CustomQuestRewardData
{
    [JsonPropertyName("Target")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId TargetId { get; set; }
}
public class CustomAchievementRewardData : CustomQuestRewardData
{
    [JsonPropertyName("Target")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId TargetId { get; set; }
}
public class CustomPocketRewardData : CustomQuestRewardData
{
    [JsonPropertyName("Target")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId TargetId { get; set; }
}
public class QuestLogicTree
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(MongoIdConverter))]
    public MongoId Id { get; set; }
    [JsonPropertyName("prequestdata")]
    public Dictionary<string, int> PreQuestData { get; set; }
    [JsonPropertyName("pretraderstanding")]
    public Dictionary<string, double> PreTraderStandingData { get; set; }
    [JsonPropertyName("pretraderlevel")]
    public Dictionary<string, int> PreTraderTrustLevelData { get; set; }
    [JsonPropertyName("prelevel")]
    public int PrePlayerLevel { get; set; }
    [JsonPropertyName("prestigelevel")]
    public int? PrePlayerPrestigeLevel { get; set; }
    [JsonPropertyName("prestigetype")]
    public int? PrestigeCompareType { get; set; }
}