// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.GraphQL.Schema;

/// <summary>
/// Database names as GraphQL takes them: the database's own, with every character GraphQL cannot take (anything but
/// letters, digits and <c>_</c>) made <c>_</c>; a leading digit and GraphQL's reserved <c>__</c> prefix get a letter
/// before them.
/// </summary>
internal static class GraphQLNames
{
    /// <summary>
    /// Makes a name GraphQL takes.
    /// </summary>
    /// <param name="name">The database's name.</param>
    /// <returns>The name.</returns>
    public static string Of(string name)
    {
        var chars = name.Select(c => char.IsAsciiLetterOrDigit(c) || c == '_' ? c : '_').ToArray();
        var made = chars.Length == 0 ? "_" : new string(chars);
        if (char.IsAsciiDigit(made[0]))
        {
            made = "_" + made;
        }

        // Names starting with __ are GraphQL's own (introspection).
        return made.StartsWith("__", StringComparison.Ordinal) ? "x" + made : made;
    }
}

/// <summary>
/// The names given within one scope (a schema's types, one type's fields): the first to ask for a name has it, and a
/// later one gets it with <c>_2</c>, <c>_3</c> after it. Asking in a fixed order makes the names the same for every
/// schema of the same tables.
/// </summary>
internal sealed class GraphQLNameScope
{
    private readonly HashSet<string> _taken = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQLNameScope"/> class.
    /// </summary>
    /// <param name="reserved">The names no one is given.</param>
    public GraphQLNameScope(IEnumerable<string> reserved)
    {
        foreach (var name in reserved)
        {
            _taken.Add(name);
        }
    }

    /// <summary>Gets the names given other than as asked, as (asked for, given).</summary>
    public List<(string Wanted, string Given)> Renamed { get; } = [];

    /// <summary>
    /// Gives a name: the one asked for if no one has it, else the first of it with <c>_2</c>, <c>_3</c>… after it.
    /// </summary>
    /// <param name="wanted">The name asked for, as GraphQL takes it.</param>
    /// <returns>The name given.</returns>
    public string Take(string wanted)
    {
        var name = wanted;
        for (var i = 2; !_taken.Add(name); i++)
        {
            name = $"{wanted}_{i}";
        }

        if (!string.Equals(name, wanted, StringComparison.Ordinal))
        {
            Renamed.Add((wanted, name));
        }

        return name;
    }
}
