// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Xml.Serialization;

namespace Akov.NetDocsProcessor.Xml;

[Serializable, XmlRoot("doc")]
public class XmlDoc
{
    [XmlElement("assembly")]
    public XmlAssembly? Assembly { get; set; }
    
    [XmlArray("members")]
    [XmlArrayItem("member")]
    public List<XmlMember>? Members { get; set; }
}