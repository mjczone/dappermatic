// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace Akov.NetDocsProcessor.Output;

public interface IXmlMemberElement : IXmlMemberBaseElement
{
    public string? Returns { get; set; }
    public List<ExceptionInfo>? Exceptions { get; set; }
    public List<ParameterInfo>? Parameters { get; set; }
}
