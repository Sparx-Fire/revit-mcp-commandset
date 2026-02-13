# revit-mcp-commandset
🔄 Revit-MCP Client | Core implementation of the Revit-MCP protocol that connects LLMs with Revit. Includes essential CRUD commands for Revit elements enabling AI-driven BIM automation.

# Custom Commands Setup

## Installation

1. Create folder: `RevitMCPCommandSet` at the end of the usual Revit addins directory like so `C:\Users\[USERNAME]\AppData\Roaming\Autodesk\Revit\Addins\20XX\RevitMCPCommandSet\`

2. Add files:
   - Copy `command.json` from this repo to the `RevitMCPCommandSet` folder
   - Create `20XX` subfolder
   - Place compiled output from this repo in the `20XX` subfolder

3. In Revit: Go to **Add-ins** > **Settings** > **Refresh** > **Save**

## Testing

The test project uses [Nice3point.TUnit.Revit](https://github.com/Nice3point/RevitUnit) to run integration tests against a live Revit instance. No separate addin installation is required — the framework injects into the running Revit process automatically.

### Prerequisites

- **.NET 10 SDK** — required by Nice3point.Revit.Sdk 6.1.0. Install via `winget install Microsoft.DotNet.SDK.10`
- **Autodesk Revit 2026** (or 2025) — must be installed and licensed on your machine

### Running Tests

1. Open Revit 2026 (or 2025) and wait for it to fully load
2. Run the tests from the command line:

```bash
# For Revit 2026
dotnet test -c Debug.R26 --project revit-mcp-commandset.Tests -r win-x64

# For Revit 2025
dotnet test -c Debug.R25 --project revit-mcp-commandset.Tests -r win-x64
```

> **Note:** The `-r win-x64` flag is required on ARM64 machines because the Revit API assemblies are x64-only.

Alternatively, you can use `dotnet run`:

```bash
cd revit-mcp-commandset.Tests
dotnet run -c Debug.R26
```

### IDE Support

- **JetBrains Rider** — enable "Testing Platform support" in Settings > Build, Execution, Deployment > Unit Testing > Testing Platform
- **Visual Studio** — tests should be discoverable through the standard Test Explorer

### Project Structure

| File | Purpose |
|------|---------|
| `revit-mcp-commandset.Tests/AssemblyInfo.cs` | Global `[assembly: TestExecutor<RevitThreadExecutor>]` registration |
| `revit-mcp-commandset.Tests/Architecture/` | Tests for level and room creation commands |
| `revit-mcp-commandset.Tests/DataExtraction/` | Tests for model statistics, room data export, and material quantities |
| `revit-mcp-commandset.Tests/ColorSplashTests.cs` | Tests for color override functionality |
| `revit-mcp-commandset.Tests/TagRoomsTests.cs` | Tests for room tagging functionality |

### Writing New Tests

Test classes inherit from `RevitApiTest` and use TUnit's async assertion API:

```csharp
public class MyTests : RevitApiTest
{
    private static Document _doc;

    [Before(HookType.Class)]
    [HookExecutor<RevitThreadExecutor>]
    public static void Setup()
    {
        _doc = Application.NewProjectDocument(UnitSystem.Imperial);
    }

    [After(HookType.Class)]
    [HookExecutor<RevitThreadExecutor>]
    public static void Cleanup()
    {
        _doc?.Close(false);
    }

    [Test]
    public async Task MyTest_Condition_ExpectedResult()
    {
        var elements = new FilteredElementCollector(_doc)
            .WhereElementIsNotElementType()
            .ToElements();

        await Assert.That(elements.Count).IsGreaterThan(0);
    }
}
```

## Important Note

   - Command names must be identical between `revit-mcp` and `revit-mcp-commandset` repositories, otherwise Claude cannot find them.
   - The `commandRegistry.json` is created automatically, do not import it from the installer.