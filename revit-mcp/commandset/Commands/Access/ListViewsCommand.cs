using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands.Access;

public class ListViewsCommand : ExternalEventCommandBase
{
    private ListViewsEventHandler _handler => (ListViewsEventHandler)Handler;

    public override string CommandName => "list_views";

    public ListViewsCommand(UIApplication uiApp)
        : base(new ListViewsEventHandler(), uiApp)
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
            throw new TimeoutException("list_views timed out");
        }
    }
}
