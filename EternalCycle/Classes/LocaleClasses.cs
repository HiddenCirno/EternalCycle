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

public class CustomQuestLocaleData
{
    [JsonPropertyName("name")]
    public string QuestName { get; set; }
    [JsonPropertyName("note")]
    public string QuestNote { get; set; }
    [JsonPropertyName("conditions")]
    public Dictionary<string, string> QuestConditions { get; set; }
    [JsonPropertyName("description")]
    public string QuestDescription { get; set; }
    [JsonPropertyName("startedMessageText")]
    public string QuestStartMessaage { get; set; }
    [JsonPropertyName("successMessageText")]
    public string QuestSuccessMessage { get; set; }
    [JsonPropertyName("failMessageText")]
    public string QuestFailMessage { get; set; }
    [JsonPropertyName("location")]
    public string QuestLocation { get; set; }
}
