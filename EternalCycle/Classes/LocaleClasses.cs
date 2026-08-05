using System.Text.Json.Serialization;

namespace EternalCycleServer;

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
