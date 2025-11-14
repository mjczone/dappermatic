// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Xml.Serialization;

namespace Akov.NetDocsProcessor.Xml;

[Serializable]
public class XmlParameter
{
    [XmlAttribute("name")]
    public string? Name { get; set; }

    [XmlText]
    public string? Text { get; set; }
}
