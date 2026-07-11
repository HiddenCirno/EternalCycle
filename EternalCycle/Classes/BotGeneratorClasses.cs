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
using static EternalCycleServer.Utils;

namespace EternalCycleServer
{
    public class CustomAlterBot
    {
        [JsonPropertyName("role")]
        public string BotRole { get; set; }

        [JsonPropertyName("type")]

        public BotType? BotType { get; set; }

        [JsonPropertyName("forceloot")]
        public Dictionary<MongoId, int> ForcedLoot {  get; set; }

        [JsonPropertyName("typeloot")]
        public Dictionary<MongoId, int> ForcedLootWithType { get; set; }

        [JsonPropertyName("location")]
        public int SpawnLocation { get; set; } //BitMap

        [JsonPropertyName("chance")]
        public int Chance { get; set; }

        [JsonPropertyName("cleanweapon")]
        public bool CleanWeaponDur {  get; set; }
    }
}