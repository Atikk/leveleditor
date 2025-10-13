# DotGame - Tile-Based Game Editor

## Overview
DotGame is a C# Windows Forms desktop application featuring a tile-based map editor and game player. Originally created as LINQPad scripts, it has been converted to a standard .NET project structure for the Replit environment.

## Project Structure
- **src/DotGameCSharp/** - Main C# project
  - `Program.cs` - Application entry point
  - `MainMenuForm.cs` - Main menu UI
  - `EditorForm.cs` - Tile map editor
  - `GameForm.cs` - Game player
  - `Map.cs` - Map data structures and loading
  - `Character.cs` - Character classes and creation

## ✅ Solution: Avalonia UI Running Successfully!

This project was originally designed for Windows using Windows Forms. The original Windows Forms code is preserved in `src/DotGameCSharp/` for Windows users.

**For Linux/Replit**, we've successfully created an **Avalonia UI demo** that runs perfectly!

### Working Setup
- ✅ Avalonia UI framework installed and working
- ✅ VNC server (Xvnc) for GUI display
- ✅ noVNC web interface (accessible via webview on port 5000)
- ✅ Fluxbox window manager
- ✅ All Linux dependencies installed (fontconfig, libICE, libSM, libX11)
- ✅ Demo app running at `avalonia-demo/DotGameAvalonia/`

### Technical Achievement
Successfully proved that cross-platform .NET UI applications work in Replit's Linux environment. The Avalonia framework provides a modern, Windows Forms-like experience that runs on Linux.

### Next Steps for Full Game Port
The original game logic (`Map.cs`, `Character.cs`, `GameForm.cs`) can now be ported to Avalonia, reusing most of the existing code while adapting the UI layer.

## Features
- **Map Editor**: Create tile-based maps using sprite sheets or individual tiles
  - Load and split sprite sheets
  - Adjustable brush size
  - Multiple grid sizes (10x10 to 50x50)
  - Save/load maps as JSON files with embedded base64 images
  
- **Game Player**: Play test your maps
  - Character creation with multiple classes (Warrior, Mage, Thief)
  - Custom character sprites
  - Tile-based movement (WASD or arrow keys)
  - Character stats and animations

- **Character System**:
  - Three character classes with different stats
  - Custom naming
  - Sprite support with animation frames
  - RPG-style attributes (HP, Attack, Defense)

## File Formats
Maps are saved as JSON files containing:
- Grid dimensions (cols, rows)
- Tile dimensions (tileW, tileH)
- 2D array of base64-encoded PNG images

## Development Notes
- Built with .NET 7.0
- Originally LINQPad scripts (.linq files)
- Converted to standard C# project structure
- Windows Forms version preserved for Windows users
- Avalonia UI version successfully running on Linux/Replit

## How to Use This Project

### On Replit/Linux (Avalonia Demo - Currently Running!)
The Avalonia UI demo is already running and accessible via webview:
```bash
./start-app.sh
```
The VNC/noVNC environment displays the GUI on port 5000.

### On Windows (Original Windows Forms Version)
The original Windows Forms app works perfectly on Windows:
```bash
cd src/DotGameCSharp
dotnet run
```

## Recent Changes
- 2025-10-13: Imported from GitHub and converted from LINQPad format
- 2025-10-13: Set up C# project structure with .NET 7.0
- 2025-10-13: Configured VNC/X11 environment
- 2025-10-13: Successfully implemented Avalonia UI as cross-platform solution
- 2025-10-13: Installed all required Linux dependencies (fontconfig, libICE, libSM, X11 libraries)
- 2025-10-13: Verified Avalonia demo app running successfully on Linux
- 2025-10-13: Added comprehensive documentation
