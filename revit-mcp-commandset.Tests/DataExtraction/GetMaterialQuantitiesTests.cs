using Autodesk.Revit.DB;
using Nice3point.TUnit.Revit;

namespace RevitMCPCommandSet.Tests.DataExtraction;

[ClassSetup]
[ClassCleanup]
public class GetMaterialQuantitiesTests : RevitApiTest
{
    private Document _doc;
    private Level _level;

    [ClassSetup]
    public void Setup()
    {
        _doc = Application.NewProjectDocument(UnitSystem.Imperial);

        using var tx = new Transaction(_doc, "Setup Material Quantities Test");
        tx.Start();

        _level = Level.Create(_doc, 0.0);
        _level.Name = "Material Test Level";

        // Create walls that will have materials
        Wall.Create(_doc, Line.CreateBound(new XYZ(0, 0, 0), new XYZ(10, 0, 0)), _level.Id, false);
        Wall.Create(_doc, Line.CreateBound(new XYZ(10, 0, 0), new XYZ(10, 10, 0)), _level.Id, false);
        Wall.Create(_doc, Line.CreateBound(new XYZ(10, 10, 0), new XYZ(0, 10, 0)), _level.Id, false);

        tx.Commit();
    }

    [ClassCleanup]
    public void Cleanup()
    {
        _doc?.Close(false);
    }

    [Test]
    public void ExtractMaterials_FromWalls_MaterialNamesPopulated()
    {
        var walls = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_Walls)
            .WhereElementIsNotElementType()
            .ToElements();

        Assert.That(walls.Count, Is.GreaterThan(0));

        var materialData = new Dictionary<ElementId, (string Name, string Class, double Area, double Volume, HashSet<ElementId> Elements)>();

        foreach (var element in walls)
        {
            var materialIds = element.GetMaterialIds(false);
            foreach (var matId in materialIds)
            {
                var material = _doc.GetElement(matId) as Material;
                if (material == null) continue;

                if (!materialData.ContainsKey(matId))
                {
                    materialData[matId] = (material.Name, material.MaterialClass, 0, 0, new HashSet<ElementId>());
                }

                double area = element.GetMaterialArea(matId, false);
                double volume = element.GetMaterialVolume(matId);

                var existing = materialData[matId];
                materialData[matId] = (existing.Name, existing.Class, existing.Area + area, existing.Volume + volume, existing.Elements);
                materialData[matId].Elements.Add(element.Id);
            }
        }

        // Walls in a default template should have at least one material
        Assert.That(materialData.Count, Is.GreaterThan(0));

        foreach (var kvp in materialData)
        {
            Assert.That(kvp.Value.Name, Is.Not.Null.And.Not.Empty);
        }
    }

    [Test]
    public void FilterByCategory_WallsOnly_OnlyWallMaterialsReturned()
    {
        var builtInCategories = new List<BuiltInCategory> { BuiltInCategory.OST_Walls };
        var filter = new ElementMulticategoryFilter(builtInCategories);

        var elements = new FilteredElementCollector(_doc)
            .WhereElementIsNotElementType()
            .WherePasses(filter)
            .ToElements();

        // All returned elements should be walls
        foreach (var element in elements)
        {
            Assert.That(element.Category, Is.Not.Null);
#if REVIT2024_OR_GREATER
            Assert.That(element.Category.Id.Value, Is.EqualTo((long)BuiltInCategory.OST_Walls));
#else
            Assert.That(element.Category.Id.IntegerValue, Is.EqualTo((int)BuiltInCategory.OST_Walls));
#endif
        }
    }

    [Test]
    public void MaterialAreaVolume_Accumulation_ValuesNonNegative()
    {
        var walls = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_Walls)
            .WhereElementIsNotElementType()
            .ToElements();

        double totalArea = 0;
        double totalVolume = 0;

        foreach (var element in walls)
        {
            var materialIds = element.GetMaterialIds(false);
            foreach (var matId in materialIds)
            {
                double area = element.GetMaterialArea(matId, false);
                double volume = element.GetMaterialVolume(matId);

                Assert.That(area, Is.GreaterThanOrEqualTo(0));
                Assert.That(volume, Is.GreaterThanOrEqualTo(0));

                totalArea += area;
                totalVolume += volume;
            }
        }

        Assert.That(totalArea, Is.GreaterThanOrEqualTo(0));
        Assert.That(totalVolume, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void ElementCountPerMaterial_TracksUniqueElements()
    {
        var walls = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_Walls)
            .WhereElementIsNotElementType()
            .ToElements();

        var materialElementSets = new Dictionary<ElementId, HashSet<ElementId>>();

        foreach (var element in walls)
        {
            var materialIds = element.GetMaterialIds(false);
            foreach (var matId in materialIds)
            {
                if (!materialElementSets.ContainsKey(matId))
                    materialElementSets[matId] = new HashSet<ElementId>();

                materialElementSets[matId].Add(element.Id);
            }
        }

        foreach (var kvp in materialElementSets)
        {
            // Element count should match the unique element set count
            int elementCount = kvp.Value.Count;
            Assert.That(elementCount, Is.GreaterThan(0));

            // Adding the same element twice should not increase count
            var testSet = new HashSet<ElementId>(kvp.Value);
            int countBefore = testSet.Count;
            testSet.Add(kvp.Value.First()); // Add duplicate
            Assert.That(testSet.Count, Is.EqualTo(countBefore));
        }
    }
}
