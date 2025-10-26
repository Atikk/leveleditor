using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

internal class Program
{
    private static int Main(string[] args)
    {
        bool apply = args.Contains("--apply");
        string mapsDir = "maps";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--maps-dir" && i + 1 < args.Length)
            {
                mapsDir = args[i + 1];
                i++;
            }
        }

        Console.WriteLine($"MapPassabilityTool: mapsDir='{mapsDir}' apply={apply}");

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

        int changed = 0;
        int errors = 0;

        foreach (var file in files)
        {
            Console.WriteLine($"\nProcessing: {file}");
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Failed to read file: {ex.Message}");
                errors++;
                continue;
            }

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(text);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Invalid JSON: {ex.Message}");
                errors++;
                continue;
            }

            if (root == null)
            {
                Console.Error.WriteLine("  Empty JSON document");
                errors++;
                continue;
            }

            bool okRows = TryGetInt(root["rows"], out int rows);
            bool okCols = TryGetInt(root["cols"], out int cols);

            if (!okRows || !okCols)
            {
                Console.Error.WriteLine("  Missing or invalid 'rows'/'cols' properties. Skipping.");
                errors++;
                continue;
            }

            var passNode = root["passability"];
            if (passNode == null)
            {
                Console.WriteLine($"  passability: MISSING (rows={rows}, cols={cols})");
                if (apply)
                {
                    // inject default (all true)
                    var rowsArray = new JsonArray();
                    for (int r = 0; r < rows; r++)
                    {
                        var rowArr = new JsonArray();
                        for (int c = 0; c < cols; c++) rowArr.Add(true);
                        rowsArray.Add(rowArr);
                    }
                    root["passability"] = rowsArray;

                    try
                    {
                        // create backup
                        var bak = file + ".bak";
                        if (File.Exists(bak)) bak = file + ".bak." + DateTime.Now.ToString("yyyyMMddHHmmss");
                        File.Copy(file, bak);
                        File.WriteAllText(file, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                        Console.WriteLine($"  Injected passability and backed up original to: {bak}");
                        changed++;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"  Failed to write file: {ex.Message}");
                        errors++;
                    }
                }
                continue;
            }

            // If passability exists, validate dimensions
            if (passNode is JsonArray passArr)
            {
                if (passArr.Count != rows)
                {
                    Console.Error.WriteLine($"  passability malformed: row count {passArr.Count} != rows {rows}");
                    errors++;
                    continue;
                }

                bool rowError = false;
                for (int r = 0; r < passArr.Count; r++)
                {
                    if (passArr[r] is not JsonArray row)
                    {
                        Console.Error.WriteLine($"  passability malformed: row {r} is not an array");
                        rowError = true; break;
                    }
                    if (row.Count != cols)
                    {
                        Console.Error.WriteLine($"  passability malformed: row {r} length {row.Count} != cols {cols}");
                        rowError = true; break;
                    }
                    foreach (var val in row)
                    {
                        if (val is not JsonValue)
                        {
                            Console.Error.WriteLine($"  passability malformed: non-primitive found in row {r}");
                            rowError = true; break;
                        }
                    }
                    if (rowError) break;
                }

                if (!rowError) Console.WriteLine("  passability: OK");
                else errors++;
            }
            else
            {
                Console.Error.WriteLine("  passability malformed: not an array");
                errors++;
            }
        }

        Console.WriteLine($"\nSummary: changed={changed}, errors={errors}");
        return errors == 0 ? 0 : 1;
    }

    private static bool TryGetInt(JsonNode? node, out int value)
    {
        value = 0;
        if (node == null) return false;
        try
        {
            // JsonNode.GetValue<T> will throw if not convertible
            value = node.GetValue<int>();
            return true;
        }
        catch
        {
            // fallback to parsing string
            try
            {
                var s = node.ToString();
                return int.TryParse(s, out value);
            }
            catch
            {
                return false;
            }
        }
    }
}
