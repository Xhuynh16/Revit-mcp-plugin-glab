using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands.Access;

public class ListLevelsCommand : ExternalEventCommandBase
{
    private ListLevelsEventHandler _handler => (ListLevelsEventHandler)Handler;

    public override string CommandName => "list_levels";

    public ListLevelsCommand(UIApplication uiApp)
        : base(new ListLevelsEventHandler(), uiApp)
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
            throw new TimeoutException("list_levels timed out");
        }
    }
}
