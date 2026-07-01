using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services;

public class ListViewsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    public ViewDataResult ResultInfo { get; private set; }
    public bool TaskCompleted { get; private set; }
    private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

    private static readonly HashSet<ViewType> ExcludedViewTypes = new HashSet<ViewType>
    {
        ViewType.Internal,
        ViewType.SystemBrowser,
        ViewType.ProjectBrowser,
        ViewType.DrawingSheet,
        ViewType.Undefined
    };

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

            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !ExcludedViewTypes.Contains(v.ViewType))
                .OrderBy(v => v.ViewType.ToString())
                .ThenBy(v => v.Name)
                .Select(v => new ViewData
                {
#if REVIT2024_OR_GREATER
                    Id = (int)v.Id.Value,
#else
                    Id = v.Id.IntegerValue,
#endif
                    Name = v.Name,
                    ViewType = v.ViewType.ToString(),
                    IsTemplate = v.IsTemplate,
                    LevelName = v.GenLevel?.Name
                })
                .ToList();

            ResultInfo = new ViewDataResult
            {
                Views = views,
                Count = views.Count,
                Success = true,
                Message = $"Found {views.Count} views"
            };
        }
        catch (Exception ex)
        {
            ResultInfo = new ViewDataResult
            {
                Success = false,
                Message = $"Error listing views: {ex.Message}"
            };
        }
        finally
        {
            TaskCompleted = true;
            _resetEvent.Set();
        }
    }

    public string GetName() => "List Views";
}
