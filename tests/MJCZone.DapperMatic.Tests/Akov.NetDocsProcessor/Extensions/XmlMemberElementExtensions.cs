// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Akov.NetDocsProcessor.Output;
using Akov.NetDocsProcessor.Xml;
using Microsoft.CodeAnalysis;

namespace Akov.NetDocsProcessor.Extensions;

internal static class XmlMemberElementExtensions
{
    public static void FillBy(this IXmlMemberBaseElement element, XmlMember xmlMember)
    {
        element.Summary = xmlMember.Summary?.InnerXml?.Trim();
        element.Example = xmlMember.Example?.InnerXml?.Trim();
        element.Remarks = xmlMember.Remarks?.InnerXml?.Trim();
        element.TypeParameters = xmlMember
            .TypeParameters?.Select(t => new TypeParameterInfo { Name = t.Name, Text = t.Text })
            .ToList();

        if (element is IXmlMemberElement member)
        {
            member.Exceptions = xmlMember
                .Exceptions?.Select(e => new ExceptionInfo
                {
                    Text = e.Text,
                    Reference = e.Reference
                })
                .ToList();

            member.Parameters = xmlMember
                .Parameters?.Select(t => new ParameterInfo { Name = t.Name, Text = t.Text })
                .ToList();

            member.Returns = xmlMember.Returns?.InnerXml;
        }
    }
    
    public static void FillBy(this IXmlMemberBaseElement element, XmlMember xmlMember, ISymbol? symbol)
    {
        // First, fill with XML information
        element.FillBy(xmlMember);
        
        // Then, enhance parameter information with type data from symbol
        if (element is IXmlMemberElement member && symbol != null)
        {
            var symbolParameters = symbol.GetParameterTypes();
            if (symbolParameters != null && member.Parameters != null)
            {
                // Merge type information from symbol with XML comment text
                foreach (var parameter in member.Parameters)
                {
                    var symbolParam = symbolParameters.FirstOrDefault(p => p.Name == parameter.Name);
                    if (symbolParam != null)
                    {
                        parameter.Type = symbolParam.Type;
                    }
                }
            }
            else if (symbolParameters != null && member.Parameters == null)
            {
                // If no XML parameters but symbol has parameters, create them with type info only
                member.Parameters = symbolParameters;
            }
        }
    }
}
