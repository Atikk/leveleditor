# MapPassabilityTool

Small dotnet console tool to validate and optionally inject a default `passability` jagged bool[][] into `maps/*.json` files.

Usage (dry-run):

  dotnet run --project tools/MapPassabilityTool/MapPassabilityTool.csproj -- --maps-dir maps

To actually write changes (creates a `.bak` backup for each file it modifies):

  dotnet run --project tools/MapPassabilityTool/MapPassabilityTool.csproj -- --maps-dir maps --apply

Options:
- `--maps-dir <path>`: directory containing `*.json` map files (default: `maps`)
- `--apply`: if provided, the tool will write changes and create `.bak` backups. Otherwise it runs in dry-run mode.

The tool validates that `rows` and `cols` exist and that `passability` (if present) is a jagged array matching those dimensions.
