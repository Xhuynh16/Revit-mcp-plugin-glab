using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.Modify;

public class SetParameterInput
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("value")]
    public string Value { get; set; }
}

public class SetElementParametersRequest
{
    [JsonProperty("elementIds")]
    public List<int> ElementIds { get; set; } = new();

    [JsonProperty("parameters")]
    public List<SetParameterInput> Parameters { get; set; } = new();
}

public class ParameterSetResult
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }
}

public class ElementParameterResult
{
    [JsonProperty("elementId")]
    public int ElementId { get; set; }

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }

    [JsonProperty("parameters")]
    public List<ParameterSetResult> Parameters { get; set; } = new();
}

public class SetElementParametersResult
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }

    [JsonProperty("results")]
    public List<ElementParameterResult> Results { get; set; } = new();
}
