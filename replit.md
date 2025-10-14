# DotGame - Tile-Based Game Editor

## Overview
DotGame is a C# tile-based map editor and game player application. Originally created as Windows Forms LINQPad scripts, it has been successfully ported to Avalonia UI for cross-platform support on Linux/Replit.

## Project Structure
- **src/DotGameCSharp/** - Original Windows Forms version
  - `Program.cs` - Application entry point
  - `MainMenuForm.cs` - Main menu UI
  - `EditorForm.cs` - Tile map editor
  - `GameForm.cs` - Game player
  - `Map.cs` - Map data structures and loading
  - `Character.cs` - Character classes and creation

- **DotGame/** - Avalonia UI version (Linux/cross-platform)
  - `Models/Map.cs` - Map loading with Avalonia/SkiaSharp
  - `Models/Character.cs` - Character system
  - `Views/MainMenuWindow.axaml` - Main menu
  - `Views/GameWindow.axaml` - Game player
  - `Views/CharacterCreationWindow.axaml` - Character creation
  - `Views/EditorWindow.axaml` - Map editor (placeholder)
  - `Views/MapSelectorWindow.axaml` - Map file selector

## ✅ Avalonia UI Port Complete!

Successfully ported the entire game from Windows Forms to Avalonia UI for Linux/Replit:

### Working Features
- ✅ Main menu with navigation
- ✅ Map loading from JSON files
- ✅ Character creation (Warrior, Mage, Thief classes)
- ✅ Game player with tile-based movement (WASD/Arrow keys)
- ✅ Character rendering and animation
- ✅ VNC/noVNC GUI environment on port 5000
- ✅ No file dialogs (uses text-based map selection for Linux compatibility)

### Technical Stack
- **Framework**: Avalonia UI 11.0.10 (.NET 8.0)
- **Graphics**: SkiaSharp for image rendering
- **Display**: Xvnc + noVNC web interface
- **Window Manager**: Fluxbox

### Known Limitations
- Map editor is a placeholder (file dialog dependency)
- Character sprite loading disabled (file dialog dependency)
- DBus/GTK file dialogs not available in Linux environment

## Features

### Game Player
- Load maps from JSON files
- Character creation with three classes:
  - **Warrior**: High HP and Defense
  - **Mage**: High Attack, low Defense
  - **Thief**: Balanced stats
- Tile-based movement with WASD or arrow keys
- Character animations and direction changes
- 30 FPS rendering

### Map Format
Maps are JSON files containing:
- Grid dimensions (cols, rows)
- Tile dimensions (tileW, tileH)
- 2D array of base64-encoded PNG tiles
- Sample map available in `/home/runner/workspace/maps/sample.json`

## How to Use

### On Replit/Linux (Avalonia Version - Running Now!)
The game is accessible via VNC on port 5000:
```bash
./start-app.sh
```
1. Click "Connect" in the noVNC interface
2. The DotGame main menu will appear
3. Click "Test Map" to play
4. Select a map file path or choose from samples
5. Create your character (Warrior/Mage/Thief)
6. Play the game with WASD or arrow keys

### On Windows (Original Windows Forms)
The original version works on Windows:
```bash
cd src/DotGameCSharp
dotnet run
```

## Development Notes
- Ported from Windows Forms to Avalonia UI for cross-platform support
- Adapted System.Drawing types to Avalonia.Media and SkiaSharp
- Removed file dialog dependencies for Linux compatibility
- Map and Character logic largely reused from original code
- Built with .NET 8.0 for security and modern framework support

## Recent Changes
- 2025-10-14: Completed Avalonia UI port of DotGame
- 2025-10-14: Ported Map.cs and Character.cs to use SkiaSharp
- 2025-10-14: Created Avalonia windows for MainMenu, Game, and Character Creation
- 2025-10-14: Implemented text-based map selection (no file dialogs)
- 2025-10-14: Added sample map file for testing
- 2025-10-14: Successfully running on VNC/noVNC environment
- 2025-10-13: Initial Avalonia demo and environment setup
