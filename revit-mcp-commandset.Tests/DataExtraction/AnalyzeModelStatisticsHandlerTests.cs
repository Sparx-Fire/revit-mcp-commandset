using Autodesk.Revit.DB;
using Nice3point.TUnit.Revit;
using Nice3point.TUnit.Revit.Executors;
using RevitMCPCommandSet.Services.DataExtraction;
using TUnit.Core;
using TUnit.Core.Executors;

namespace RevitMCPCommandSet.Tests.DataExtraction;

public class AnalyzeModelStatisticsHandlerTests : RevitApiTest
{
    private static Document _doc;
    private static string _tempPath;

    [Before(HookType.Class)]
    [HookExecutor<RevitThreadExecutor>]
    public static void Setup()
    {
        var doc = Application.NewProjectDocument(UnitSystem.Imperial);

        using (var tx = new Transaction(doc, "Setup Statistics Test"))
        {
            tx.Start();

            var level = Level.Create(doc, 0.0);
            level.Name = "Stats Test Level";

            var floorPlanType = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.FloorPlan);

            if (floorPlanType != null)
            {
                ViewPlan.Create(doc, floorPlanType.Id, level.Id);
            }

            // Create walls to have some categorized elements
            Wall.Create(doc, Line.CreateBound(new XYZ(0, 0, 0), new XYZ(10, 0, 0)), level.Id, false);
            Wall.Create(doc, Line.CreateBound(new XYZ(10, 0, 0), new XYZ(10, 10, 0)), level.Id, false);
            Wall.Create(doc, Line.CreateBound(new XYZ(10, 10, 0), new XYZ(0, 10, 0)), level.Id, false);

            tx.Commit();
        }

        _tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.rvt");
        doc.SaveAs(_tempPath);
        doc.Close(false);
        _doc = Application.OpenDocumentFile(_tempPath);
    }

    [After(HookType.Class)]
    [HookExecutor<RevitThreadExecutor>]
    public static void Cleanup()
    {
        _doc?.Close(false);
        try { File.Delete(_tempPath); } catch { }
    }

    [Test]
    [TestExecutor<RevitThreadExecutor>]
    public async Task Execute_BasicModel_ReturnsSuccessfulResult()
    {
        var handler = new AnalyzeModelStatisticsHandler();
        handler.SetParameters(includeDetailedTypes: true);

        handler.RunOnDocument(_doc);

        await Assert.That(handler.ResultInfo).IsNotNull();
        await Assert.That(handler.ResultInfo.Success).IsTrue();
        await Assert.That(handler.ResultInfo.Message).IsNotNull();
    }

    [Test]
    [TestExecutor<RevitThreadExecutor>]
    public async Task Execute_CategoriesPopulated_WallsCounted()
    {
        var handler = new AnalyzeModelStatisticsHandler();
        handler.SetParameters(includeDetailedTypes: true);

        handler.RunOnDocument(_doc);

        await Assert.That(handler.ResultInfo.Success).IsTrue();
        await Assert.That(handler.ResultInfo.TotalElements).IsGreaterThan(0);
        await Assert.That(handler.ResultInfo.Categories.Count).IsGreaterThan(0);

        // Verify walls category exists
        var wallCategory = handler.ResultInfo.Categories.FirstOrDefault(c => c.CategoryName == "Walls");
        await Assert.That(wallCategory).IsNotNull();
        await Assert.That(wallCategory.ElementCount).IsGreaterThan(0);
    }

    [Test]
    [TestExecutor<RevitThreadExecutor>]
    public async Task Execute_LevelStatistics_LevelsListed()
    {
        var handler = new AnalyzeModelStatisticsHandler();
        handler.SetParameters(includeDetailedTypes: false);

        handler.RunOnDocument(_doc);

        await Assert.That(handler.ResultInfo.Success).IsTrue();
        await Assert.That(handler.ResultInfo.Levels.Count).IsGreaterThan(0);

        foreach (var level in handler.ResultInfo.Levels)
        {
            await Assert.That(level.LevelName).IsNotNullOrEmpty();
        }
    }

    [Test]
    [TestExecutor<RevitThreadExecutor>]
    public async Task Execute_ProjectName_MatchesDocument()
    {
        var handler = new AnalyzeModelStatisticsHandler();
        handler.SetParameters(includeDetailedTypes: false);

        handler.RunOnDocument(_doc);

        await Assert.That(handler.ResultInfo.Success).IsTrue();
        await Assert.That(handler.ResultInfo.ProjectName).IsNotNullOrEmpty();
    }

    [Test]
    [TestExecutor<RevitThreadExecutor>]
    public async Task Execute_TaskCompleted_FlagSet()
    {
        var handler = new AnalyzeModelStatisticsHandler();
        handler.SetParameters(includeDetailedTypes: true);

        handler.RunOnDocument(_doc);

        await Assert.That(handler.TaskCompleted).IsTrue();
        await Assert.That(handler.ResultInfo.Success).IsTrue();
        await Assert.That(handler.ResultInfo.TotalTypes).IsGreaterThan(0);
        await Assert.That(handler.ResultInfo.TotalViews).IsGreaterThan(0);
    }
}
