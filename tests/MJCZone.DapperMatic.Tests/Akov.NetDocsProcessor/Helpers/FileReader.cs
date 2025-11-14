// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Reflection;
using System.Xml.Serialization;
using Akov.NetDocsProcessor.Xml;

namespace Akov.NetDocsProcessor.Helpers;

internal class FileReader
{
    public static XmlDoc ReadXml(string path)
    {
        var serializer = new XmlSerializer(typeof(XmlDoc));
        using var reader = new StreamReader(path);
        var doc = (XmlDoc?)serializer.Deserialize(reader);
        return doc ?? throw new InvalidOperationException($"The xml file {path} was not deserialized correctly");
    }

    public static Assembly ReadAssembly(string path) => Assembly.LoadFile(path);
}
