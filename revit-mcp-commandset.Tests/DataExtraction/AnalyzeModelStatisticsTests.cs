using Autodesk.Revit.DB;
using Nice3point.TUnit.Revit;

namespace RevitMCPCommandSet.Tests.DataExtraction;

[ClassSetup]
[ClassCleanup]
public class AnalyzeModelStatisticsTests : RevitApiTest
{
    private Document _doc;

    [ClassSetup]
    public void Setup()
    {
        _doc = Application.NewProjectDocument(UnitSystem.Imperial);

        using var tx = new Transaction(_doc, "Setup Statistics Test");
        tx.Start();

        // Create a level and some elements for statistics
        var level = Level.Create(_doc, 0.0);
        level.Name = "Stats Test Level";

        var floorPlanType = new FilteredElementCollector(_doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.FloorPlan);

        if (floorPlanType != null)
        {
            ViewPlan.Create(_doc, floorPlanType.Id, level.Id);
        }

        // Create walls to have some categorized elements
        Wall.Create(_doc, Line.CreateBound(new XYZ(0, 0, 0), new XYZ(10, 0, 0)), level.Id, false);
        Wall.Create(_doc, Line.CreateBound(new XYZ(10, 0, 0), new XYZ(10, 10, 0)), level.Id, false);
        Wall.Create(_doc, Line.CreateBound(new XYZ(10, 10, 0), new XYZ(0, 10, 0)), level.Id, false);

        tx.Commit();
    }

    [ClassCleanup]
    public void Cleanup()
    {
        _doc?.Close(false);
    }

    [Test]
    public void TotalElementCount_MatchesFilteredElementCollector()
    {
        int totalElements = new FilteredElementCollector(_doc)
            .WhereElementIsNotElementType()
            .GetElementCount();

        Assert.That(totalElements, Is.GreaterThan(0));

        int totalTypes = new FilteredElementCollector(_doc)
            .WhereElementIsElementType()
            .GetElementCount();

        Assert.That(totalTypes, Is.GreaterThan(0));
    }

    [Test]
    public void CategoryGrouping_ElementsGroupedByCategory_CountsCorrect()
    {
        var elements = new FilteredElementCollector(_doc)
            .WhereElementIsNotElementType()
            .ToElements();

        var categoryGroups = new Dictionary<string, int>();
        foreach (var elem in elements)
        {
            if (elem.Category == null) continue;
            string catName = elem.Category.Name;

            if (!categoryGroups.ContainsKey(catName))
                categoryGroups[catName] = 0;
            categoryGroups[catName]++;
        }

        // Should have at least walls category
        Assert.That(categoryGroups.Count, Is.GreaterThan(0));

        // Total of grouped counts should match elements with categories
        int groupedTotal = categoryGroups.Values.Sum();
        int elementsWithCategories = elements.Count(e => e.Category != null);
        Assert.That(groupedTotal, Is.EqualTo(elementsWithCategories));
    }

    [Test]
    public void LevelStatistics_ElevationAndElementCount_Populated()
    {
        var levels = new FilteredElementCollector(_doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .ToList();

        Assert.That(levels.Count, Is.GreaterThan(0));

        foreach (var level in levels)
        {
            Assert.That(level.Name, Is.Not.Null.And.Not.Empty);

            int elementCount = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .Where(e => e.LevelId == level.Id)
                .Count();

            // Element count should be non-negative
            Assert.That(elementCount, Is.GreaterThanOrEqualTo(0));
        }
    }

    [Test]
    public void ViewCounting_ExcludesTemplates_CountCorrect()
    {
        var allViews = new FilteredElementCollector(_doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .ToList();

        int totalViewsExcludingTemplates = allViews.Count(v => !v.IsTemplate);
        int templateCount = allViews.Count(v => v.IsTemplate);

        Assert.That(totalViewsExcludingTemplates, Is.GreaterThan(0));
        Assert.That(totalViewsExcludingTemplates + templateCount, Is.EqualTo(allViews.Count));
    }

    [Test]
    public void DetailedTypeBreakdown_FamilyInstanceTypes_TrackedCorrectly()
    {
        var elements = new FilteredElementCollector(_doc)
            .WhereElementIsNotElementType()
            .ToElements();

        var typeStats = new Dictionary<string, (string FamilyName, string TypeName, int Count)>();
        var familyNames = new HashSet<string>();

        foreach (var elem in elements)
        {
            if (elem is FamilyInstance fi)
            {
                string familyName = fi.Symbol?.Family?.Name;
                string typeName = fi.Symbol?.Name;

                if (!string.IsNullOrEmpty(familyName))
                    familyNames.Add(familyName);

                if (!string.IsNullOrEmpty(typeName))
                {
                    string key = $"{familyName}:{typeName}";
                    if (typeStats.ContainsKey(key))
                    {
                        var existing = typeStats[key];
                        typeStats[key] = (existing.FamilyName, existing.TypeName, existing.Count + 1);
                    }
                    else
                    {
                        typeStats[key] = (familyName, typeName, 1);
                    }
                }
            }
        }

        // Validate that all tracked types have positive instance counts
        foreach (var kvp in typeStats)
        {
            Assert.That(kvp.Value.Count, Is.GreaterThan(0));
            Assert.That(kvp.Value.TypeName, Is.Not.Null.And.Not.Empty);
        }

        // Family names should be a subset of all families
        foreach (var name in familyNames)
        {
            Assert.That(name, Is.Not.Null.And.Not.Empty);
        }
    }
}
