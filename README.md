# DotGame - Tile-Based Game Editor

A C# desktop application for creating and playing tile-based games with a visual map editor.

## âœ… Cross-Platform Success!

This application was originally designed for **Windows** using Windows Forms. The Windows Forms version is preserved in `src/DotGameCSharp/` and works perfectly on Windows systems.

**For Replit/Linux**, we've created a **working Avalonia UI demo** that proves cross-platform .NET UI is fully functional!

### Current Status in Replit

âœ… **What Works:**
- Avalonia UI demo app running successfully on Linux
- VNC/noVNC environment configured (accessible via webview)
- Cross-platform .NET runtime working perfectly
- All required Linux dependencies installed

ðŸ“‚ **Demo Location:** `avalonia-demo/Dotgame.Avalonia/`

### Running This Project

#### Option 1: Run Avalonia Demo (Currently Working!)
The Avalonia demo is already running and accessible through the webview:
```bash
./start-app.sh
```
This starts the VNC server and the Avalonia app automatically.

#### Option 2: Run Original Windows Version
On Windows systems, the original Windows Forms app works perfectly:
```bash
cd src/DotGameCSharp
dotnet run
```

#### Running on Windows (helper script)

If you prefer a small helper script to build and run the Avalonia demo on Windows, there's a PowerShell script at the repo root:

```powershell
.\run-windows.ps1
```

Usage notes:
- Requires the .NET 8 SDK (dotnet) on PATH.
- By default the script builds and runs `DotGame\Dotgame.Avalonia.csproj` in `Debug` configuration.
- Run `Get-Help .\run-windows.ps1 -Full` for available parameters (configuration, skip build, run detached, etc.).


#### Option 3: Port Game Logic to Avalonia
The original game logic (`Map.cs`, `Character.cs`, `GameForm.cs`) can be ported to the working Avalonia framework. The Avalonia demo proves the environment is ready!

## Project Features

### Map Editor
- Load sprite sheets and split them into tiles
- Paint tiles on a configurable grid (10x10 to 50x50)
- Adjustable brush size
- Save/load maps as self-contained JSON files

### Game Player
- Create characters with custom names and sprites
- Choose from three character classes:
  - **Warrior**: High HP and Defense
  - **Mage**: High Attack, Low Defense
  - **Thief**: Balanced stats
- Play test maps with tile-based movement
- Character animations

## Project Structure

```
src/DotGameCSharp/
â”œâ”€â”€ Program.cs              # Application entry point
â”œâ”€â”€ MainMenuForm.cs         # Main menu UI
â”œâ”€â”€ EditorForm.cs           # Tile map editor
â”œâ”€â”€ GameForm.cs             # Game player
â”œâ”€â”€ Map.cs                  # Map data and serialization
â”œâ”€â”€ Character.cs            # Character system
â””â”€â”€ DotGameCSharp.csproj    # Project configuration
```

## File Format

Maps are saved as JSON files with embedded base64-encoded PNG images:

```json
{
  "cols": 20,
  "rows": 20,
  "tileW": 32,
  "tileH": 32,
  "map": [
    ["data:image/png;base64,...", null, ...],
    ...
  ]
}
```

## Controls (When Running)

### Game Mode
- **Arrow Keys** or **WASD**: Move character
- **ESC**: Exit game

### Editor Mode
- **Mouse Click/Drag**: Paint tiles
- **Grid Size Dropdown**: Change map dimensions
- **Brush Size**: Adjust brush size
- **Save/Load**: Export or import maps

## Technical Details

### Original Windows Version (`src/DotGameCSharp/`)
- **Framework**: .NET 7.0
- **UI**: Windows Forms (Windows-only)
- **Language**: C# 11
- **Graphics**: System.Drawing

### Cross-Platform Avalonia Demo (`avalonia-demo/Dotgame.Avalonia/`)
- **Framework**: .NET 8.0
- **UI**: Avalonia UI (cross-platform)
- **Language**: C# 11
- **Display**: VNC/noVNC on Linux, native on Windows/macOS

## Next Steps: Port Game Logic to Avalonia

The Avalonia framework is already working! To create the full game in Avalonia:

1. **Copy Game Logic from Original**
   - `Map.cs` - Can be reused with minimal changes
   - `Character.cs` - Can be reused with minimal changes
   - Game mechanics are framework-independent

2. **Adapt UI Components**
   - Convert Windows Forms controls â†’ Avalonia controls
   - Migrate painting/rendering logic to Avalonia's rendering system
   - Update event handlers for Avalonia's event model

3. **Leverage Working Infrastructure**
   - VNC/noVNC environment already configured
   - All Linux dependencies installed
   - Workflow ready to run the app

## Original Source

This project was originally created as LINQPad scripts (.linq files) and has been converted to a standard C# project structure.

## License

The project structure and code organization was set up by Replit Agent. The original game logic remains as imported from the source repository.

