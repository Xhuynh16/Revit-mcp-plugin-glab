using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands.Access;

public class ListSheetsCommand : ExternalEventCommandBase
{
    private ListSheetsEventHandler _handler => (ListSheetsEventHandler)Handler;

    public override string CommandName => "list_sheets";

    public ListSheetsCommand(UIApplication uiApp)
        : base(new ListSheetsEventHandler(), uiApp)
    {
    }

    public override object Execute(JObject parameters, string requestId)
    {
        if (RaiseAndWaitForCompletion(30000))
        {
            return _handler.ResultInfo;
        }
        else
        {
            throw new TimeoutException("list_sheets timed out");
        }
    }
}
