// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Reflection;
using Akov.NetDocsProcessor.Common;
using Akov.NetDocsProcessor.Extensions;
using Akov.NetDocsProcessor.Output;
using Microsoft.CodeAnalysis;
using TypeInfo = System.Reflection.TypeInfo;

namespace Akov.NetDocsProcessor.Helpers;

internal partial class DescriptionHelper
{
    public static NamespaceDescription CreateNamespace(
        string currentNamespace,
        string rootNamespace
    ) =>
        new()
        {
            Self = new PageInfo
            {
                DisplayName = currentNamespace,
                Url = currentNamespace.TrimRoot(rootNamespace),
                ElementType = ElementType.Namespace,
            },
        };

    public static TypeDescription CreateType(
        TypeInfo typeInfo,
        INamedTypeSymbol? symbol,
        PageInfo @namespace
    )
    {
        var elementType = typeInfo.GetTypeElementType();

        // Extract inheritance information
        PageInfo? baseType = null;
        List<PageInfo>? implementedInterfaces = new List<PageInfo>();

        if (symbol != null)
        {
            // Get base type (exclude System.Object and System.ValueType)
            if (
                symbol.BaseType != null
                && symbol.BaseType.Name != "Object"
                && symbol.BaseType.Name != "ValueType"
            )
            {
                var baseNamespaceUrl = symbol.BaseType.ContainingNamespace.ToDisplayString();
                baseType = new PageInfo
                {
                    DisplayName = symbol.BaseType.Name,
                    Url = Path.Combine(baseNamespaceUrl, symbol.BaseType.Name),
                    ElementType = ElementType.Class,
                };
            }

            // Get implemented interfaces
            if (symbol.Interfaces.Length > 0)
            {
                implementedInterfaces = new List<PageInfo>();
                foreach (var interfaceSymbol in symbol.Interfaces)
                {
                    var interfaceNamespaceUrl =
                        interfaceSymbol.ContainingNamespace.ToDisplayString();
                    implementedInterfaces.Add(
                        new PageInfo
                        {
                            DisplayName = interfaceSymbol.Name,
                            Url = Path.Combine(interfaceNamespaceUrl, interfaceSymbol.Name),
                            ElementType = ElementType.Interface,
                        }
                    );
                }
            }
        }

        // Generate fallback comment ID from reflection when symbol is null
        var commentId = symbol?.GetDocumentationCommentId();
        if (string.IsNullOrEmpty(commentId))
        {
            commentId = typeInfo.GenerateXmlCommentId();
        }

        return new()
        {
            ElementType = elementType,
            Name = typeInfo.Name,
            FullName = typeInfo.FullName ?? typeInfo.Name,
            CommentId = commentId ?? Texts.XmlCommentNotFound,
            Self = new PageInfo
            {
                DisplayName = symbol?.Name ?? typeInfo.Name,
                Url = Path.Combine(@namespace.Url, typeInfo.GetTypeName()),
                ElementType = elementType,
            },
            Namespace = @namespace,
            BaseType = baseType,
            ImplementedInterfaces = implementedInterfaces,
            PayloadInfo = symbol?.GetPayload() ?? new PayloadInfo(),
        };
    }

    public static MemberDescription CreateMember(
        string memberName,
        string urlName,
        MemberTypes memberType,
        ISymbol? symbol,
        PageInfo parent,
        MemberInfo? reflectionMember = null
    )
    {
        string GetMemberFolder() =>
            memberType == MemberTypes.Property
                ? "properties"
                : $"{memberType.ToString().ToLower()}s";

        // Generate fallback comment ID from reflection when symbol is null
        var commentId = symbol?.GetDocumentationCommentId();
        if (string.IsNullOrEmpty(commentId) && reflectionMember != null)
        {
            commentId = reflectionMember switch
            {
                MethodBase method => method.GenerateXmlCommentId(),
                PropertyInfo property => property.GenerateXmlCommentId(),
                FieldInfo field => field.GenerateXmlCommentId(),
                EventInfo eventInfo => eventInfo.GenerateXmlCommentId(),
                _ => Texts.XmlCommentNotFound,
            };
        }

        return new()
        {
            Self = new PageInfo
            {
                DisplayName = symbol.GetDisplayName() ?? memberName,
                Url = Path.Combine(parent.Url, GetMemberFolder(), urlName),
            },
            CommentId = commentId ?? Texts.XmlCommentNotFound,
            MemberType = memberType.ToString(),
            Name = memberName,
            ReturnType = symbol?.GetReturnType(),
            Parent = parent,
            Title = symbol?.GetShortName(),
            PayloadInfo = symbol?.GetPayload() ?? new PayloadInfo(),
            Symbol = symbol,
        };
    }

    // Overload for backward compatibility - uses memberName for both display and URL
    public static MemberDescription CreateMember(
        string memberName,
        MemberTypes memberType,
        ISymbol? symbol,
        PageInfo parent,
        MemberInfo? reflectionMember = null
    ) => CreateMember(memberName, memberName, memberType, symbol, parent, reflectionMember);

    public static EnumMemberDescription CreateEnumMember(ISymbol symbol, PageInfo parent) =>
        new()
        {
            Name = symbol.Name,
            CommentId = symbol.GetDocumentationCommentId() ?? Texts.XmlCommentNotFound,
            Parent = parent,
        };
}
