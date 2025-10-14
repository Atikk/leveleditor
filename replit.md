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
- ✅ **Full Map Editor** - Create and edit tile-based maps
  - Load sprite sheets and split into tiles
  - Tile palette with selection
  - Brush painting with size control
  - Save/Load maps to JSON format
  - Text-based file input (Linux compatible)
- ✅ **Sprite Editor** - Create pixel art and sprites
  - Pixel-by-pixel drawing with color palette
  - Multi-frame support for animations
  - Frame navigation (prev/next)
  - Zoom control for detailed editing
  - Save/Load sprites as PNG
  - Custom color picker (RGB values)
- ✅ Map loading from JSON files
- ✅ Character creation (Warrior, Mage, Thief classes)
- ✅ Game player with tile-based movement (WASD/Arrow keys)
- ✅ Character rendering and animation
- ✅ VNC/noVNC GUI environment on port 5000
- ✅ No file dialogs (uses text-based file selection for Linux compatibility)

### Technical Stack
- **Framework**: Avalonia UI 11.0.10 (.NET 8.0)
- **Graphics**: SkiaSharp for image rendering
- **Display**: Xvnc + noVNC web interface
- **Window Manager**: Fluxbox

### Known Limitations
- DBus/GTK file dialogs not available in Linux environment (using text-based input instead)
- Sprite editor saves single frame (multi-frame export planned for future)

## Features

### Map Editor
- **Sprite Sheet Splitting**: Load sprite sheets and automatically split them into individual tiles
- **Tile Palette**: Visual palette of all available tiles for painting
- **Brush Tool**: Paint tiles with adjustable brush size (1-10 pixels)
- **Grid System**: Choose grid size (10x10 to 50x50)
- **Save/Load**: Export maps to JSON format and reload them for editing
- **Text Input**: Linux-compatible file selection via text input

### Sprite Editor
- **Pixel Canvas**: Draw pixel-by-pixel with zoom control (1x-20x)
- **Color Palette**: 32 preset colors plus custom RGB color picker
- **Multi-Frame**: Create animations with frame navigation
- **Grid Toggle**: Show/hide pixel grid for precision drawing
- **Save/Load**: Export sprites as PNG and reload for editing
- **Draw & Erase**: Left-click to draw, right-click to erase

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
2. The DotGame main menu will appear with options:
   - **Map Editor**: Create and edit tile-based maps
   - **Sprite Editor**: Draw pixel art and sprites  
   - **Test Map**: Play an existing map
   - **Create Character**: Design your character

#### Using the Map Editor
1. Click "Map Editor" from main menu
2. Load a sprite sheet or individual tiles via text path input
3. Split sprite sheets by setting tile dimensions
4. Select tiles from palette and paint on the grid
5. Adjust brush size and grid size as needed
6. Save your map to JSON format (e.g., `maps/mymap.json`)

#### Using the Sprite Editor
1. Click "Sprite Editor" from main menu
2. Choose canvas size and zoom level
3. Select colors from palette or use custom RGB values
4. Draw pixels with left-click, erase with right-click
5. Use frame navigation to create animations
6. Save sprites as PNG (e.g., `sprites/mysprite.png`)

#### Playing the Game
1. Click "Test Map" to play
2. Select a map file path or choose from samples
3. Create your character (Warrior/Mage/Thief)
4. Play the game with WASD or arrow keys

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
- 2025-10-14: **Added full Map Editor and Sprite Editor!**
  - Map Editor: Sprite sheet splitting, tile palette, brush tool, save/load JSON
  - Sprite Editor: Pixel drawing, multi-frame support, color palette, save/load PNG
  - Both editors integrated into main menu
- 2025-10-14: Completed Avalonia UI port of DotGame
- 2025-10-14: Ported Map.cs and Character.cs to use SkiaSharp
- 2025-10-14: Created Avalonia windows for MainMenu, Game, and Character Creation
- 2025-10-14: Implemented text-based file selection (no file dialogs)
- 2025-10-14: Added sample map file for testing
- 2025-10-14: Successfully running on VNC/noVNC environment
- 2025-10-13: Initial Avalonia demo and environment setup
