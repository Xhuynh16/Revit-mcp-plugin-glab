using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services;

public class ListLevelsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    public LevelDataResult ResultInfo { get; private set; }
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

            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .Select(l => new LevelData
                {
#if REVIT2024_OR_GREATER
                    Id = (int)l.Id.Value,
#else
                    Id = l.Id.IntegerValue,
#endif
                    Name = l.Name,
                    ElevationMeters = Math.Round(
                        UnitUtils.ConvertFromInternalUnits(l.Elevation, UnitTypeId.Meters), 3),
                    IsBuildingStory = l.get_Parameter(BuiltInParameter.LEVEL_IS_BUILDING_STORY)?.AsInteger() == 1
                })
                .ToList();

            ResultInfo = new LevelDataResult
            {
                Levels = levels,
                Count = levels.Count,
                Success = true,
                Message = $"Found {levels.Count} levels"
            };
        }
        catch (Exception ex)
        {
            ResultInfo = new LevelDataResult
            {
                Success = false,
                Message = $"Error listing levels: {ex.Message}"
            };
        }
        finally
        {
            TaskCompleted = true;
            _resetEvent.Set();
        }
    }

    public string GetName() => "List Levels";
}
