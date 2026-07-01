using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Modify;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;

namespace RevitMCPCommandSet.Services;

public class SetElementParametersEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    public SetElementParametersResult ResultInfo { get; private set; }
    public bool TaskCompleted { get; private set; }
    public SetElementParametersRequest Request { get; set; }

    private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

    public bool WaitForCompletion(int timeoutMilliseconds = 10000)
    {
        _resetEvent.Reset();
        return _resetEvent.WaitOne(timeoutMilliseconds);
    }

    public void Execute(UIApplication app)
    {
        try
        {
            var doc = app.ActiveUIDocument.Document;
            var results = new List<ElementParameterResult>();

            using (var transaction = new Transaction(doc, "Set Element Parameters"))
            {
                transaction.Start();

                foreach (var elementId in Request.ElementIds)
                {
                    var element = doc.GetElement(new ElementId(elementId));
                    var elementResult = new ElementParameterResult { ElementId = elementId };

                    if (element == null)
                    {
                        elementResult.Success = false;
                        elementResult.Message = "Element not found";
                        results.Add(elementResult);
                        continue;
                    }

                    bool elementSuccess = true;
                    foreach (var paramInput in Request.Parameters)
                    {
                        var paramResult = SetSingleParameter(element, doc, paramInput);
                        elementResult.Parameters.Add(paramResult);
                        if (!paramResult.Success)
                        {
                            elementSuccess = false;
                        }
                    }

                    elementResult.Success = elementSuccess;
                    elementResult.Message = elementSuccess
                        ? "All parameters set"
                        : "One or more parameters failed";
                    results.Add(elementResult);
                }

                transaction.Commit();
            }

            ResultInfo = new SetElementParametersResult
            {
                Success = true,
                Message = $"Processed {results.Count} element(s)",
                Results = results
            };
        }
        catch (Exception ex)
        {
            ResultInfo = new SetElementParametersResult
            {
                Success = false,
                Message = $"Error setting element parameters: {ex.Message}",
                Results = new List<ElementParameterResult>()
            };
        }
        finally
        {
            TaskCompleted = true;
            _resetEvent.Set();
        }
    }

    private static ParameterSetResult SetSingleParameter(Element element, Document doc, SetParameterInput input)
    {
        var result = new ParameterSetResult { Name = input.Name };

        var parameter = element.LookupParameter(input.Name);
        if (parameter == null)
        {
            result.Success = false;
            result.Message = "Parameter not found on element";
            return result;
        }

        if (parameter.IsReadOnly)
        {
            result.Success = false;
            result.Message = "Parameter is read-only";
            return result;
        }

        if (parameter.StorageType == StorageType.ElementId || parameter.StorageType == StorageType.None)
        {
            result.Success = false;
            result.Message = $"Unsupported storage type: {parameter.StorageType}";
            return result;
        }

        try
        {
            // SetValueString accepts the value in the same display format/units the user
            // would type in Revit's UI, so it handles unit conversion for Double parameters
            // and works for String/Integer parameters as well.
            bool ok = parameter.SetValueString(input.Value);
            result.Success = ok;
            result.Message = ok ? null : "Revit rejected the value";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = ex.Message;
        }

        return result;
    }

    public string GetName() => "Set Element Parameters";
}
