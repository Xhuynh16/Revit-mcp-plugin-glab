using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.Views;

public class SheetSpecInput
{
    [JsonProperty("number")]
    public string Number { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("viewIds")]
    public List<int> ViewIds { get; set; } = new();
}

public class CreateSheetsRequest
{
    [JsonProperty("titleBlockTypeId")]
    public int? TitleBlockTypeId { get; set; }

    [JsonProperty("sheets")]
    public List<SheetSpecInput> Sheets { get; set; } = new();
}

public class ViewPlacementResult
{
    [JsonProperty("viewId")]
    public int ViewId { get; set; }

    [JsonProperty("viewName")]
    public string ViewName { get; set; }

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }
}

public class SheetCreateResult
{
    [JsonProperty("sheetId")]
    public int SheetId { get; set; }

    [JsonProperty("number")]
    public string Number { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }

    [JsonProperty("placedViews")]
    public List<ViewPlacementResult> PlacedViews { get; set; } = new();
}

public class CreateSheetsResult
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }

    [JsonProperty("titleBlockUsed")]
    public string TitleBlockUsed { get; set; }

    [JsonProperty("results")]
    public List<SheetCreateResult> Results { get; set; } = new();
}
