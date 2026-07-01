using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Modify;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands.Modify;

public class SetElementParametersCommand : ExternalEventCommandBase
{
    private SetElementParametersEventHandler _handler => (SetElementParametersEventHandler)Handler;

    public override string CommandName => "set_element_parameters";

    public SetElementParametersCommand(UIApplication uiApp)
        : base(new SetElementParametersEventHandler(), uiApp)
    {
    }

    public override object Execute(JObject parameters, string requestId)
    {
        var request = parameters?.ToObject<SetElementParametersRequest>();
        if (request == null || request.ElementIds.Count == 0)
        {
            throw new ArgumentException("elementIds is required and must not be empty");
        }
        if (request.Parameters.Count == 0)
        {
            throw new ArgumentException("parameters is required and must not be empty");
        }

        _handler.Request = request;

        if (RaiseAndWaitForCompletion(30000))
        {
            return _handler.ResultInfo;
        }
        else
        {
            throw new TimeoutException("set_element_parameters timed out");
        }
    }
}
