// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Akov.NetDocsProcessor.Common;

namespace Akov.NetDocsProcessor.Output;

/// <summary>
/// The namespace container.
/// </summary>
public class NamespaceDescription
{
    
#if NET7_0_OR_GREATER
    
    /// <summary>
    /// The reference to the page info.
    /// </summary>
    public required PageInfo Self { get; set; }
    
#else

    /// <summary>
    /// The reference to the page info.
    /// </summary>
    public PageInfo Self { get; set;} = default!;
    
#endif

    public ElementType ElementType => ElementType.Namespace;
    
    /// <summary>
    /// The list of type descriptions.
    /// </summary>
    public List<TypeDescription> Types { get; } = new();
}