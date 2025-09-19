// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using MJCZone.DapperMatic.Providers.Base;
using MJCZone.DapperMatic.AspNetCore;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests;

public class TestOutputDocs
{
    private static readonly JsonSerializerOptions SerializationSettings = CreateSerializationSettings();

    public TestOutputDocs(ITestOutputHelper logger)
    {
        Logger = logger;
    }

    private ITestOutputHelper Logger { get; }

    private static JsonSerializerOptions CreateSerializationSettings()
    {
        var stringEnumConverter = new System.Text.Json.Serialization.JsonStringEnumConverter();
        var serializationSettings = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System
                .Text
                .Json
                .Serialization
                .JsonIgnoreCondition
                .WhenWritingNull,
        };
        serializationSettings.Converters.Add(stringEnumConverter);
        return serializationSettings;
    }

    [Fact]
    public void Can_generate_docs()
    {
        // Generate documentation for both assemblies
        GenerateAssemblyDocs(typeof(DatabaseMethodsBase).Assembly);
        GenerateAssemblyDocs(typeof(DapperMaticConfigurationBuilder).Assembly);
    }

    private void GenerateAssemblyDocs(Assembly assembly)
    {
        // get the directory of the current assembly
        var assemblyDirectory = Path.GetDirectoryName(assembly.Location);
        if (assemblyDirectory == null || !Directory.Exists(assemblyDirectory))
        {
            Logger.WriteLine($"Could not get the directory of the assembly {assembly.GetName().Name}.");
            Assert.True(false);
            return;
        }

        // get the xml documentation file for the assembly
        var xmlFile = Path.Combine(assemblyDirectory, $"{assembly.GetName().Name}.xml");
        if (!File.Exists(xmlFile))
        {
            Logger.WriteLine($"Could not find the xml documentation file: {xmlFile}");
            Assert.True(false);
            return;
        }

        // load the xml documentation file
        var xml = XDocument.Load(xmlFile);
        if (xml.Root == null)
        {
            Logger.WriteLine($"Could not load the xml documentation file for {assembly.GetName().Name}.");
            Assert.True(false);
            return;
        }

        // see: https://github.com/mysticmind/reversemarkdown-net (HTML to md conversion)

        // see: https://github.com/akovanev/NetDocsProcessor/tree/main
        //      https://github.com/akovanev/NetDocsProcessor/blob/main/demo/Akov.Chillout.Demo/Program.cs
        var akovData = new Akov.NetDocsProcessor.Api.DocsProcessorApi()
            .ObtainDocumentation(
                new Akov.NetDocsProcessor.Input.AssemblyPaths(assembly.Location, xmlFile),
                new Akov.NetDocsProcessor.Input.GenerationSettings
                {
                    // AccessLevel = Akov.NetDocsProcessor.Input.AccessLevel.Protected
                    AccessLevel = Akov.NetDocsProcessor.Input.AccessLevel.Public
                }
            )
            .ToList();

        var akovJsonFile = Path.Combine(assemblyDirectory, $"{assembly.GetName().Name}.docs.json");
        var output = new
        {
            enums = new
            {
                elementTypes = Enum.GetValues<Akov.NetDocsProcessor.Common.ElementType>(),
                accessLevels = Enum.GetValues<Akov.NetDocsProcessor.Input.AccessLevel>(),
            },
            data = akovData,
        };
        var serializedContent = JsonSerializer.Serialize(output, SerializationSettings);
        File.WriteAllText(akovJsonFile, serializedContent);
        //Logger.WriteLine(JsonSerializer.Serialize(akovData, SerializationSettings));

        // var docsJsonFileName = $"{assembly.GetName().Name}.json";
        // var docsJsonFile = Path.Combine(assemblyDirectory, docsJsonFileName);
        // var docs = new Docs();
        // docs.AddAssembly(assembly, xml);
        // File.WriteAllText(docsJsonFile, JsonSerializer.Serialize(docs, SerializationSettings));
        //Logger.WriteLine(JsonSerializer.Serialize(docs, SerializationSettings));

        // output the file to the docs directory
        var rootDirectory = Path.GetDirectoryName(assemblyDirectory) ?? string.Empty;
        while (!File.Exists(Path.Combine(rootDirectory, ".editorconfig")))
        {
            rootDirectory = Path.GetDirectoryName(rootDirectory);
            if (string.IsNullOrEmpty(rootDirectory))
            {
                Logger.WriteLine("Could not find the root directory.");
                return;
            }
        }
        var packagesDirectory = Path.Combine(rootDirectory, "docs", "packages");
        var docsAssemblyJsonFile = Path.Combine(
            packagesDirectory,
            $"{assembly.GetName().Name}.json"
        );
        Directory.CreateDirectory(packagesDirectory);

        // Write directly to the destination instead of copying to avoid file locking issues
        File.WriteAllText(docsAssemblyJsonFile, serializedContent);
        Logger.WriteLine($"Created {docsAssemblyJsonFile}");
        return;

        // The following is prototypical code created BEFORE the Akov.NetDocsProcessor was used.

        // Assert.NotNull(docs.Title);

        // // get the xml documentation for the type
        // var xmlMemberNodes = xml.Root.Element("members")?.Elements("member");
        // Assert.NotNull(xmlMemberNodes);

        // foreach (var type in assembly.GetExportedTypes())
        // {
        //     Logger.WriteLine(type.FullName);

        //     var xmlTypeElement = xmlMemberNodes.FirstOrDefault(e =>
        //         e.Attribute("name")?.Value == $"T:{type.FullName}"
        //     );
        //     var xmlConstructorNodes = xmlMemberNodes
        //         .Where(e =>
        //             e.Attribute("name")?.Value.StartsWith($"M:{type.FullName}.#ctor") == true
        //         )
        //         .ToArray();

        //     Assert.NotNull(xmlTypeElement);

        //     // log the summary of the type
        //     if (!string.IsNullOrWhiteSpace(xmlTypeElement.Element("summary")?.Value))
        //         Logger.WriteLine($"\t{xmlTypeElement.Element("summary")?.Value}");

        //     // log the remarks of the type
        //     if (!string.IsNullOrWhiteSpace(xmlTypeElement.Element("remarks")?.Value))
        //         Logger.WriteLine($"\t{xmlTypeElement.Element("remarks")?.Value}");

        //     // log the example of the type
        //     if (!string.IsNullOrWhiteSpace(xmlTypeElement.Element("example")?.Value))
        //         Logger.WriteLine($"\t{xmlTypeElement.Element("example")?.Value}");

        //     // log the type parameters of the type
        //     foreach (var param in xmlTypeElement.Elements("typeparam"))
        //     {
        //         Logger.WriteLine($"\t{param.Attribute("name")?.Value}: {param.Value}");
        //     }

        //     // log the constructors of the type
        //     var constructors = type.GetConstructors(BindingFlags.Public);
        //     if (constructors.Any())
        //         Logger.WriteLine($"\t=== Constructors ========================\n");

        //     if (xmlConstructorNodes.Length == constructors.Length)
        //     {
        //         for (int i = 0; i < constructors.Length; i++)
        //         {
        //             ConstructorInfo? constructor = constructors[i];
        //             var xmlConstructorNode = xmlConstructorNodes[i];

        //             if (xmlConstructorNode.Attribute("name") == null)
        //                 continue;

        //             Logger.WriteLine($"\t{xmlConstructorNode.Attribute("name")?.Value}");

        //             if (!string.IsNullOrWhiteSpace(xmlConstructorNode.Element("summary")?.Value))
        //                 Logger.WriteLine($"\t{xmlConstructorNode.Element("summary")?.Value}");

        //             if (!string.IsNullOrWhiteSpace(xmlConstructorNode.Element("remarks")?.Value))
        //                 Logger.WriteLine($"\t{xmlConstructorNode.Element("remarks")?.Value}");

        //             if (!string.IsNullOrWhiteSpace(xmlConstructorNode.Element("example")?.Value))
        //                 Logger.WriteLine($"\t{xmlConstructorNode.Element("example")?.Value}");
        //         }
        //     }

        //     // log the fields of the type
        //     var fields = type.GetFields(BindingFlags.Public);
        //     if (fields.Any())
        //         Logger.WriteLine($"\t=== Fields ========================\n");

        //     foreach (var field in fields)
        //     {
        //         Logger.WriteLine($"\t{field.Name}");

        //         // get the xml documentation for the field
        //         var fieldElement = xml
        //             .Root.Element("members")
        //             ?.Elements("member")
        //             .FirstOrDefault(e =>
        //                 e.Attribute("name")?.Value == $"F:{type.FullName}.{field.Name}"
        //             );

        //         if (fieldElement != null)
        //         {
        //             // log the summary of the field
        //             if (!string.IsNullOrWhiteSpace(fieldElement.Element("summary")?.Value))
        //                 Logger.WriteLine($"\t\tSummary: {fieldElement.Element("summary")?.Value}");

        //             // log the remarks of the field
        //             if (!string.IsNullOrWhiteSpace(fieldElement.Element("remarks")?.Value))
        //                 Logger.WriteLine($"\t\t{fieldElement.Element("remarks")?.Value}");

        //             // log the example of the field
        //             if (!string.IsNullOrWhiteSpace(fieldElement.Element("example")?.Value))
        //                 Logger.WriteLine($"\t\t{fieldElement.Element("example")?.Value}");
        //         }
        //     }

        //     // log the properties of the type
        //     var properties = type.GetProperties(BindingFlags.Public);
        //     if (properties.Any())
        //         Logger.WriteLine($"\t=== Properties ========================\n");

        //     foreach (var property in properties)
        //     {
        //         Logger.WriteLine($"\t{property.Name}");

        //         // get the xml documentation for the property
        //         var propertyElement = xml
        //             .Root.Element("members")
        //             ?.Elements("member")
        //             .FirstOrDefault(e =>
        //                 e.Attribute("name")?.Value == $"P:{type.FullName}.{property.Name}"
        //             );

        //         if (propertyElement != null)
        //         {
        //             // log the summary of the property
        //             if (!string.IsNullOrWhiteSpace(propertyElement.Element("summary")?.Value))
        //                 Logger.WriteLine($"\t\t{propertyElement.Element("summary")?.Value}");

        //             // log the remarks of the property
        //             if (!string.IsNullOrWhiteSpace(propertyElement.Element("remarks")?.Value))
        //                 Logger.WriteLine($"\t\t{propertyElement.Element("remarks")?.Value}");

        //             // log the example of the property
        //             if (!string.IsNullOrWhiteSpace(propertyElement.Element("example")?.Value))
        //                 Logger.WriteLine($"\t\t{propertyElement.Element("example")?.Value}");
        //         }
        //     }

        //     // log the methods of the type
        //     if (type.GetMethods(BindingFlags.Public).Any())
        //         Logger.WriteLine($"\t=== Methods ========================\n");

        //     foreach (var method in type.GetMethods())
        //     {
        //         Logger.WriteLine($"\tMethod: {method.Name}");

        //         // get the xml documentation for the method
        //         var methodElement = xml
        //             .Root.Element("members")
        //             ?.Elements("member")
        //             .FirstOrDefault(e =>
        //                 e.Attribute("name")?.Value == $"M:{type.FullName}.{method.Name}"
        //             );

        //         if (methodElement != null)
        //         {
        //             // log the summary of the method
        //             if (!string.IsNullOrWhiteSpace(methodElement.Element("summary")?.Value))
        //                 Logger.WriteLine($"\t\tSummary: {methodElement.Element("summary")?.Value}");

        //             // log the remarks of the method
        //             if (!string.IsNullOrWhiteSpace(methodElement.Element("remarks")?.Value))
        //                 Logger.WriteLine($"\t\tRemarks: {methodElement.Element("remarks")?.Value}");

        //             // log the example of the method
        //             if (!string.IsNullOrWhiteSpace(methodElement.Element("example")?.Value))
        //                 Logger.WriteLine($"\t\tExample: {methodElement.Element("example")?.Value}");

        //             // log the parameters of the method
        //             if (methodElement.Elements("param").Any())
        //                 Logger.WriteLine("\t\tParameters:");

        //             foreach (var param in methodElement.Elements("param"))
        //             {
        //                 Logger.WriteLine(
        //                     $"\t\tParam: {param.Attribute("name")?.Value}: {param.Value}"
        //                 );
        //             }

        //             // log the return value of the method
        //             if (!string.IsNullOrWhiteSpace(methodElement.Element("returns")?.Value))
        //                 Logger.WriteLine($"\t\tReturns: {methodElement.Element("returns")?.Value}");
        //         }
        //     }
        // }

        // Assert.True(true);
    }
}

public class Docs
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Copyright { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
    public List<AssemblyDocs> Assemblies { get; set; } = [];
}

public static class DocsExtensions
{
    public static Docs AddAssembly(this Docs docs, Assembly assembly, XDocument docsXml)
    {
        var assemblyName = assembly.GetName().Name;
        if (string.IsNullOrWhiteSpace(assemblyName))
            return docs;

        if (string.IsNullOrWhiteSpace(docs.Title))
            docs.Title = (
                assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? assemblyName
            ).Trim();

        if (string.IsNullOrEmpty(docs.Description))
            docs.Description = (
                assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description
                ?? string.Empty
            ).Trim();

        if (string.IsNullOrEmpty(docs.Product))
            docs.Product = (
                assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? string.Empty
            ).Trim();

        if (string.IsNullOrEmpty(docs.Company))
            docs.Company = (
                assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty
            ).Trim();

        if (string.IsNullOrEmpty(docs.Copyright))
            docs.Copyright = (
                assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty
            ).Trim();

        if (string.IsNullOrEmpty(docs.Version))
            docs.Version = (
                assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
                ?? assembly.GetCustomAttribute<AssemblyVersionAttribute>()?.Version
                ?? assembly.GetName()?.Version?.ToString()
                ?? string.Empty
            ).Trim();

        foreach (var m in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (!docs.Metadata.ContainsKey(m.Key) && !string.IsNullOrWhiteSpace(m.Value))
            {
                // camelCase the key
                var key = m.Key[..1].ToLower() + m.Key[1..];
                docs.Metadata[key] = m.Value;
            }
        }

        var assemblyDocs = AssemblyDocs.Generate(assembly, docsXml);
        docs.Assemblies.Add(assemblyDocs);

        return docs;
    }

    public static string InnerXml(this XNode node)
    {
        using var reader = node.CreateReader();
        reader.MoveToContent();
        return reader.ReadInnerXml();

        // var sb = new StringBuilder();
        // foreach (var node in node.Nodes())
        // {
        //     if (node is XText text)
        //         sb.Append(text.Value);
        //     else if (node is XElement el)
        //     {
        //         sb.Append(el.ToString());
        //         // sb.Append(el.Value);
        //     }
        // }
        // return sb.ToString().Trim();
    }
}

public class AssemblyDocs
{
    internal static AssemblyDocs Generate(Assembly assembly, XDocument docsXml)
    {
        var docsXmlAssemblyName = docsXml.Root?.Element("assembly")?.Element("name")?.Value;

        Assert.Equal(assembly.GetName().Name, docsXmlAssemblyName);

        var docs = new AssemblyDocs
        {
            Id = $"A:{docsXmlAssemblyName}",
            Name = assembly.GetName().Name!,
            FullName = assembly.FullName!,
        };

        foreach (var type in assembly.GetExportedTypes())
        {
            var typeDocs = TypeDocs.Generate(type, docsXml);
            docs.Types.Add(typeDocs);
        }

        return docs;
    }

    /// <summary>
    /// Starts with A:
    /// </summary>
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public List<TypeDocs> Types { get; set; } = [];
}

public class TypeDocs
{
    internal static TypeDocs Generate(Type type, XDocument docsXml)
    {
        var memberNodes = docsXml.Root!.Element("members")?.Elements("member").ToArray() ?? [];
        var typeNode = memberNodes.FirstOrDefault(e =>
            e.Attribute("name")?.Value == $"T:{type.FullName}"
        );

        Assert.NotNull(typeNode);

        var typeDocs = new TypeDocs
        {
            Id = $"T:{type.FullName}",
            Type = new TypeRef(type),
            Summary = typeNode.Element("summary")?.InnerXml().Trim(),
            Remarks = typeNode.Element("remarks")?.InnerXml().Trim(),
            Example = typeNode.Element("example")?.InnerXml().Trim(),
        };

        // only the constructors WITH parameters
        var cstrNodes = memberNodes
            .Where(e =>
                e.Attribute("name")?.Value?.StartsWith($"M:{type.FullName}.#ctor") == true
                && e.Attribute("name")?.Value?.EndsWith("#ctor") == false
            )
            .ToArray();
        var fieldNodes = memberNodes
            .Where(e => e.Attribute("name")?.Value?.StartsWith($"F:{type.FullName}.") == true)
            .ToArray();
        var propertyNodes = memberNodes
            .Where(e => e.Attribute("name")?.Value?.StartsWith($"P:{type.FullName}.") == true)
            .ToArray();
        var methodNodes = memberNodes
            .Where(e =>
                e.Attribute("name")?.Value?.StartsWith($"M:{type.FullName}.") == true
                && !e.Attribute("name")?.Value?.Contains("#ctor") == true
            )
            .ToArray();

        var bindingFlags =
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static;

        // only the constructors WITH parameters
        var constructors = type.GetConstructors(bindingFlags)
            .Where(c => c.GetParameters().Length > 0)
            .ToArray();
        var fields = type.GetFields(bindingFlags)
            .Where(m => !m.IsSpecialName && !m.IsPrivate && !(m.IsStatic && m.IsPrivate))
            .ToArray();
        var properties = type.GetProperties(bindingFlags).ToArray();
        var methods = type.GetMethods(bindingFlags)
            .Where(m =>
                !m.IsConstructor
                && !m.IsHideBySig
                && m.DeclaringType?.Namespace?.StartsWith("MJCZone") == true
                && !(m.IsStatic && m.IsPrivate)
            )
            .ToArray();

        if (constructors.Length != cstrNodes.Length)
            Assert.Equal(constructors.Length, cstrNodes.Length);

        for (int i = 0; i < constructors.Length; i++)
        {
            var cstrNode = cstrNodes[i];
            Assert.NotNull(cstrNode);

            var cstrNodeName = cstrNode.Attribute("name")?.Value ?? string.Empty;
            Assert.True(cstrNodeName.Length > 3, "Node name is too short.");

            var constructor = constructors[i];
            var constructorDocs = new ConstructorDocs
            {
                Id = $"C:{cstrNodeName[3..]}",
                Summary = cstrNode.Element("summary")?.InnerXml().Trim(),
                Remarks = cstrNode.Element("remarks")?.InnerXml().Trim(),
                Example = cstrNode.Element("example")?.InnerXml().Trim(),
            };
            typeDocs.Constructors.Add(constructorDocs);
        }

        foreach (var field in fields)
        {
            var fieldNode = fieldNodes.FirstOrDefault(e =>
                e.Attribute("name")?.Value == $"F:{type.FullName}.{field.Name}"
            );
            if (fieldNode == null)
                continue;
            Assert.NotNull(fieldNode);

            var fieldDocs = new FieldDocs
            {
                Id = $"F:{type.FullName}.{field.Name}",
                Name = field.Name,
                Inherited = field.DeclaringType != type,
                IsReadOnly = field.IsInitOnly,
                IsStatic = field.IsStatic,
                FieldType = new TypeRef(field.FieldType),
                Summary = fieldNode.Element("summary")?.InnerXml().Trim(),
                Remarks = fieldNode.Element("remarks")?.InnerXml().Trim(),
                Example = fieldNode.Element("example")?.InnerXml().Trim(),
            };
            typeDocs.Fields.Add(fieldDocs);
        }

        foreach (var property in properties)
        {
            var propertyNode = propertyNodes.FirstOrDefault(e =>
                e.Attribute("name")?.Value == $"P:{type.FullName}.{property.Name}"
            );
            if (propertyNode == null)
            {
                propertyNode = propertyNodes.FirstOrDefault(e =>
                    e.Attribute("name")?.Value
                    == $"P:{property.DeclaringType!.FullName}.{property.Name}"
                );
                if (propertyNode == null)
                    continue;
            }
            Assert.NotNull(propertyNode);

            var propertyDocs = new PropertyDocs
            {
                Id = $"P:{type.FullName}.{property.Name}",
                Name = property.Name,
                Inherited = property.DeclaringType != type,
                IsReadOnly = !property.CanWrite,
                PropertyType = new TypeRef(property.PropertyType),
                Summary = propertyNode.Element("summary")?.InnerXml().Trim(),
                Remarks = propertyNode.Element("remarks")?.InnerXml().Trim(),
                Example = propertyNode.Element("example")?.InnerXml().Trim(),
            };
            typeDocs.Properties.Add(propertyDocs);
        }

        foreach (var method in methods)
        {
            var methodNode = methodNodes.FirstOrDefault(e =>
                e.Attribute("name")?.Value.StartsWith($"M:{type.FullName}.{method.Name}") == true
            );
            if (methodNode == null)
                continue;
            Assert.NotNull(methodNode);

            var methodDocs = new MethodDocs
            {
                Id = $"M:{type.FullName}.{method.Name}",
                Name = method.Name,
                Inherited = method.DeclaringType != type,
                IsReadOnly = method.IsSpecialName,
                IsStatic = method.IsStatic,
                ReturnType = new TypeRef(method.ReturnType),
                Summary = methodNode.Element("summary")?.InnerXml().Trim(),
                Remarks = methodNode.Element("remarks")?.InnerXml().Trim(),
                Example = methodNode.Element("example")?.InnerXml().Trim(),
            };

            foreach (var param in method.GetParameters())
            {
                var paramNode = methodNode
                    .Elements("param")
                    .FirstOrDefault(e =>
                        e.Attribute("name")?.Value
                        == $"M:{type.FullName}.{method.Name}({param.ParameterType.Name})"
                    );

                var paramDocs = new TypeParamDocs
                {
                    Type = new TypeRef(param.ParameterType),
                    Name = param.Name!,
                    Summary = paramNode?.Element("summary")?.InnerXml().Trim(),
                    Remarks = paramNode?.Element("remarks")?.InnerXml().Trim(),
                    Example = paramNode?.Element("example")?.InnerXml().Trim(),
                };
                methodDocs.TypeParams.Add(paramDocs);
            }

            typeDocs.Methods.Add(methodDocs);
        }

        return typeDocs;
    }

    /// <summary>
    /// Starts with T:
    /// </summary>
    public string Id { get; set; } = string.Empty;
    public TypeRef Type { get; set; } = null!;
    public string? Summary { get; set; } = null;
    public string? Remarks { get; set; } = null;
    public string? Example { get; set; } = null;
    public List<ConstructorDocs> Constructors { get; set; } = [];
    public List<FieldDocs> Fields { get; set; } = [];
    public List<PropertyDocs> Properties { get; set; } = [];
    public List<MethodDocs> Methods { get; set; } = [];
}

public class ConstructorDocs
{
    /// <summary>
    /// Starts with C:
    /// </summary>
    public string Id { get; set; } = string.Empty;
    public List<TypeParamDocs> TypeParams { get; set; } = [];
    public string? Summary { get; set; } = null;
    public string? Remarks { get; set; } = null;
    public string? Example { get; set; } = null;
}

public class FieldDocs
{
    /// <summary>
    /// Starts with F:
    /// </summary>
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Inherited { get; set; } = false;
    public bool IsReadOnly { get; set; } = false;
    public bool IsStatic { get; set; } = false;
    public TypeRef FieldType { get; set; } = null!;
    public string? Summary { get; set; } = null;
    public string? Remarks { get; set; } = null;
    public string? Example { get; set; } = null;
}

public class PropertyDocs
{
    /// <summary>
    /// Starts with P:
    /// </summary>
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Inherited { get; set; } = false;
    public bool IsReadOnly { get; set; } = false;
    public TypeRef PropertyType { get; set; } = null!;
    public string? Summary { get; set; } = null;
    public string? Remarks { get; set; } = null;
    public string? Example { get; set; } = null;
}

public class MethodDocs
{
    /// <summary>
    /// Starts with M:
    /// </summary>
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Inherited { get; set; } = false;
    public bool IsReadOnly { get; set; } = false;
    public bool IsStatic { get; set; } = false;
    public TypeRef? ReturnType { get; set; } = null;
    public string? Summary { get; set; } = null;
    public string? Remarks { get; set; } = null;
    public string? Example { get; set; } = null;
    public List<TypeParamDocs> TypeParams { get; set; } = [];
}

public class TypeParamDocs
{
    public TypeRef? Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Summary { get; set; } = null;
    public string? Remarks { get; set; } = null;
    public string? Example { get; set; } = null;
}

public class TypeRef
{
    public TypeRef() { }

    public TypeRef(Type type)
    {
        Name = type.Name;
        FullName = type.AssemblyQualifiedName!;
        DisplayName = type.GetFriendlyName();
        Namespace = type.Namespace!;
        Assembly = type.Assembly.FullName!;
    }

    public string Assembly { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<TypeRef> GenericArguments { get; set; } = [];
    public TypeRef? BaseType { get; set; } = null;
    public List<TypeRef> Interfaces { get; set; } = [];
}
