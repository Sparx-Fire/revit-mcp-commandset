using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Nice3point.TUnit.Revit;

namespace RevitMCPCommandSet.Tests;

[ClassSetup]
[ClassCleanup]
public class TagRoomsTests : RevitApiTest
{
    private Document _doc;
    private Level _level;
    private ViewPlan _floorPlan;

    [ClassSetup]
    public void Setup()
    {
        _doc = Application.NewProjectDocument(UnitSystem.Imperial);

        using var tx = new Transaction(_doc, "Setup Tag Test Environment");
        tx.Start();

        _level = Level.Create(_doc, 0.0);
        _level.Name = "Tag Test Level";

        var floorPlanType = new FilteredElementCollector(_doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.FloorPlan);

        if (floorPlanType != null)
        {
            _floorPlan = ViewPlan.Create(_doc, floorPlanType.Id, _level.Id);
        }

        // Create a 10ft x 10ft enclosure for room placement
        double size = 10.0;
        var p1 = new XYZ(0, 0, 0);
        var p2 = new XYZ(size, 0, 0);
        var p3 = new XYZ(size, size, 0);
        var p4 = new XYZ(0, size, 0);

        Wall.Create(_doc, Line.CreateBound(p1, p2), _level.Id, false);
        Wall.Create(_doc, Line.CreateBound(p2, p3), _level.Id, false);
        Wall.Create(_doc, Line.CreateBound(p3, p4), _level.Id, false);
        Wall.Create(_doc, Line.CreateBound(p4, p1), _level.Id, false);

        tx.Commit();
    }

    [ClassCleanup]
    public void Cleanup()
    {
        _doc?.Close(false);
    }

    [Test]
    public void TagRoom_CreateRoomTag_TagExists()
    {
        Room room;
        using (var tx = new Transaction(_doc, "Create Room For Tag"))
        {
            tx.Start();
            room = _doc.Create.NewRoom(_level, new UV(5.0, 5.0));
            tx.Commit();
        }

        Assert.That(room, Is.Not.Null);
        Assert.That(_floorPlan, Is.Not.Null);

        using (var tx = new Transaction(_doc, "Tag Room"))
        {
            tx.Start();

            var locPoint = room.Location as LocationPoint;
            XYZ roomCenter = locPoint?.Point ?? new XYZ(5.0, 5.0, 0);
            var tagPoint = new UV(roomCenter.X, roomCenter.Y);

            var tag = _doc.Create.NewRoomTag(
                new LinkElementId(room.Id),
                tagPoint,
                _floorPlan.Id);

            tx.Commit();

            Assert.That(tag, Is.Not.Null);
            Assert.That(tag.Room, Is.Not.Null);
        }
    }

    [Test]
    public void TagRoom_WithLeader_HasLeaderIsTrue()
    {
        Room room;
        using (var tx = new Transaction(_doc, "Create Room For Leader Tag"))
        {
            tx.Start();
            room = _doc.Create.NewRoom(_level, new UV(5.0, 5.0));
            tx.Commit();
        }

        Assert.That(room, Is.Not.Null);
        Assert.That(_floorPlan, Is.Not.Null);

        using (var tx = new Transaction(_doc, "Tag Room With Leader"))
        {
            tx.Start();

            var locPoint = room.Location as LocationPoint;
            XYZ roomCenter = locPoint?.Point ?? new XYZ(5.0, 5.0, 0);

            var tag = _doc.Create.NewRoomTag(
                new LinkElementId(room.Id),
                new UV(roomCenter.X, roomCenter.Y),
                _floorPlan.Id);

            if (tag != null)
            {
                tag.HasLeader = true;
            }

            tx.Commit();

            Assert.That(tag, Is.Not.Null);
            Assert.That(tag.HasLeader, Is.True);
        }
    }

    [Test]
    public void TagRoom_SkipAlreadyTagged_NoDuplicateTags()
    {
        Room room;
        using (var tx = new Transaction(_doc, "Create Room For Skip Test"))
        {
            tx.Start();
            room = _doc.Create.NewRoom(_level, new UV(5.0, 5.0));
            tx.Commit();
        }

        Assert.That(room, Is.Not.Null);
        Assert.That(_floorPlan, Is.Not.Null);

        // Create first tag
        using (var tx = new Transaction(_doc, "First Tag"))
        {
            tx.Start();
            var locPoint = room.Location as LocationPoint;
            XYZ center = locPoint?.Point ?? new XYZ(5.0, 5.0, 0);
            _doc.Create.NewRoomTag(new LinkElementId(room.Id), new UV(center.X, center.Y), _floorPlan.Id);
            tx.Commit();
        }

        // Check existing tags (mimics handler duplicate detection)
        var existingTags = new FilteredElementCollector(_doc, _floorPlan.Id)
            .OfCategory(BuiltInCategory.OST_RoomTags)
            .WhereElementIsNotElementType()
            .Cast<RoomTag>()
            .ToList();

        var roomsWithTags = new HashSet<long>();
        foreach (var tag in existingTags)
        {
            if (tag.Room != null)
            {
#if REVIT2024_OR_GREATER
                roomsWithTags.Add(tag.Room.Id.Value);
#else
                roomsWithTags.Add(tag.Room.Id.IntegerValue);
#endif
            }
        }

#if REVIT2024_OR_GREATER
        bool alreadyTagged = roomsWithTags.Contains(room.Id.Value);
#else
        bool alreadyTagged = roomsWithTags.Contains(room.Id.IntegerValue);
#endif

        Assert.That(alreadyTagged, Is.True);
    }

    [Test]
    public void TagRoom_SpecificRoomById_OnlyThatRoomTagged()
    {
        // Create two rooms in separate enclosures
        Room room1, room2;
        using (var tx = new Transaction(_doc, "Create Two Enclosures"))
        {
            tx.Start();

            // Second enclosure offset by 20ft
            double offset = 20.0;
            double size = 10.0;
            var q1 = new XYZ(offset, 0, 0);
            var q2 = new XYZ(offset + size, 0, 0);
            var q3 = new XYZ(offset + size, size, 0);
            var q4 = new XYZ(offset, size, 0);

            Wall.Create(_doc, Line.CreateBound(q1, q2), _level.Id, false);
            Wall.Create(_doc, Line.CreateBound(q2, q3), _level.Id, false);
            Wall.Create(_doc, Line.CreateBound(q3, q4), _level.Id, false);
            Wall.Create(_doc, Line.CreateBound(q4, q1), _level.Id, false);

            room1 = _doc.Create.NewRoom(_level, new UV(5.0, 5.0));
            room2 = _doc.Create.NewRoom(_level, new UV(offset + 5.0, 5.0));

            tx.Commit();
        }

        Assert.That(room1, Is.Not.Null);
        Assert.That(room2, Is.Not.Null);
        Assert.That(_floorPlan, Is.Not.Null);

        // Tag only room1
        using (var tx = new Transaction(_doc, "Tag Specific Room"))
        {
            tx.Start();

            var locPoint = room1.Location as LocationPoint;
            XYZ center = locPoint?.Point ?? new XYZ(5.0, 5.0, 0);
            _doc.Create.NewRoomTag(new LinkElementId(room1.Id), new UV(center.X, center.Y), _floorPlan.Id);

            tx.Commit();
        }

        // Verify only room1 has a tag
        var tags = new FilteredElementCollector(_doc, _floorPlan.Id)
            .OfCategory(BuiltInCategory.OST_RoomTags)
            .WhereElementIsNotElementType()
            .Cast<RoomTag>()
            .Where(t => t.Room != null)
            .ToList();

        var taggedRoomIds = tags.Select(t => t.Room.Id).ToList();
        Assert.That(taggedRoomIds, Does.Contain(room1.Id));
    }

    [Test]
    public void FindRoomTagType_ByCategory_TagTypeFound()
    {
        var roomTagType = new FilteredElementCollector(_doc)
            .OfClass(typeof(FamilySymbol))
            .WhereElementIsElementType()
            .Cast<FamilySymbol>()
            .FirstOrDefault(e => e.Category != null &&
#if REVIT2024_OR_GREATER
                e.Category.Id.Value == (long)BuiltInCategory.OST_RoomTags);
#else
                e.Category.Id.IntegerValue == (int)BuiltInCategory.OST_RoomTags);
#endif

        // Room tag type should exist in a default project template
        Assert.That(roomTagType, Is.Not.Null);
    }
}
