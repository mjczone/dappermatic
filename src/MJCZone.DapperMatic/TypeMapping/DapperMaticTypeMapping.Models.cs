// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dapper;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.TypeMapping.Handlers;

namespace MJCZone.DapperMatic.TypeMapping;

/// <summary>
/// Handlers for the property types of the application's own models.
/// </summary>
/// <remarks>
/// Dapper needs a handler registered for each exact type: one for <c>List&lt;int&gt;</c> does nothing for
/// <c>List&lt;OrderLine&gt;</c>. Handlers for collections and dictionaries of common element types are registered
/// up front; those for the application's own types come from its models, which are looked at when
/// <see cref="Initialize"/> runs, when <see cref="DmTableFactory"/> builds one, and the first time Dapper reads
/// one.
/// <para>
/// A dictionary, or a collection of objects, is always stored as JSON. A collection of simple values, such as
/// <c>List&lt;int&gt;</c>, is not unless <see cref="DapperMaticMappingOptions.StoreSimpleCollectionsAsJson"/> is
/// on, because a handler would stop Dapper expanding it for <c>WHERE id IN @ids</c>. An object of the
/// application's own type is stored as JSON only where the property is clearly a column: it carries a column
/// attribute, or its class is a table model. Otherwise it is left alone, because a handler for a type stops Dapper
/// reading that type as a row, which would break <c>Query&lt;Customer&gt;</c> and multi-mapping for a class that
/// merely has a <c>Customer</c> property.
/// </para>
/// </remarks>
public static partial class DapperMaticTypeMapping
{
    private static readonly ConcurrentDictionary<Type, byte> RegisteredModels = new();

    private static readonly Type[] CommonElementTypes =
    [
        typeof(string),
        typeof(int),
        typeof(long),
        typeof(short),
        typeof(byte),
        typeof(bool),
        typeof(double),
        typeof(float),
        typeof(decimal),
        typeof(Guid),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(DateOnly),
        typeof(TimeOnly),
        typeof(TimeSpan),
        typeof(char),
        typeof(object),
    ];

    private static readonly Type[] SmartArrayElementTypes =
    [
        typeof(string),
        typeof(int),
        typeof(long),
        typeof(short),
        typeof(bool),
        typeof(double),
        typeof(float),
        typeof(decimal),
        typeof(Guid),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(DateOnly),
        typeof(TimeOnly),
        typeof(TimeSpan),
    ];

    private static readonly Type[] CollectionShapes =
    [
        typeof(List<>),
        typeof(IList<>),
        typeof(ICollection<>),
        typeof(IEnumerable<>),
        typeof(IReadOnlyList<>),
        typeof(IReadOnlyCollection<>),
        typeof(Collection<>),
        typeof(ImmutableArray<>),
        typeof(ImmutableList<>),
    ];

    private static readonly Type[] SetShapes =
    [
        typeof(HashSet<>),
        typeof(ISet<>),
        typeof(IReadOnlySet<>),
        typeof(ImmutableHashSet<>),
    ];

    private static readonly Type[] DictionaryShapes =
    [
        typeof(Dictionary<,>),
        typeof(IDictionary<,>),
        typeof(IReadOnlyDictionary<,>),
    ];

    private static readonly string[] SkippedAssemblyPrefixes =
    [
        "System",
        "Microsoft",
        "netstandard",
        "mscorlib",
        "WindowsBase",
        "Dapper",
        "MJCZone.DapperMatic",
        "Npgsql",
        "MySql",
        "MySqlConnector",
        "NetTopologySuite",
        "Newtonsoft",
        "xunit",
        "NUnit",
        "Moq",
        "Castle",
        "FluentAssertions",
        "Serilog",
        "NLog",
        "Polly",
        "Azure",
        "AWSSDK",
        "Google",
        "Grpc",
        "StackExchange",
        "Swashbuckle",
        "Scalar",
    ];

    private static readonly string[] DriverNamespacePrefixes =
    [
        "Npgsql",
        "NetTopologySuite",
        "MySql",
        "MySqlConnector",
        "Microsoft.SqlServer",
        "System.Data",
    ];

    /// <summary>
    /// Registers handlers for the property types of the given models, such as <c>List&lt;OrderLine&gt;</c>, for
    /// models <see cref="Initialize"/> could not find.
    /// </summary>
    /// <param name="modelTypes">The model classes.</param>
    /// <remarks>
    /// Has no effect before <see cref="Initialize"/>. Models in the entry assembly and the application assemblies
    /// it references, models built by <see cref="DmTableFactory"/>, and models Dapper has read are registered
    /// already.
    /// </remarks>
    public static void RegisterModels(params Type[] modelTypes)
    {
        ArgumentNullException.ThrowIfNull(modelTypes);
        foreach (var modelType in modelTypes)
        {
            RegisterModel(modelType);
        }
    }

    /// <summary>
    /// Registers handlers for the property types of every model in an assembly.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <remarks>Has no effect before <see cref="Initialize"/>.</remarks>
    public static void RegisterAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        foreach (var type in LoadableTypes(assembly))
        {
            if (IsModelCandidate(type))
            {
                RegisterModel(type);
            }
        }
    }

    /// <summary>
    /// Stores values of the given types as JSON, wherever they appear: as a parameter, a column or a property.
    /// </summary>
    /// <param name="types">
    /// The types, such as <c>List&lt;string&gt;</c> or an application's own <c>Address</c> class.
    /// </param>
    /// <remarks>
    /// Has no effect before <see cref="Initialize"/>. Dapper then treats a type registered here only as a JSON
    /// value: a collection of it no longer expands for <c>WHERE id IN @ids</c>, and <c>Query&lt;Address&gt;</c>
    /// would read the first column of each row as JSON. An array of simple values is stored natively on
    /// PostgreSQL, where DDL creates an array column for it.
    /// </remarks>
    public static void RegisterJsonTypes(params Type[] types)
    {
        ArgumentNullException.ThrowIfNull(types);
        if (!IsInitialized)
        {
            return;
        }

        foreach (var type in types)
        {
            if (type.IsArray && SmartArrayElementTypes.Contains(type.GetElementType()))
            {
                RegisterSmartArrayHandler(type.GetElementType()!);
            }
            else
            {
                RegisterJsonHandler(type);
            }
        }
    }

    /// <summary>
    /// Registers handlers for the property types of a model, once.
    /// </summary>
    /// <param name="modelType">The model class.</param>
    internal static void RegisterModel(Type modelType)
    {
        if (!IsInitialized || !IsModelCandidate(modelType) || !RegisteredModels.TryAdd(modelType, 0))
        {
            return;
        }

        var isTableModel = IsTableModel(modelType);
        foreach (var property in modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0 || IsIgnored(property))
            {
                continue;
            }

            RegisterPropertyType(property.PropertyType, isTableModel || HasColumnAttribute(property));
        }
    }

    /// <summary>
    /// Determines whether a type is a collection of simple values, which Dapper expands for <c>IN</c> lists.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>True for a collection of numbers, strings, Guids, dates, times or enums.</returns>
    internal static bool IsSimpleValueCollection(Type type)
    {
        if (!IsCollection(type) || type == typeof(char[]))
        {
            return false;
        }

        var enumerable =
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                ? type
                : type.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        var element = type.IsArray ? type.GetElementType() : enumerable?.GetGenericArguments()[0];

        return element is not null && IsSimpleValue(Nullable.GetUnderlyingType(element) ?? element);
    }

    /// <summary>
    /// Registers a JSON handler for a type, and for its nullable form if it is a value type.
    /// </summary>
    private static void RegisterJsonHandler(Type type)
    {
        var handler = (SqlMapper.ITypeHandler)Activator.CreateInstance(typeof(JsonTypeHandler<>).MakeGenericType(type))!;
        TryAddTypeHandler(handler, type);
        if (type.IsValueType && Nullable.GetUnderlyingType(type) is null)
        {
            TryAddTypeHandler(handler, typeof(Nullable<>).MakeGenericType(type));
        }
    }

    /// <summary>
    /// Registers the array handler that stores an array natively on PostgreSQL and as JSON elsewhere.
    /// </summary>
    private static void RegisterSmartArrayHandler(Type elementType)
    {
        var handlerType = typeof(SmartArrayTypeHandler<>).MakeGenericType(elementType);
        TryAddTypeHandler((SqlMapper.ITypeHandler)Activator.CreateInstance(handlerType)!, elementType.MakeArrayType());
    }

    private static void RegisterCommonCollectionHandlers()
    {
        foreach (var element in CommonElementTypes)
        {
            if (element != typeof(object) && !_options.StoreSimpleCollectionsAsJson)
            {
                continue;
            }

            foreach (var shape in CollectionShapes)
            {
                RegisterJsonHandler(shape.MakeGenericType(element));
            }

            if (element != typeof(object))
            {
                foreach (var shape in SetShapes)
                {
                    RegisterJsonHandler(shape.MakeGenericType(element));
                }
            }
        }

        RegisterJsonHandler(typeof(object[]));
        RegisterJsonHandler(typeof(List<Dictionary<string, object>>));

        var stringKeyedValues = CommonElementTypes.Concat(
            [typeof(string[]), typeof(List<string>), typeof(Dictionary<string, object>), typeof(Dictionary<string, string>)]
        );
        foreach (var shape in DictionaryShapes)
        {
            foreach (var value in stringKeyedValues)
            {
                RegisterJsonHandler(shape.MakeGenericType(typeof(string), value));
            }

            foreach (var key in new[] { typeof(int), typeof(long), typeof(Guid) })
            {
                RegisterJsonHandler(shape.MakeGenericType(key, typeof(string)));
                RegisterJsonHandler(shape.MakeGenericType(key, typeof(object)));
            }
        }
    }

    private static void RegisterPropertyType(Type type, bool isColumn)
    {
        var valueType = Nullable.GetUnderlyingType(type) ?? type;
        if (
            valueType.ContainsGenericParameters
            || IsMappedByDapper(valueType)
            || SqlMapper.HasTypeHandler(valueType)
            || IsDriverType(valueType)
        )
        {
            return;
        }

        if (IsSimpleValueCollection(valueType) && !_options.StoreSimpleCollectionsAsJson)
        {
            return;
        }

        if (IsCollection(valueType) || (isColumn && IsStoredObject(valueType)))
        {
            RegisterJsonHandler(valueType);
        }
    }

    private static void RegisterKnownModels()
    {
        foreach (var modelType in DmTableFactory.CachedTypes)
        {
            RegisterModel(modelType);
        }
    }

    private static void RegisterEntryAssemblies()
    {
        var entry = Assembly.GetEntryAssembly();
        if (entry is null)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<Assembly>([entry]);
        while (pending.Count > 0)
        {
            var assembly = pending.Dequeue();
            if (!seen.Add(assembly.FullName ?? string.Empty) || IsSkippedAssembly(assembly.GetName()))
            {
                continue;
            }

            RegisterAssembly(assembly);

            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (seen.Contains(reference.FullName) || IsSkippedAssembly(reference))
                {
                    continue;
                }

                try
                {
                    pending.Enqueue(Assembly.Load(reference));
                }
                catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
                {
                    // An assembly that cannot be loaded has no models to register.
                }
            }
        }
    }

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }

    private static bool IsSkippedAssembly(AssemblyName name) =>
        name.Name is null
        || SkippedAssemblyPrefixes.Any(prefix => name.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool IsModelCandidate(Type type) =>
        (type.IsClass || (type.IsValueType && !type.IsEnum && !type.IsPrimitive))
        && !type.IsAbstract
        && !type.ContainsGenericParameters
        && !type.IsDefined(typeof(CompilerGeneratedAttribute), false)
        && !typeof(Delegate).IsAssignableFrom(type)
        && !typeof(Attribute).IsAssignableFrom(type)
        && !typeof(Exception).IsAssignableFrom(type)
        && !typeof(IEnumerable).IsAssignableFrom(type)
        && !IsDriverType(type)
        && type.Namespace?.StartsWith("System", StringComparison.Ordinal) != true;

    private static bool IsMappedByDapper(Type type) =>
        type.IsPrimitive
        || type.IsEnum
        || type == typeof(string)
        || type == typeof(decimal)
        || type == typeof(DateTime)
        || type == typeof(DateTimeOffset)
        || type == typeof(TimeSpan)
        || type == typeof(DateOnly)
        || type == typeof(TimeOnly)
        || type == typeof(Guid)
        || type == typeof(byte[])
        || type == typeof(object);

    private static bool IsDriverType(Type type) =>
        type.Namespace is { } ns
        && DriverNamespacePrefixes.Any(prefix => ns.StartsWith(prefix, StringComparison.Ordinal));

    private static bool IsCollection(Type type) =>
        type != typeof(string) && type != typeof(byte[]) && typeof(IEnumerable).IsAssignableFrom(type);

    private static bool IsSimpleValue(Type type) =>
        type.IsPrimitive
        || type.IsEnum
        || type == typeof(string)
        || type == typeof(decimal)
        || type == typeof(Guid)
        || type == typeof(DateTime)
        || type == typeof(DateTimeOffset)
        || type == typeof(DateOnly)
        || type == typeof(TimeOnly)
        || type == typeof(TimeSpan)
        || type == typeof(Half)
        || type == typeof(Int128)
        || type == typeof(UInt128)
        || type == typeof(System.Numerics.BigInteger);

    private static bool IsStoredObject(Type type) =>
        IsModelCandidate(type)
        && !IsTableModel(type)
        && type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Length > 0;

    private static bool IsTableModel(Type type) =>
        type.IsDefined(typeof(DmTableAttribute), true) || type.IsDefined(typeof(TableAttribute), true);

    private static bool HasColumnAttribute(PropertyInfo property) =>
        property.GetCustomAttributes(true).Any(a => a is DmColumnAttribute or ColumnAttribute || a.GetType().Name == "AliasAttribute");

    private static bool IsIgnored(PropertyInfo property) =>
        property.GetCustomAttributes(true).Any(a =>
            a is DmIgnoreAttribute or NotMappedAttribute or DmRelationAttribute || a.GetType().Name == "IgnoreAttribute"
        );
}
