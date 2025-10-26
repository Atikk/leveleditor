# AssetExporter

Prototype tool to extract embedded base64 data-URL tiles from `maps/*.json` and write them to `assets/tilesets/<mapname>/`.

Usage (dry-run, reports findings but does not write files):

  dotnet run --project tools/AssetExporter/AssetExporter.csproj -- --maps-dir maps

To actually write extracted files and update maps (creates `.bak` backups):

  dotnet run --project tools/AssetExporter/AssetExporter.csproj -- --maps-dir maps --apply

Behavior:
- Scans `map` property (expected array of rows, each row an array of strings).
- Detects entries whose value starts with `data:` and treats them as data URLs.
- In `--apply` mode: extracts to `assets/tilesets/<mapname>/tile_<row>_<col>.<ext>` and replaces the map cell with that relative path.

This is a prototype to be refined; it intentionally keeps behavior simple and auditable (creates `.bak` before writing).
