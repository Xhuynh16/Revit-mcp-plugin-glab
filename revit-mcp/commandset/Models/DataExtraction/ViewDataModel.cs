using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.DataExtraction;

public class ViewDataResult
{
    [JsonProperty("views")]
    public List<ViewData> Views { get; set; } = new();

    [JsonProperty("count")]
    public int Count { get; set; }

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }
}

public class ViewData
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("viewType")]
    public string ViewType { get; set; }

    [JsonProperty("isTemplate")]
    public bool IsTemplate { get; set; }

    [JsonProperty("levelName")]
    public string LevelName { get; set; }
}
