using SPTarkov.Server.Core.Models.Common;
using System.Text.Json.Serialization;
using static EternalCycleServer.Utils;

namespace EternalCycleServer
{
    public class CustomBannerData
    {
        [JsonPropertyName("id")]
        [JsonConverter(typeof(MongoIdConverter))]
        public MongoId Id { get; set; }
        [JsonPropertyName("img")]
        public string ImagePath { get; set; }
        [JsonPropertyName("map")]
        public int Map { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("desc")]
        public string Description { get; set; }
    }
}