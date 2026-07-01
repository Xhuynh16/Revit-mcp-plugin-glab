using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services;

public class GetElementPropertiesEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    public ElementPropertiesResult ResultInfo { get; private set; }
    public bool TaskCompleted { get; private set; }
    public int TargetElementId { get; set; }

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
            var element = doc.GetElement(new ElementId(TargetElementId));

            if (element == null)
            {
                ResultInfo = new ElementPropertiesResult
                {
                    Success = false,
                    Message = $"Element with id {TargetElementId} not found"
                };
                return;
            }

            var parameters = element.Parameters
                .Cast<Parameter>()
                .OrderBy(p => p.Definition.Name)
                .Select(p => new ParameterData
                {
                    Name = p.Definition.Name,
                    Value = GetParameterValueString(p, doc),
                    StorageType = p.StorageType.ToString(),
                    Group = p.Definition.ParameterGroup.ToString(),
                    IsReadOnly = p.IsReadOnly
                })
                .ToList();

            string typeName = null;
            var typeId = element.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                typeName = doc.GetElement(typeId)?.Name;
            }

            ResultInfo = new ElementPropertiesResult
            {
#if REVIT2024_OR_GREATER
                Id = (int)element.Id.Value,
#else
                Id = element.Id.IntegerValue,
#endif
                Name = element.Name,
                Category = element.Category?.Name,
                TypeName = typeName,
                Parameters = parameters,
                Success = true,
                Message = $"Found {parameters.Count} parameters"
            };
        }
        catch (Exception ex)
        {
            ResultInfo = new ElementPropertiesResult
            {
                Success = false,
                Message = $"Error getting element properties: {ex.Message}"
            };
        }
        finally
        {
            TaskCompleted = true;
            _resetEvent.Set();
        }
    }

    private static string GetParameterValueString(Parameter p, Document doc)
    {
        if (p == null || !p.HasValue)
        {
            return null;
        }

        switch (p.StorageType)
        {
            case StorageType.String:
                return p.AsString();
            case StorageType.ElementId:
                var id = p.AsElementId();
                if (id == ElementId.InvalidElementId)
                {
                    return null;
                }
                return doc.GetElement(id)?.Name ?? id.ToString();
            default:
                return p.AsValueString();
        }
    }

    public string GetName() => "Get Element Properties";
}
