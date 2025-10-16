using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Collections.Generic;

var assemblyPath = args.Length > 0 ? args[0] : throw new ArgumentException("Expect assembly path");
if (!File.Exists(assemblyPath))
{
    Console.Error.WriteLine($"Assembly not found: {assemblyPath}");
    return;
}

var runtimeAssemblies = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll", SearchOption.TopDirectoryOnly);
var references = new List<string>(runtimeAssemblies)
{
    assemblyPath
};

var resolver = new PathAssemblyResolver(references);
using var mlc = new MetadataLoadContext(resolver);
var assembly = mlc.LoadFromAssemblyPath(assemblyPath);
foreach (var type in assembly.GetTypes().OrderBy(t => t.FullName))
{
    Console.WriteLine(type.FullName);
}
