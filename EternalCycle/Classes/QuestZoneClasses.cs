using System.Text.Json.Serialization;

namespace EternalCycleServer;

public class QuestZone
{
    [JsonPropertyName("zoneId")]
    public string ZoneId { get; set; }

    [JsonPropertyName("zoneName")]
    public string ZoneName { get; set; }

    [JsonPropertyName("zoneLocation")]
    public string ZoneLocation { get; set; }

    [JsonPropertyName("zoneType")]
    public string ZoneType { get; set; }  // "visit" / "placeitem" / "killbot" / "flarezone"

    [JsonPropertyName("flareType")]
    public string FlareType { get; set; }  // "Light" / "Airdrop" / "ExitActivate" / "Quest" / "AIFollowEvent"

    [JsonPropertyName("position")]
    public ZoneTransform Position { get; set; }

    [JsonPropertyName("rotation")]
    public ZoneTransform Rotation { get; set; }

    [JsonPropertyName("scale")]
    public ZoneTransform Scale { get; set; }

    [JsonPropertyName("groupPosition")]
    public List<ZoneTransforms> GroupPosition { get; set; }
}

public class ZoneTransform
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("z")]
    public float Z { get; set; }

    [JsonPropertyName("w")]
    public float W { get; set; } = 0;
}

public class ZoneTransforms
{
    [JsonPropertyName("position")]
    public ZoneTransform Position { get; set; }

    [JsonPropertyName("rotation")]
    public ZoneTransform Rotation { get; set; }

    [JsonPropertyName("scale")]
    public ZoneTransform Scale { get; set; }
}