using SPTarkov.Server.Core.Models.Utils;
using System.Text.Json.Serialization;

namespace EternalCycleServer
{
    public class SyncResourceRequest : IRequestData
    {
        [JsonPropertyName("clientHashes")]
        public Dictionary<string, string> ClientHashes { get; set; } = new Dictionary<string, string>();
    }

    public class SyncResourceResponse
    {
        [JsonPropertyName("validFiles")]
        public List<string> ValidFiles { get; set; } = new List<string>();

        [JsonPropertyName("filesToUpdate")]
        public Dictionary<string, string> FilesToUpdate { get; set; } = new Dictionary<string, string>();
    }
}