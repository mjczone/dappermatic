// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections;
using Microsoft.Extensions.Logging;
using MJCZone.DapperMatic.Migrations;

namespace MJCZone.DapperMatic.AspNetCore;

/// <summary>
/// Extension methods for sending the migrator's events to an <see cref="ILogger"/>.
/// </summary>
public static class DmMigrationOptionsExtensions
{
    /// <summary>
    /// Sends the migrator's events to <paramref name="logger"/>, in addition to any
    /// <see cref="DmMigrationOptions.Log"/> callback already set.
    /// </summary>
    /// <remarks>
    /// Each event is logged at its own level, with an event id named after its <see cref="DmMigrationEventKind"/>.
    /// An event about a table carries the table's ledger key as the <c>TableKey</c> property, for structured
    /// logging.
    /// </remarks>
    /// <param name="options">The migration options.</param>
    /// <param name="logger">The logger, such as an <c>ILogger&lt;DmMigrator&gt;</c> from dependency injection.</param>
    /// <returns>The migration options, for chaining.</returns>
    public static DmMigrationOptions UseLogger(this DmMigrationOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        options.Log += e => Write(logger, e);
        return options;
    }

    /// <summary>
    /// Gets the event id an event of the given kind is logged with.
    /// </summary>
    /// <param name="kind">The kind of event.</param>
    /// <returns>The event id: the kind's value plus one, since an id of zero means none, and its name.</returns>
    internal static EventId GetEventId(DmMigrationEventKind kind) => new((int)kind + 1, kind.ToString());

    private static void Write(ILogger logger, DmMigrationEvent e)
    {
        var level = e.Level switch
        {
            DmMigrationEventLevel.Debug => LogLevel.Debug,
            DmMigrationEventLevel.Warning => LogLevel.Warning,
            _ => LogLevel.Information,
        };

        if (logger.IsEnabled(level))
        {
            logger.Log(level, GetEventId(e.Kind), new EventState(e), null, static (state, _) => state.ToString());
        }
    }

    /// <summary>
    /// The state an event is logged with: its message and table key as named values, the way a message
    /// template's values are, so that structured logging providers record them as properties.
    /// </summary>
    private sealed class EventState : IReadOnlyList<KeyValuePair<string, object?>>
    {
        private readonly DmMigrationEvent _event;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventState"/> class.
        /// </summary>
        /// <param name="e">The event.</param>
        public EventState(DmMigrationEvent e)
        {
            _event = e;
        }

        /// <inheritdoc />
        public int Count => _event.TableKey is null ? 2 : 3;

        /// <inheritdoc />
        public KeyValuePair<string, object?> this[int index]
        {
            get
            {
                if (index == 0)
                {
                    return new("Message", _event.Message);
                }

                // Last, as in the state of a message template.
                if (index == Count - 1)
                {
                    return new("{OriginalFormat}", "{Message}");
                }

                if (index == 1 && _event.TableKey is not null)
                {
                    return new("TableKey", _event.TableKey);
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Returns the event's message.
        /// </summary>
        /// <returns>The message.</returns>
        public override string ToString() => _event.Message;
    }
}
