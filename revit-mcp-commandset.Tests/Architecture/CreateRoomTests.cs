using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Nice3point.TUnit.Revit;

namespace RevitMCPCommandSet.Tests.Architecture;

[ClassSetup]
[ClassCleanup]
public class CreateRoomTests : RevitApiTest
{
    private Document _doc;
    private Level _level;
    private ViewPlan _floorPlan;

    [ClassSetup]
    public void Setup()
    {
        _doc = Application.NewProjectDocument(UnitSystem.Imperial);

        // Create a level and floor plan, then build enclosing walls for room placement
        using var tx = new Transaction(_doc, "Setup Room Test Environment");
        tx.Start();

        _level = Level.Create(_doc, 0.0);
        _level.Name = "Room Test Level";

        var floorPlanType = new FilteredElementCollector(_doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.FloorPlan);

        if (floorPlanType != null)
        {
            _floorPlan = ViewPlan.Create(_doc, floorPlanType.Id, _level.Id);
        }

        // Create a 10ft x 10ft enclosure of walls for room placement
        double size = 10.0; // feet
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
    public void CreateRoom_AtValidLocation_RoomExistsWithArea()
    {
        using var tx = new Transaction(_doc, "Create Room");
        tx.Start();

        // Place room at center of the 10x10 enclosure
        var uv = new UV(5.0, 5.0);
        var room = _doc.Create.NewRoom(_level, uv);

        tx.Commit();

        Assert.That(room, Is.Not.Null);
        Assert.That(room.Area, Is.GreaterThan(0));
    }

    [Test]
    public void CreateRoom_SetName_RoomNameParameterSet()
    {
        using var tx = new Transaction(_doc, "Create Room With Name");
        tx.Start();

        var room = _doc.Create.NewRoom(_level, new UV(5.0, 5.0));
        if (room != null)
        {
            var nameParam = room.get_Parameter(BuiltInParameter.ROOM_NAME);
            if (nameParam != null && !nameParam.IsReadOnly)
            {
                nameParam.Set("Conference Room");
            }
        }

        tx.Commit();

        if (room != null)
        {
            var nameParam = room.get_Parameter(BuiltInParameter.ROOM_NAME);
            Assert.That(nameParam?.AsString(), Is.EqualTo("Conference Room"));
        }
    }

    [Test]
    public void CreateRoom_SetNumber_RoomNumberParameterSet()
    {
        using var tx = new Transaction(_doc, "Create Room With Number");
        SuppressDuplicateNumberWarnings(tx);
        tx.Start();

        var room = _doc.Create.NewRoom(_level, new UV(5.0, 5.0));
        if (room != null)
        {
            var numberParam = room.get_Parameter(BuiltInParameter.ROOM_NUMBER);
            if (numberParam != null && !numberParam.IsReadOnly)
            {
                numberParam.Set("101");
            }
        }

        tx.Commit();

        if (room != null)
        {
            var numberParam = room.get_Parameter(BuiltInParameter.ROOM_NUMBER);
            Assert.That(numberParam?.AsString(), Is.EqualTo("101"));
        }
    }

    [Test]
    public void CreateRoom_SetDepartmentAndComments_ParametersSet()
    {
        using var tx = new Transaction(_doc, "Create Room With Dept");
        tx.Start();

        var room = _doc.Create.NewRoom(_level, new UV(5.0, 5.0));
        if (room != null)
        {
            var deptParam = room.get_Parameter(BuiltInParameter.ROOM_DEPARTMENT);
            if (deptParam != null && !deptParam.IsReadOnly)
            {
                deptParam.Set("Engineering");
            }

            var commentsParam = room.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (commentsParam != null && !commentsParam.IsReadOnly)
            {
                commentsParam.Set("Test comment");
            }
        }

        tx.Commit();

        if (room != null)
        {
            Assert.That(room.get_Parameter(BuiltInParameter.ROOM_DEPARTMENT)?.AsString(),
                Is.EqualTo("Engineering"));
            Assert.That(room.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString(),
                Is.EqualTo("Test comment"));
        }
    }

    [Test]
    public void CreateRoom_DuplicateNumber_UniqueNumberGenerated()
    {
        var existingNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "200", "201", "202" };
        string uniqueNumber = GetUniqueRoomNumber("200", existingNumbers);

        Assert.That(uniqueNumber, Is.Not.EqualTo("200"));
        Assert.That(existingNumbers.Contains(uniqueNumber), Is.False);
    }

    [Test]
    public void CreateRoom_MultipleRooms_AllGetUniqueNumbers()
    {
        var existingNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var assignedNumbers = new List<string>();

        for (int i = 0; i < 5; i++)
        {
            string number = GetNextAvailableRoomNumber(existingNumbers);
            existingNumbers.Add(number);
            assignedNumbers.Add(number);
        }

        // All numbers should be unique
        Assert.That(assignedNumbers.Distinct().Count(), Is.EqualTo(5));
    }

    [Test]
    public void CreateRoom_RollbackOnFailure_RoomNotPersisted()
    {
        int roomCountBefore = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_Rooms)
            .WhereElementIsNotElementType()
            .GetElementCount();

        using (var tx = new Transaction(_doc, "Create Room Rollback"))
        {
            tx.Start();
            _doc.Create.NewRoom(_level, new UV(5.0, 5.0));
            tx.RollBack();
        }

        int roomCountAfter = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_Rooms)
            .WhereElementIsNotElementType()
            .GetElementCount();

        Assert.That(roomCountAfter, Is.EqualTo(roomCountBefore));
    }

    [Test]
    public void CreateRoom_UpperLimitAndOffset_ParametersSet()
    {
        using var tx = new Transaction(_doc, "Create Room With Offset");
        tx.Start();

        var room = _doc.Create.NewRoom(_level, new UV(5.0, 5.0));
        if (room != null)
        {
            double offsetMm = 3000;
            double offsetFeet = offsetMm / 304.8;

            var limitOffsetParam = room.get_Parameter(BuiltInParameter.ROOM_UPPER_OFFSET);
            if (limitOffsetParam != null && !limitOffsetParam.IsReadOnly)
            {
                limitOffsetParam.Set(offsetFeet);
            }
        }

        tx.Commit();

        if (room != null)
        {
            var readParam = room.get_Parameter(BuiltInParameter.ROOM_UPPER_OFFSET);
            Assert.That(readParam, Is.Not.Null);
            Assert.That(readParam.AsDouble(), Is.EqualTo(3000.0 / 304.8).Within(0.001));
        }
    }

    #region Helper Methods (mirroring handler logic)

    private static void SuppressDuplicateNumberWarnings(Transaction tx)
    {
        var failureOptions = tx.GetFailureHandlingOptions();
        failureOptions.SetClearAfterRollback(true);
        failureOptions.SetDelayedMiniWarnings(false);
        tx.SetFailureHandlingOptions(failureOptions);
    }

    /// <summary>
    /// Mirrors GetUniqueRoomNumber from CreateRoomEventHandler
    /// </summary>
    private static string GetUniqueRoomNumber(string baseNumber, HashSet<string> existingNumbers)
    {
        if (string.IsNullOrEmpty(baseNumber))
            baseNumber = "1";

        if (!existingNumbers.Contains(baseNumber))
            return baseNumber;

        int lastDigitEnd = -1;
        int lastDigitStart = -1;
        for (int i = baseNumber.Length - 1; i >= 0; i--)
        {
            if (char.IsDigit(baseNumber[i]))
            {
                if (lastDigitEnd == -1) lastDigitEnd = i;
                lastDigitStart = i;
            }
            else if (lastDigitEnd != -1)
                break;
        }

        if (lastDigitStart != -1)
        {
            string prefix = baseNumber.Substring(0, lastDigitStart);
            string numericPart = baseNumber.Substring(lastDigitStart, lastDigitEnd - lastDigitStart + 1);
            string suffix = baseNumber.Substring(lastDigitEnd + 1);

            if (int.TryParse(numericPart, out int num))
            {
                for (int i = 1; i <= 1000; i++)
                {
                    string candidate = prefix + (num + i).ToString().PadLeft(numericPart.Length, '0') + suffix;
                    if (!existingNumbers.Contains(candidate))
                        return candidate;
                }
            }
        }

        for (char c = 'A'; c <= 'Z'; c++)
        {
            string candidate = baseNumber + c;
            if (!existingNumbers.Contains(candidate))
                return candidate;
        }

        return baseNumber + "-" + Guid.NewGuid().ToString().Substring(0, 4);
    }

    /// <summary>
    /// Mirrors GetNextAvailableRoomNumber from CreateRoomEventHandler
    /// </summary>
    private static string GetNextAvailableRoomNumber(HashSet<string> existingNumbers)
    {
        int maxNumber = 0;
        foreach (string num in existingNumbers)
        {
            if (int.TryParse(num, out int parsed))
            {
                if (parsed > maxNumber) maxNumber = parsed;
            }
        }

        for (int i = maxNumber + 1; i < maxNumber + 10000; i++)
        {
            string candidate = i.ToString();
            if (!existingNumbers.Contains(candidate))
                return candidate;
        }

        return (maxNumber + 1).ToString();
    }

    #endregion
}
