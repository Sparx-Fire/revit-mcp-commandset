# revit-mcp-commandset
🔄 Revit-MCP Client | Core implementation of the Revit-MCP protocol that connects LLMs with Revit. Includes essential CRUD commands for Revit elements enabling AI-driven BIM automation.

# Setup

## Prerequisites

Set up the Revit MCP plugin first by following the guide at [revit-mcp-plugin](https://github.com/Sparx-Fire/revit-mcp-plugin).

## Installation

1. Locate the folder containing `revit-mcp-plugin.dll` and find the `Commands` folder inside it. You can also find this folder from within Revit by going to **Add-in Modules > Revit MCP Plugin > Settings** and clicking **OpenCommandSetFolder**.

2. Create a `RevitMCPCommandSet` folder inside `Commands`:
   ```
   Commands/
   └── RevitMCPCommandSet/
       ├── command.json
       └── 20XX/
           └── (compiled output)
   ```

3. Add files:
   - Copy `command.json` from this repo into the `RevitMCPCommandSet` folder
   - Create a `20XX` subfolder matching your Revit version (e.g. `2025`)
   - Place the compiled output from this repo in the `20XX` subfolder

4. In Revit: Go to **Add-ins** > **Settings** > **Refresh** > **Save**

## Important Note

   - Command names must be identical between `revit-mcp` and `revit-mcp-commandset` repositories, otherwise Claude cannot find them.
   - The `commandRegistry.json` is created automatically, do not import it from the installer.