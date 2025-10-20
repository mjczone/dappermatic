// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Akov.NetDocsProcessor.Extensions;
using Akov.NetDocsProcessor.Output;
using Akov.NetDocsProcessor.Xml;

namespace Akov.NetDocsProcessor.Helpers;

internal partial class DescriptionHelper
{
    public static void UpdateTypeSummary(TypeDescription typeDescription, List<XmlMember> members)
    {
        var typeData = members.FirstOrDefault(m => m.Name == typeDescription.CommentId);
        if (typeData is null)
            return;

        typeDescription.FillBy(typeData);
    }

    public static void UpdateMembers(List<MemberDescription> memberDescriptions, List<XmlMember> members)
    {
        foreach (var memberDescription in memberDescriptions)
        {
            var memberData = members.FirstOrDefault(m => m.Name == memberDescription.CommentId);
            if (memberData is null)
                continue;

            memberDescription.FillBy(memberData, memberDescription.Symbol);
        }
    }

    public static void UpdateEnumMembers(List<EnumMemberDescription> memberDescriptions, List<XmlMember> members)
    {
        foreach (var memberDescription in memberDescriptions)
        {
            var memberData = members.FirstOrDefault(m => m.Name == memberDescription.CommentId);
            if (memberData is null)
                continue;

            memberDescription.FillBy(memberData);
        }
    }
}
