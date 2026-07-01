using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands.Access;

public class GetElementPropertiesCommand : ExternalEventCommandBase
{
    private GetElementPropertiesEventHandler _handler => (GetElementPropertiesEventHandler)Handler;

    public override string CommandName => "get_element_properties";

    public GetElementPropertiesCommand(UIApplication uiApp)
        : base(new GetElementPropertiesEventHandler(), uiApp)
    {
    }

    public override object Execute(JObject parameters, string requestId)
    {
        var elementId = parameters?["elementId"]?.ToObject<int?>();
        if (elementId == null)
        {
            throw new ArgumentException("elementId is required");
        }

        _handler.TargetElementId = elementId.Value;

        if (RaiseAndWaitForCompletion(30000))
        {
            return _handler.ResultInfo;
        }
        else
        {
            throw new TimeoutException("get_element_properties timed out");
        }
    }
}
