using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.DataExtraction;

public class ElementPropertiesResult
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("category")]
    public string Category { get; set; }

    [JsonProperty("typeName")]
    public string TypeName { get; set; }

    [JsonProperty("parameters")]
    public List<ParameterData> Parameters { get; set; } = new();

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }
}

public class ParameterData
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("value")]
    public string Value { get; set; }

    [JsonProperty("storageType")]
    public string StorageType { get; set; }

    [JsonProperty("group")]
    public string Group { get; set; }

    [JsonProperty("isReadOnly")]
    public bool IsReadOnly { get; set; }
}
