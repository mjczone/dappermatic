// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Immutable;
using System.Reflection;
using Akov.NetDocsProcessor.Helpers;
using Akov.NetDocsProcessor.Input;
using Akov.NetDocsProcessor.Output;
using Microsoft.CodeAnalysis;
using TypeInfo = System.Reflection.TypeInfo;

namespace Akov.NetDocsProcessor.Extensions;

internal static partial class TypeInfoExtensions
{
    public static List<MemberDescription> PopulateConstructors(this TypeInfo typeInfo, TypeDescription parent, ImmutableArray<IMethodSymbol>? symbols, AccessLevel accessLevel)
        =>
            typeInfo
                .GetTypeConstructors()
                .OnlyVisible(accessLevel)
                .Select(constructor => 
                    DescriptionHelper.CreateMember(
                        constructor.DeclaringType?.Name ?? "Constructor",  // Use type name for constructor display
                        constructor.GetUniqueName(),  // Use unique name for URL
                        MemberTypes.Constructor,
                        symbols.FindBy(constructor),
                        parent.Self,
                        constructor))  // Pass reflection member for fallback
                .ToList();
    
    public static List<MemberDescription> PopulateFields(this TypeInfo typeInfo, TypeDescription parent, ImmutableArray<IFieldSymbol>? symbols, AccessLevel accessLevel)
        =>
            typeInfo
                .GetTypeFields()
                .OnlyVisible(accessLevel)
                .Select(field => 
                    DescriptionHelper.CreateMember(
                        field.Name, 
                        MemberTypes.Field,
                        symbols.FindBy(field),
                        parent.Self,
                        field))  // Pass reflection member for fallback
                .ToList();
    
    public static List<MemberDescription> PopulateMethods(this TypeInfo typeInfo, TypeDescription parent, ImmutableArray<IMethodSymbol>? symbols, AccessLevel accessLevel)
        =>
            typeInfo
                .GetTypeMethods()
                .OnlyVisible(accessLevel)
                .Select(method => 
                    DescriptionHelper.CreateMember(
                        method.Name,  // Use actual method name, not unique name
                        method.GetUniqueName(),  // Pass unique name separately for URL
                        MemberTypes.Method,
                        symbols.FindBy(method),
                        parent.Self,
                        method))  // Pass reflection member for fallback
                .ToList();
    
    public static List<MemberDescription> PopulateProperties(this TypeInfo typeInfo, TypeDescription parent, ImmutableArray<IPropertySymbol>? symbols, AccessLevel accessLevel)
        =>
            typeInfo
                .GetTypeProperties()
                .OnlyVisible(accessLevel)
                .Select(property => 
                    DescriptionHelper.CreateMember(
                        property.Name, 
                        MemberTypes.Property, 
                        symbols.FindBy(property),
                        parent.Self,
                        property))  // Pass reflection member for fallback
                .ToList();
    
    public static List<MemberDescription> PopulateEvents(this TypeInfo typeInfo, TypeDescription parent, ImmutableArray<IEventSymbol>? symbols, AccessLevel accessLevel)
        =>
            typeInfo
                .GetTypeEvents()
                .OnlyVisible(accessLevel)
                .Select(@event => 
                    DescriptionHelper.CreateMember(
                        @event.Name, 
                        MemberTypes.Event,
                        symbols.FindBy(@event),
                        parent.Self,
                        @event))  // Pass reflection member for fallback
                .ToList();

    public static List<EnumMemberDescription> PopulateEnumMembers(this TypeInfo typeInfo, TypeDescription parent, ImmutableArray<ISymbol>? symbols)
    {
        var result = new List<EnumMemberDescription>();
        if (!typeInfo.IsEnum || symbols is null) return result;

        result.AddRange(
            from ISymbol? symbol in symbols 
            select DescriptionHelper.CreateEnumMember(symbol, parent.Self));

        return result;
    }
    
    private static ConstructorInfo[] GetTypeConstructors(this TypeInfo typeInfo)
        => typeInfo
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Where(m => IsMethodBelongingToType(m.DeclaringType, typeInfo))
            .ToArray();

    private static FieldInfo[] GetTypeFields(this TypeInfo typeInfo)
        => typeInfo
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Where(p => IsMethodBelongingToType(p.DeclaringType, typeInfo))
            .ToArray();
    
    private static MethodInfo[] GetTypeMethods(this TypeInfo typeInfo)
        => typeInfo
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Where(m => IsMethodBelongingToType(m.DeclaringType, typeInfo) && !m.IsSpecialName && !m.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
            .ToArray();

    private static PropertyInfo[] GetTypeProperties(this TypeInfo typeInfo)
        => typeInfo
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Where(p => IsMethodBelongingToType(p.DeclaringType, typeInfo))
            .ToArray();

    private static EventInfo[] GetTypeEvents(this TypeInfo typeInfo)
        => typeInfo
            .GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Where(e => IsMethodBelongingToType(e.DeclaringType, typeInfo))
            .ToArray();

    /// <summary>
    /// Determines whether a member's declaring type belongs to the specified type.
    /// This handles both regular classes and generic base classes.
    /// </summary>
    /// <param name="declaringType">The declaring type of the member.</param>
    /// <param name="typeInfo">The type we're checking against.</param>
    /// <returns>True if the member belongs to the type; otherwise, false.</returns>
    private static bool IsMethodBelongingToType(Type? declaringType, TypeInfo typeInfo)
    {
        if (declaringType == null) return false;
        
        // Direct match - for regular classes
        if (declaringType == typeInfo) return true;
        
        // For generic base classes, check if the declaring type is assignable from the type
        // This handles cases where methods are inherited from generic base classes
        if (declaringType.IsAssignableFrom(typeInfo)) return true;
        
        return false;
    }
}