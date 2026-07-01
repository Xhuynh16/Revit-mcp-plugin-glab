using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.DataExtraction;

public class SheetDataResult
{
    [JsonProperty("sheets")]
    public List<SheetData> Sheets { get; set; } = new();

    [JsonProperty("count")]
    public int Count { get; set; }

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }
}

public class SheetData
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("sheetNumber")]
    public string SheetNumber { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; }
}
