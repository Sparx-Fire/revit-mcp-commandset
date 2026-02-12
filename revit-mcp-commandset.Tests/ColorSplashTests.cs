using Autodesk.Revit.DB;
using Nice3point.TUnit.Revit;

namespace RevitMCPCommandSet.Tests;

[ClassSetup]
[ClassCleanup]
public class ColorSplashTests : RevitApiTest
{
    private Document _doc;
    private Level _level;
    private ViewPlan _floorPlan;

    [ClassSetup]
    public void Setup()
    {
        _doc = Application.NewProjectDocument(UnitSystem.Imperial);

        using var tx = new Transaction(_doc, "Setup Color Splash Test Environment");
        tx.Start();

        _level = Level.Create(_doc, 0.0);
        _level.Name = "Color Test Level";

        var floorPlanType = new FilteredElementCollector(_doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.FloorPlan);

        if (floorPlanType != null)
        {
            _floorPlan = ViewPlan.Create(_doc, floorPlanType.Id, _level.Id);
        }

        // Create walls with different types to test parameter grouping
        var p1 = new XYZ(0, 0, 0);
        var p2 = new XYZ(10, 0, 0);
        var p3 = new XYZ(20, 0, 0);
        var p4 = new XYZ(30, 0, 0);

        Wall.Create(_doc, Line.CreateBound(p1, p2), _level.Id, false);
        Wall.Create(_doc, Line.CreateBound(p2, p3), _level.Id, false);
        Wall.Create(_doc, Line.CreateBound(p3, p4), _level.Id, false);

        tx.Commit();
    }

    [ClassCleanup]
    public void Cleanup()
    {
        _doc?.Close(false);
    }

    [Test]
    public void GroupElementsByParameter_WallComments_GroupsCorrectly()
    {
        // Set comments on walls to create groups
        var walls = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_Walls)
            .WhereElementIsNotElementType()
            .ToElements();

        Assert.That(walls.Count, Is.GreaterThanOrEqualTo(3));

        using (var tx = new Transaction(_doc, "Set Wall Comments"))
        {
            tx.Start();
            int i = 0;
            foreach (var wall in walls)
            {
                var param = wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (param != null && !param.IsReadOnly)
                {
                    param.Set(i < 2 ? "Group A" : "Group B");
                }
                i++;
            }
            tx.Commit();
        }

        // Group by parameter value (mimics handler logic)
        var groups = new Dictionary<string, List<ElementId>>();
        foreach (var wall in walls)
        {
            var param = wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            string value = param?.HasValue == true ? param.AsString() ?? "None" : "None";

            if (!groups.ContainsKey(value))
                groups[value] = new List<ElementId>();
            groups[value].Add(wall.Id);
        }

        Assert.That(groups.ContainsKey("Group A"), Is.True);
        Assert.That(groups.ContainsKey("Group B"), Is.True);
        Assert.That(groups["Group A"].Count, Is.EqualTo(2));
    }

    [Test]
    public void ApplyGraphicOverrides_SetColor_OverrideApplied()
    {
        Assert.That(_floorPlan, Is.Not.Null);

        var walls = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_Walls)
            .WhereElementIsNotElementType()
            .ToElements();

        Assert.That(walls.Count, Is.GreaterThan(0));

        using var tx = new Transaction(_doc, "Apply Overrides");
        tx.Start();

        var color = new Color(255, 0, 0);
        var overrides = new OverrideGraphicSettings();
        overrides.SetProjectionLineColor(color);
        overrides.SetSurfaceForegroundPatternColor(color);

        var targetId = walls.First().Id;
        _floorPlan.SetElementOverrides(targetId, overrides);

        tx.Commit();

        var applied = _floorPlan.GetElementOverrides(targetId);
        Assert.That(applied.ProjectionLineColor.Red, Is.EqualTo(255));
        Assert.That(applied.ProjectionLineColor.Green, Is.EqualTo(0));
        Assert.That(applied.ProjectionLineColor.Blue, Is.EqualTo(0));
    }

    [Test]
    public void FindSolidFillPattern_InDocument_PatternFound()
    {
        var solidFillId = ElementId.InvalidElementId;

        var patterns = new FilteredElementCollector(_doc)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>();

        foreach (var patternElement in patterns)
        {
            var pattern = patternElement.GetFillPattern();
            if (pattern.IsSolidFill)
            {
                solidFillId = patternElement.Id;
                break;
            }
        }

        Assert.That(solidFillId, Is.Not.EqualTo(ElementId.InvalidElementId));
    }

    [Test]
    public void CustomColorMapping_ArrayOfColors_MapsCorrectly()
    {
        var paramValues = new List<string> { "Value A", "Value B", "Value C" };
        var customColors = new List<int[]>
        {
            new[] { 255, 0, 0 },
            new[] { 0, 255, 0 },
            new[] { 0, 0, 255 }
        };

        var colorMap = new Dictionary<string, int[]>();
        for (int i = 0; i < paramValues.Count; i++)
        {
            if (i < customColors.Count)
                colorMap[paramValues[i]] = customColors[i];
        }

        Assert.That(colorMap["Value A"], Is.EqualTo(new[] { 255, 0, 0 }));
        Assert.That(colorMap["Value B"], Is.EqualTo(new[] { 0, 255, 0 }));
        Assert.That(colorMap["Value C"], Is.EqualTo(new[] { 0, 0, 255 }));
    }

    [Test]
    public void GradientColorGeneration_BlueToRed_InterpolatesCorrectly()
    {
        var paramValues = new List<string> { "Low", "Mid", "High" };
        int[] startColor = { 0, 0, 180 };
        int[] endColor = { 180, 0, 0 };

        var colorMap = new Dictionary<string, int[]>();
        for (int i = 0; i < paramValues.Count; i++)
        {
            double ratio = (double)i / (paramValues.Count - 1);
            int[] color =
            {
                (int)(startColor[0] + (endColor[0] - startColor[0]) * ratio),
                (int)(startColor[1] + (endColor[1] - startColor[1]) * ratio),
                (int)(startColor[2] + (endColor[2] - startColor[2]) * ratio)
            };
            colorMap[paramValues[i]] = color;
        }

        // First should be blue (0,0,180)
        Assert.That(colorMap["Low"][0], Is.EqualTo(0));
        Assert.That(colorMap["Low"][2], Is.EqualTo(180));

        // Last should be red (180,0,0)
        Assert.That(colorMap["High"][0], Is.EqualTo(180));
        Assert.That(colorMap["High"][2], Is.EqualTo(0));

        // Mid should be interpolated (90,0,90)
        Assert.That(colorMap["Mid"][0], Is.EqualTo(90));
        Assert.That(colorMap["Mid"][2], Is.EqualTo(90));
    }
}
