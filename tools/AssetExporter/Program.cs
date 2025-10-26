using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;

internal class Program
{
    private static int Main(string[] args)
    {
        string mapsDir = "maps";
        bool apply = args.Contains("--apply");

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--maps-dir" && i + 1 < args.Length)
            {
                mapsDir = args[i + 1];
                i++;
            }
        }

        Console.WriteLine($"AssetExporter: mapsDir='{mapsDir}' apply={apply}");

        if (!Directory.Exists(mapsDir))
        {
            Console.Error.WriteLine($"Error: maps directory not found: {mapsDir}");
            return 2;
        }

        var files = Directory.GetFiles(mapsDir, "*.json", SearchOption.TopDirectoryOnly).OrderBy(x => x).ToArray();
        if (files.Length == 0)
        {
            Console.WriteLine("No .json files found in maps directory.");
            return 0;
        }

            int extracted = 0;
            int errors = 0;

            // dedupe map: map tile content hash -> relative output path
            var dedupe = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            Console.WriteLine($"\nProcessing: {file}");
            string text;
            try { text = File.ReadAllText(file); } catch (Exception ex) { Console.Error.WriteLine($"  Failed to read file: {ex.Message}"); errors++; continue; }

            JsonNode? root;
            try { root = JsonNode.Parse(text); } catch (Exception ex) { Console.Error.WriteLine($"  Invalid JSON: {ex.Message}"); errors++; continue; }
            if (root == null) { Console.Error.WriteLine("  Empty JSON document"); errors++; continue; }

            var mapNode = root["map"];
            if (mapNode == null)
            {
                Console.WriteLine("  map: MISSING");
                continue;
            }

            if (mapNode is not JsonArray rows)
            {
                Console.Error.WriteLine("  map malformed: expected array of rows"); errors++; continue;
            }

            var mapBaseName = Path.GetFileNameWithoutExtension(file);
            var outDir = Path.Combine("assets", "tilesets", mapBaseName);

            int rowIdx = 0;
            bool fileTouched = false;

            foreach (var rowNode in rows)
            {
                if (rowNode is not JsonArray cols) { rowIdx++; continue; }
                int colIdx = 0;
                foreach (var col in cols)
                {
                    if (col is JsonValue val && val.TryGetValue<string>(out var s) && s != null && s.StartsWith("data:"))
                    {
                        // parse data URL: data:[<mediatype>][;base64],<data>
                        try
                        {
                            var comma = s.IndexOf(',');
                            if (comma < 0) throw new InvalidOperationException("invalid data URL");
                            var meta = s.Substring(5, comma - 5);
                            var body = s.Substring(comma + 1);
                            var isBase64 = meta.Contains("base64");
                            var mime = meta.Split(';')[0];
                            var ext = MimeToExt(mime) ?? "bin";

                            Console.WriteLine($"  Found embedded asset at row={rowIdx} col={colIdx} mime={mime} base64={isBase64}");

                            if (apply)
                            {
                                Directory.CreateDirectory(outDir);
                                byte[] bytes = isBase64 ? Convert.FromBase64String(body) : Encoding.UTF8.GetBytes(Uri.UnescapeDataString(body));

                                // compute hash for deduplication
                                string hash;
                                using (var sha = SHA256.Create())
                                {
                                    var h = sha.ComputeHash(bytes);
                                    hash = BitConverter.ToString(h).Replace("-", "").ToLowerInvariant().Substring(0, 16);
                                }

                                if (dedupe.TryGetValue(hash, out var existingRelative))
                                {
                                    // reuse existing file
                                    cols[colIdx] = JsonValue.Create(existingRelative);
                                    Console.WriteLine($"    Reused existing extracted file for hash {hash}");
                                }
                                else
                                {
                                    var filename = $"tile_{hash}.{ext}";
                                    var dest = Path.Combine(outDir, filename);
                                    File.WriteAllBytes(dest, bytes);
                                    var rel = Path.Combine("assets", "tilesets", mapBaseName, filename).Replace('\\', '/');
                                    cols[colIdx] = JsonValue.Create(rel);
                                    dedupe[hash] = rel;
                                    fileTouched = true;
                                    extracted++;
                                    Console.WriteLine($"    Extracted to: {dest}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"    Failed to extract embedded asset: {ex.Message}");
                            errors++;
                        }
                    }
                    colIdx++;
                }
                rowIdx++;
            }

            if (apply && fileTouched)
            {
                try
                {
                    var bak = file + ".bak";
                    if (File.Exists(bak)) bak = file + ".bak." + DateTime.Now.ToString("yyyyMMddHHmmss");
                    File.Copy(file, bak);
                    File.WriteAllText(file, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                    Console.WriteLine($"  Updated map and backed up original to: {bak}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  Failed to write updated map: {ex.Message}");
                    errors++;
                }
            }
        }

        Console.WriteLine($"\nSummary: extracted={extracted}, errors={errors}");
        return errors == 0 ? 0 : 1;
    }

    private static string? MimeToExt(string? mime)
    {
        if (mime == null) return null;
        return mime.ToLowerInvariant() switch
        {
            "image/png" => "png",
            "image/jpeg" => "jpg",
            "image/jpg" => "jpg",
            "image/gif" => "gif",
            "image/webp" => "webp",
            "image/bmp" => "bmp",
            _ => null
        };
    }
}
