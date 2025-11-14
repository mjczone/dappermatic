// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Xml;
using System.Xml.Serialization;

namespace Akov.NetDocsProcessor.Xml;

[Serializable]
public class XmlMember
{
    [XmlAttribute("name")]
    public string? Name { get; set; }

    [XmlAnyElement("summary")]
    public XmlElement? Summary { get; set; }

    [XmlAnyElement("remarks")]
    public XmlElement? Remarks { get; set; }

    [XmlAnyElement("example")]
    public XmlElement? Example { get; set; }

    [XmlElement("exception")]
    public List<XmlException>? Exceptions { get; set; }

    [XmlElement("param")]
    public List<XmlParameter>? Parameters { get; set; }

    [XmlElement("typeparam")]
    public List<XmlTypeParameter>? TypeParameters { get; set; }

    [XmlAnyElement("returns")]
    public XmlElement? Returns { get; set; }

    [XmlElement("value")]
    public string? Value { get; set; }

    [XmlElement("seealso")]
    public List<XmlSeeAlso>? SeeAlso { get; set; }
}
