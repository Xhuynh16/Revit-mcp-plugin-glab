using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services;

public class ListSheetsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    public SheetDataResult ResultInfo { get; private set; }
    public bool TaskCompleted { get; private set; }
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

            var sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .OrderBy(s => s.SheetNumber)
                .Select(s => new SheetData
                {
#if REVIT2024_OR_GREATER
                    Id = (int)s.Id.Value,
#else
                    Id = s.Id.IntegerValue,
#endif
                    SheetNumber = s.SheetNumber,
                    Title = s.Name
                })
                .ToList();

            ResultInfo = new SheetDataResult
            {
                Sheets = sheets,
                Count = sheets.Count,
                Success = true,
                Message = $"Found {sheets.Count} sheets"
            };
        }
        catch (Exception ex)
        {
            ResultInfo = new SheetDataResult
            {
                Success = false,
                Message = $"Error listing sheets: {ex.Message}"
            };
        }
        finally
        {
            TaskCompleted = true;
            _resetEvent.Set();
        }
    }

    public string GetName() => "List Sheets";
}
