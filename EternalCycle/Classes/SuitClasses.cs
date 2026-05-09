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
public class CustomSuit
{
    [JsonPropertyName("_id")]
    [JsonConverter(typeof(MongoIdConverter))]
    public virtual MongoId Id { get; set; }

    [JsonPropertyName("externalObtain")]
    public virtual bool? ExternalObtain { get; set; }

    [JsonPropertyName("internalObtain")]
    public virtual bool? InternalObtain { get; set; }

    [JsonPropertyName("isHiddenInPVE")]
    public virtual bool? IsHiddenInPVE { get; set; }

    [JsonPropertyName("tid")]
    [JsonConverter(typeof(MongoIdConverter))]
    public virtual MongoId Tid { get; set; }

    [JsonPropertyName("suiteId")]
    [JsonConverter(typeof(MongoIdConverter))]
    public virtual MongoId SuiteId { get; set; }

    [JsonPropertyName("isActive")]
    public virtual bool? IsActive { get; set; }

    [JsonPropertyName("requirements")]
    public virtual CustomSuitRequirements? Requirements { get; set; }

    [JsonPropertyName("relatedBattlePassSeason")]
    public virtual int? RelatedBattlePassSeason { get; set; }

    [JsonExtensionData]
    public Dictionary<string?, object?>? ExtensionData
    {
        get
        {
            return _extensionData;
        }
        set
        {
            _extensionData = value;
        }
    }

    [JsonIgnore]
    public Dictionary<string?, object?>? _extensionData = new Dictionary<string, object>();
}
public record CustomSuitRequirements : SuitRequirements
{
    [JsonPropertyName("requiredTid")]
    [JsonConverter(typeof(MongoIdConverter))]
    public virtual MongoId? RequiredTid { get; set; }
}