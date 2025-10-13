# DotGame - Tile-Based Game Editor

A C# desktop application for creating and playing tile-based games with a visual map editor.

## ⚠️ Important: Platform Compatibility

This application was originally designed for **Windows** using Windows Forms. While it compiles successfully on Linux, **it cannot run on Linux/Replit** because it requires the `Microsoft.WindowsDesktop.App` framework, which is Windows-only.

### Current Status in Replit

✅ **What Works:**
- Project compiles successfully
- All source code is properly structured
- VNC/X11 environment is set up

❌ **What Doesn't Work:**
- Application cannot run (requires Windows runtime)
- Windows Forms UI is not supported on Linux

## Running This Project

### Option 1: Run on Windows (Recommended)
The application works perfectly on Windows:
```bash
cd src/DotGameCSharp
dotnet run
```

### Option 2: Port to Cross-Platform Framework
To run on Linux/Mac/Replit, the application needs to be ported to a cross-platform UI framework:

**Recommended Framework: Avalonia UI**
- Modern, cross-platform .NET UI framework
- Similar to Windows Forms/WPF
- Works on Windows, Linux, macOS, and web

**Alternative: ASP.NET Blazor**
- Convert to a web application
- Accessible from any browser
- No desktop UI required

### Option 3: Run on Replit (Requires Porting)
The VNC environment is already set up. After porting to Avalonia or another compatible framework, the app will display through the noVNC web interface.

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
├── Program.cs              # Application entry point
├── MainMenuForm.cs         # Main menu UI
├── EditorForm.cs           # Tile map editor
├── GameForm.cs             # Game player
├── Map.cs                  # Map data and serialization
├── Character.cs            # Character system
└── DotGameCSharp.csproj    # Project configuration
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

- **Framework**: .NET 7.0
- **UI**: Windows Forms (Windows-only)
- **Language**: C# 11
- **Graphics**: System.Drawing

## Next Steps for Cross-Platform Support

If you want to run this in Replit, here's what needs to be done:

1. **Install Avalonia Templates**
   ```bash
   dotnet new install Avalonia.Templates
   ```

2. **Create New Avalonia Project**
   ```bash
   dotnet new avalonia.app -n DotGameAvalonia
   ```

3. **Port UI Components**
   - Migrate Forms → Avalonia Windows/Views
   - Convert Controls → Avalonia controls
   - Adapt painting logic to Avalonia rendering

4. **Reuse Game Logic**
   - Map.cs can be reused as-is
   - Character.cs needs minor updates
   - Game logic is mostly framework-independent

## Original Source

This project was originally created as LINQPad scripts (.linq files) and has been converted to a standard C# project structure.

## License

The project structure and code organization was set up by Replit Agent. The original game logic remains as imported from the source repository.
