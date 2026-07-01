using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.DataExtraction;

public class LevelDataResult
{
    [JsonProperty("levels")]
    public List<LevelData> Levels { get; set; } = new();

    [JsonProperty("count")]
    public int Count { get; set; }

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }
}

public class LevelData
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("elevationMeters")]
    public double ElevationMeters { get; set; }

    [JsonProperty("isBuildingStory")]
    public bool IsBuildingStory { get; set; }
}
