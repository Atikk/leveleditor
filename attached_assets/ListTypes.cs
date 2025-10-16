using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Collections.Generic;

var assemblyPath = args.Length > 0 ? args[0] : throw new ArgumentException("Expect assembly path");
if (!File.Exists(assemblyPath))
{
    Console.Error.WriteLine($"Assembly not found: {assemblyPath}");
    return;
}

var assemblyDirectory = Path.GetDirectoryName(assemblyPath) ?? Environment.CurrentDirectory;
var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();

var loadContext = new AssemblyLoadContext("ListTypes", isCollectible: true);
loadContext.Resolving += (_, name) =>
{
    var fileName = $"{name.Name}.dll";

    var candidate = Path.Combine(assemblyDirectory, fileName);
    if (File.Exists(candidate))
    {
        return loadContext.LoadFromAssemblyPath(candidate);
    }

    candidate = Path.Combine(runtimeDirectory, fileName);
    if (File.Exists(candidate))
    {
        return loadContext.LoadFromAssemblyPath(candidate);
    }

    return null;
};

try
{
    var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
    foreach (var type in assembly.GetTypes().OrderBy(t => t.FullName))
    {
        Console.WriteLine(type.FullName);
    }
}
finally
{
    loadContext.Unload();
}
