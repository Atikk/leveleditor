using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

if (args.Length == 0)
{
	Console.Error.WriteLine("Usage: TypeLister <assembly path>");
	return;
}

var assemblyPath = args[0];
var detailType = args.Length > 1 ? args[1] : null;

if (!File.Exists(assemblyPath))
{
	Console.Error.WriteLine($"Assembly not found: {assemblyPath}");
	return;
}

using var stream = File.OpenRead(assemblyPath);
using var peReader = new PEReader(stream);
var metadataReader = peReader.GetMetadataReader();

var typeNames = new List<string>();

foreach (var handle in metadataReader.TypeDefinitions)
{
	var definition = metadataReader.GetTypeDefinition(handle);
	var typeName = metadataReader.GetString(definition.Name);
	if (typeName.StartsWith("<", StringComparison.Ordinal))
	{
		continue; // skip compiler generated display classes
	}

	var namespaceName = metadataReader.GetString(definition.Namespace);
	var fullName = string.IsNullOrEmpty(namespaceName)
		? typeName
		: namespaceName + "." + typeName;

	typeNames.Add(fullName);

	if (detailType != null && string.Equals(fullName, detailType, StringComparison.Ordinal))
	{
		Console.WriteLine($"Members of {fullName}:");
		foreach (var propertyHandle in definition.GetProperties())
		{
			var property = metadataReader.GetPropertyDefinition(propertyHandle);
			var propertyName = metadataReader.GetString(property.Name);
			Console.WriteLine($"  [Property] {propertyName}");
		}

		foreach (var methodHandle in definition.GetMethods())
		{
			var method = metadataReader.GetMethodDefinition(methodHandle);
			var methodName = metadataReader.GetString(method.Name);
			Console.WriteLine($"  [Method] {methodName}");
		}
	}
}

foreach (var name in typeNames.OrderBy(x => x, StringComparer.Ordinal))
{
	Console.WriteLine(name);
}
