// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace MJCZone.DapperMatic.Migrations.Internal;

/// <summary>
/// Reads and writes the rename history kept in the ledger, as a JSON array.
/// </summary>
/// <remarks>
/// During a deploy where two versions of an application share a database, the older version's model still has
/// the names the newer one renamed away. Reconciling it would create them again, empty, beside the renamed
/// ones. The history lets the migrator recognise that model by its fingerprint and leave the table alone.
/// </remarks>
internal static class RenameHistory
{
    /// <summary>
    /// How many entries are kept per table. Overlapping deploys involve the two most recent models, so a short
    /// history is all that is ever consulted; the rest is there for whoever reads the ledger.
    /// </summary>
    public const int MaxEntries = 20;

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Parses a stored history. Anything unreadable is treated as no history, since the history only ever
    /// prevents work and never causes it.
    /// </summary>
    /// <param name="json">The stored history, or null.</param>
    /// <returns>The entries, oldest first.</returns>
    public static List<RenameHistoryEntry> Parse(string? json)
    {
        var entries = new List<RenameHistoryEntry>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return entries;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return entries;
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var supersedes =
                    element.TryGetProperty("supersedes", out var s) && s.ValueKind == JsonValueKind.String
                        ? s.GetString()
                        : null;

                var at =
                    element.TryGetProperty("at", out var a)
                    && a.ValueKind == JsonValueKind.String
                    && DateTime.TryParse(
                        a.GetString(),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                        out var parsed
                    )
                        ? parsed
                        : default;

                var renames =
                    element.TryGetProperty("renames", out var r) && r.ValueKind == JsonValueKind.Array
                        ? r.EnumerateArray()
                            .Where(x => x.ValueKind == JsonValueKind.String)
                            .Select(x => x.GetString()!)
                            .ToList()
                        : [];

                entries.Add(new RenameHistoryEntry(supersedes, at, renames));
            }
        }
        catch (JsonException)
        {
            entries.Clear();
        }

        return entries;
    }

    /// <summary>
    /// Writes a history for storage, keeping the most recent <see cref="MaxEntries"/> entries.
    /// </summary>
    /// <param name="entries">The entries, oldest first.</param>
    /// <returns>The JSON, or null when there are no entries.</returns>
    public static string? Serialize(IReadOnlyCollection<RenameHistoryEntry> entries)
    {
        if (entries.Count == 0)
        {
            return null;
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartArray();
            foreach (var entry in entries.Skip(Math.Max(0, entries.Count - MaxEntries)))
            {
                writer.WriteStartObject();
                if (entry.Supersedes is null)
                {
                    writer.WriteNull("supersedes");
                }
                else
                {
                    writer.WriteString("supersedes", entry.Supersedes);
                }

                writer.WriteString(
                    "at",
                    DateTime.SpecifyKind(entry.At, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture)
                );
                writer.WriteStartArray("renames");
                foreach (var rename in entry.Renames)
                {
                    writer.WriteStringValue(rename);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
