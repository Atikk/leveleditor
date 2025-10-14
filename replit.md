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

## ✅ Complete JRPG Game Ready!

Successfully ported the entire game from Windows Forms to Avalonia UI for Linux/Replit with full JRPG features:

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
- ✅ **Enhanced Character System**
  - Character creation with class selection (Warrior, Mage, Thief)
  - Class descriptions and stat previews
  - HP, Attack, and Defense attributes
  - Sprite-based animations with multiple states
- ✅ **Monster/NPC System**
  - Multiple monster types (Slime, Skeleton, Dragon)
  - Monster AI with pathfinding toward player
  - Individual monster stats and behaviors
  - Animation states (Idle, Walk, Attack, Hit, Death)
- ✅ **Turn-Based Combat System**
  - Combat triggers when player meets monster
  - Attack and Defend actions
  - Damage calculation using stats (Attack vs Defense)
  - Combat UI with health bars and messages
  - Victory/defeat conditions
- ✅ **Animation System**
  - Frame-based sprite animations
  - Multiple animation states (Idle, Walk, Attack, Hit, Death)
  - Smooth frame timing (30 FPS)
  - Animation state transitions
- ✅ Map loading from JSON files
- ✅ Game player with tile-based movement (WASD/Arrow keys)
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

### Character System
- **Class Selection**: Choose from Warrior, Mage, or Thief
  - **Warrior**: 30 HP, 5 Attack, 5 Defense - Strong fighter excelling in close combat
  - **Mage**: 20 HP, 7 Attack, 3 Defense - Powerful spellcaster dealing massive damage
  - **Thief**: 25 HP, 6 Attack, 4 Defense - Balanced rogue, quick and versatile
- **Stat System**: HP, Attack, and Defense attributes affect combat
- **Character Customization**: Choose name and optional sprite
- **Animation States**: Idle, Walk, Attack, Hit, and Death animations

### Monster System
- **Multiple Monster Types**:
  - **Slime**: 15 HP, 3 ATK, 2 DEF - Weakest enemy
  - **Skeleton**: 25 HP, 5 ATK, 3 DEF - Moderate threat
  - **Dragon**: 50 HP, 10 ATK, 7 DEF - Powerful boss
- **AI Pathfinding**: Monsters chase the player
- **Random Spawning**: Monsters spawn at random map locations
- **Animations**: Full animation state support

### Combat System
- **Turn-Based Combat**: Triggered when player meets monster
- **Combat Actions**:
  - **Attack**: Deal damage based on Attack stat vs enemy Defense
  - **Defend**: Prepare for enemy attack (reduces damage)
- **Combat UI**: 
  - Health bars for player and enemy
  - Turn-based message display
  - Action buttons (Attack/Defend)
- **Victory/Defeat**: Combat ends when HP reaches 0
- **Damage Calculation**: actualDamage = max(1, Attack - Defense)

### Game Player
- Load maps from JSON files
- Tile-based exploration with WASD or arrow keys
- Real-time monster encounters
- Turn-based combat system
- 30 FPS rendering with smooth animations

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
3. Create your character:
   - Choose a class (Warrior/Mage/Thief) - see descriptions and stats
   - Enter your character name
   - Optionally load a custom sprite
4. **Explore**: Move with WASD or arrow keys
5. **Combat**: Walk into monsters to trigger turn-based battles
   - Click "Attack" to deal damage
   - Click "Defend" to prepare for enemy attacks
   - Defeat enemies to continue exploring
6. Watch your HP - if it reaches 0, you're defeated!
7. Press ESC to exit the game

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
- 2025-10-14: **Complete JRPG System Implementation with All Fixes!**
  - Enhanced animation system with proper state transitions (Idle, Walk, Attack, Hit, Death)
  - Fixed animation rendering: All entities use Draw() with currentAnimation.CurrentFrameRect()
  - Fixed idle transitions: Player and monsters properly switch between Idle/Walk based on movement
  - Fixed directional animations: All 5 states update correctly when direction changes
  - Fixed SetAnimation: Always applies updated animations even when state unchanged
  - Fixed monster AI: DidMoveThisUpdate flag tracks actual movement success
  - Fixed combat damage: CombatManager calculates and applies correct damage values
  - Monster/NPC system with AI pathfinding and animations
  - Turn-based combat with stat-based damage (Attack vs Defense)
  - Combat UI with health bars, messages, and action buttons
  - Character stats system (HP, Attack, Defense) with 3 classes
  - Class descriptions and stat previews in character creation
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
