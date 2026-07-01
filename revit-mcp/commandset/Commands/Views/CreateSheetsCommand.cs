using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Views;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;
using System.Linq;

namespace RevitMCPCommandSet.Commands.Views;

public class CreateSheetsCommand : ExternalEventCommandBase
{
    private CreateSheetsEventHandler _handler => (CreateSheetsEventHandler)Handler;

    public override string CommandName => "create_sheets";

    public CreateSheetsCommand(UIApplication uiApp)
        : base(new CreateSheetsEventHandler(), uiApp)
    {
    }

    public override object Execute(JObject parameters, string requestId)
    {
        var request = parameters?.ToObject<CreateSheetsRequest>();
        if (request == null || request.Sheets.Count == 0)
        {
            throw new ArgumentException("sheets is required and must not be empty");
        }
        if (request.Sheets.Any(s => string.IsNullOrWhiteSpace(s.Number)))
        {
            throw new ArgumentException("every sheet must have a non-empty number");
        }

        _handler.Request = request;

        if (RaiseAndWaitForCompletion(60000))
        {
            return _handler.ResultInfo;
        }
        else
        {
            throw new TimeoutException("create_sheets timed out");
        }
    }
}
