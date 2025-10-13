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

## Important Note: Windows Forms Limitations on Linux

This project was originally designed for Windows using Windows Forms. While the .NET SDK on Linux can compile Windows Forms projects with `EnableWindowsTargeting`, **running Windows Forms applications on Linux requires the Windows Desktop runtime which is not available in this environment**.

### Current Setup
The project has been configured with:
- VNC server (Xvnc) for GUI support
- noVNC web interface (accessible via browser on port 5000)
- Fluxbox window manager

### Known Issue
The application cannot run because:
```
Framework: 'Microsoft.WindowsDesktop.App', version '7.0.0' (x64)
```
This framework is Windows-only and not available on Linux.

### Possible Solutions
1. **Port to Avalonia UI** - Cross-platform .NET UI framework that works on Linux
2. **Port to ASP.NET Blazor** - Convert to a web application
3. **Use Mono with System.Windows.Forms** - Limited Windows Forms support on Linux
4. **Run on Windows** - The application works as-is on Windows systems

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
- Requires Windows or Windows compatibility layer to run

## Recent Changes
- 2025-10-13: Converted from LINQPad format to standard C# project
- 2025-10-13: Set up VNC/X11 environment for Replit (limited functionality)
