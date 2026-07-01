using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Views;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services;

public class CreateSheetsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    public CreateSheetsResult ResultInfo { get; private set; }
    public bool TaskCompleted { get; private set; }
    public CreateSheetsRequest Request { get; set; }

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

            var titleBlockTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .Cast<FamilySymbol>()
                .ToList();

            FamilySymbol titleBlock = null;
            if (Request.TitleBlockTypeId.HasValue)
            {
                titleBlock = titleBlockTypes.FirstOrDefault(t =>
#if REVIT2024_OR_GREATER
                    (int)t.Id.Value == Request.TitleBlockTypeId.Value);
#else
                    t.Id.IntegerValue == Request.TitleBlockTypeId.Value);
#endif
                if (titleBlock == null)
                {
                    ResultInfo = new CreateSheetsResult
                    {
                        Success = false,
                        Message = $"titleBlockTypeId {Request.TitleBlockTypeId.Value} is not a title block type in this project. " +
                                  "Use get_available_family_types with category 'OST_TitleBlocks' to find valid IDs."
                    };
                    return;
                }
            }
            else
            {
                titleBlock = titleBlockTypes.FirstOrDefault();
            }

            var titleBlockId = titleBlock?.Id ?? ElementId.InvalidElementId;
            var titleBlockUsed = titleBlock != null
                ? $"{titleBlock.FamilyName}: {titleBlock.Name}"
                : "(none — no title block type loaded in project)";

            // Sheet numbers must be unique across the document, including earlier sheets in this batch
            var usedNumbers = new HashSet<string>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Select(s => s.SheetNumber),
                StringComparer.OrdinalIgnoreCase);

            var results = new List<SheetCreateResult>();

            using (var transaction = new Transaction(doc, "Create Sheets"))
            {
                transaction.Start();

                foreach (var spec in Request.Sheets)
                {
                    results.Add(CreateSingleSheet(doc, spec, titleBlockId, usedNumbers));
                }

                transaction.Commit();
            }

            int createdCount = results.Count(r => r.Success);
            int viewsRequested = results.Sum(r => r.PlacedViews.Count);
            int viewsPlaced = results.Sum(r => r.PlacedViews.Count(v => v.Success));

            ResultInfo = new CreateSheetsResult
            {
                Success = true,
                Message = $"Created {createdCount} of {Request.Sheets.Count} sheet(s), placed {viewsPlaced} of {viewsRequested} view(s)",
                TitleBlockUsed = titleBlockUsed,
                Results = results
            };
        }
        catch (Exception ex)
        {
            ResultInfo = new CreateSheetsResult
            {
                Success = false,
                Message = $"Error creating sheets: {ex.Message}"
            };
        }
        finally
        {
            TaskCompleted = true;
            _resetEvent.Set();
        }
    }

    private static SheetCreateResult CreateSingleSheet(Document doc, SheetSpecInput spec, ElementId titleBlockId, HashSet<string> usedNumbers)
    {
        var result = new SheetCreateResult { Number = spec.Number, Name = spec.Name };

        if (string.IsNullOrWhiteSpace(spec.Number))
        {
            result.Success = false;
            result.Message = "Sheet number is required";
            return result;
        }

        if (usedNumbers.Contains(spec.Number))
        {
            result.Success = false;
            result.Message = $"Sheet number '{spec.Number}' is already in use";
            return result;
        }

        ViewSheet sheet = null;
        try
        {
            sheet = ViewSheet.Create(doc, titleBlockId);
            sheet.SheetNumber = spec.Number;
            if (!string.IsNullOrWhiteSpace(spec.Name))
            {
                sheet.Name = spec.Name;
            }
        }
        catch (Exception ex)
        {
            if (sheet != null)
            {
                try { doc.Delete(sheet.Id); } catch { }
            }
            result.Success = false;
            result.Message = $"Failed to create sheet: {ex.Message}";
            return result;
        }

        usedNumbers.Add(spec.Number);
#if REVIT2024_OR_GREATER
        result.SheetId = (int)sheet.Id.Value;
#else
        result.SheetId = sheet.Id.IntegerValue;
#endif

        var points = ComputePlacementPoints(sheet, spec.ViewIds.Count);
        for (int i = 0; i < spec.ViewIds.Count; i++)
        {
            result.PlacedViews.Add(PlaceView(doc, sheet, spec.ViewIds[i], points[i]));
        }

        bool allViewsOk = result.PlacedViews.All(v => v.Success);
        result.Success = true;
        result.Message = spec.ViewIds.Count == 0
            ? "Sheet created"
            : allViewsOk ? "Sheet created, all views placed" : "Sheet created, but one or more views failed to place";
        return result;
    }

    private static ViewPlacementResult PlaceView(Document doc, ViewSheet sheet, int viewId, XYZ point)
    {
        var result = new ViewPlacementResult { ViewId = viewId };

        var view = doc.GetElement(new ElementId(viewId)) as View;
        if (view == null)
        {
            result.Success = false;
            result.Message = "View not found";
            return result;
        }

        result.ViewName = view.Name;

        if (view.IsTemplate)
        {
            result.Success = false;
            result.Message = "View is a view template and cannot be placed on a sheet";
            return result;
        }

        try
        {
            // Schedules use a different placement API than graphical views
            if (view is ViewSchedule schedule)
            {
                ScheduleSheetInstance.Create(doc, sheet.Id, schedule.Id, point);
                result.Success = true;
                return result;
            }

            if (!Viewport.CanAddViewToSheet(doc, sheet.Id, view.Id))
            {
                result.Success = false;
                result.Message = "View cannot be placed on this sheet (it may already be on another sheet, or its view type is not supported)";
                return result;
            }

            Viewport.Create(doc, sheet.Id, view.Id, point);
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = ex.Message;
        }

        return result;
    }

    private static List<XYZ> ComputePlacementPoints(ViewSheet sheet, int count)
    {
        var points = new List<XYZ>();
        if (count == 0) return points;

        // Sheet outline is in feet; fall back to an A1-sized region (841 x 594 mm)
        // when there is no title block to define the outline.
        var outline = sheet.Outline;
        double minU = outline.Min.U, minV = outline.Min.V;
        double maxU = outline.Max.U, maxV = outline.Max.V;
        if (maxU - minU < 0.1 || maxV - minV < 0.1)
        {
            minU = 0; minV = 0; maxU = 2.76; maxV = 1.95;
        }

        int cols = (int)Math.Ceiling(Math.Sqrt(count));
        int rows = (int)Math.Ceiling(count / (double)cols);
        double cellW = (maxU - minU) / cols;
        double cellH = (maxV - minV) / rows;

        for (int i = 0; i < count; i++)
        {
            int col = i % cols;
            int row = i / cols;
            double u = minU + (col + 0.5) * cellW;
            double v = maxV - (row + 0.5) * cellH;
            points.Add(new XYZ(u, v, 0));
        }

        return points;
    }

    public string GetName() => "Create Sheets";
}
