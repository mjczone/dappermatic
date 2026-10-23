// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MJCZone.DapperMatic.Migrations;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Migrations;

/// <summary>
/// Unit tests for <see cref="DmMigrationOptionsExtensions.UseLogger"/>.
/// </summary>
public class DmMigrationOptionsExtensionsTests
{
    [Fact]
    public void Events_are_logged_at_their_level_with_their_kind_and_table()
    {
        var logger = new CapturingLogger();
        var options = new DmMigrationOptions().UseLogger(logger);

        options.Log!(Event(DmMigrationEventKind.TableUnchanged, DmMigrationEventLevel.Debug, "Table t is unchanged.", "t"));
        options.Log!(Event(DmMigrationEventKind.WaitingForLock, DmMigrationEventLevel.Information, "Waiting."));
        options.Log!(Event(DmMigrationEventKind.TablePending, DmMigrationEventLevel.Warning, "Table t is pending.", "t"));

        logger.Entries.Select(e => e.Level).Should().Equal(LogLevel.Debug, LogLevel.Information, LogLevel.Warning);
        logger.Entries.Select(e => e.EventId.Name).Should().Equal("TableUnchanged", "WaitingForLock", "TablePending");
        logger.Entries.Select(e => e.Message).Should().Equal("Table t is unchanged.", "Waiting.", "Table t is pending.");

        logger.Entries[0].State.Should().Equal(
            new KeyValuePair<string, object?>("Message", "Table t is unchanged."),
            new KeyValuePair<string, object?>("TableKey", "t"),
            new KeyValuePair<string, object?>("{OriginalFormat}", "{Message}")
        );

        // An event about no table in particular has no TableKey.
        logger.Entries[1].State.Should().Equal(
            new KeyValuePair<string, object?>("Message", "Waiting."),
            new KeyValuePair<string, object?>("{OriginalFormat}", "{Message}")
        );
    }

    [Fact]
    public void Each_kind_of_event_has_its_own_named_event_id()
    {
        var kinds = Enum.GetValues<DmMigrationEventKind>();
        var ids = kinds.Select(DmMigrationOptionsExtensions.GetEventId).ToList();

        ids.Select(id => id.Id).Should().OnlyHaveUniqueItems().And.NotContain(0);
        ids.Select(id => id.Name).Should().Equal(kinds.Select(k => k.ToString()));
    }

    [Fact]
    public void Events_below_the_logger_level_are_not_written()
    {
        var logger = new CapturingLogger { MinimumLevel = LogLevel.Information };
        var options = new DmMigrationOptions().UseLogger(logger);

        options.Log!(Event(DmMigrationEventKind.LockAcquired, DmMigrationEventLevel.Debug, "Took the lock."));
        options.Log!(Event(DmMigrationEventKind.Completed, DmMigrationEventLevel.Information, "Finished."));

        logger.Entries.Select(e => e.EventId.Name).Should().Equal("Completed");
    }

    [Fact]
    public void The_logger_is_added_to_a_callback_already_set()
    {
        var received = new List<DmMigrationEvent>();
        var logger = new CapturingLogger();
        var options = new DmMigrationOptions { Log = received.Add }.UseLogger(logger);

        options.Log!(Event(DmMigrationEventKind.Completed, DmMigrationEventLevel.Information, "Finished."));

        received.Should().ContainSingle();
        logger.Entries.Should().ContainSingle();
    }

    [Fact]
    public async Task A_migration_run_logs_through_the_logger_Async()
    {
        await using var db = new SqliteConnection("Data Source=:memory:");
        await db.OpenAsync();

        var table = new DmTable(
            null,
            "logger_adapter_test",
            [new DmColumn(null, "logger_adapter_test", "id", typeof(int), isPrimaryKey: true)]
        );
        var logger = new CapturingLogger();
        var migrator = new DmMigrator(new DmMigrationOptions().UseLogger(logger)).AddTable(table);

        var result = await migrator.MigrateAsync(db);

        result.IsComplete.Should().BeTrue();
        logger.Entries.Select(e => e.EventId.Name).Should().ContainInOrder("LedgerCreated", "TableReconciled", "Completed");
        logger
            .Entries.Single(e => e.EventId.Name == "TableReconciled")
            .State.Should()
            .Contain(new KeyValuePair<string, object?>("TableKey", result.Tables[0].State.TableKey));

        // Nothing changed, so the second run takes the fast path and says so at Debug.
        logger.Entries.Clear();
        await migrator.MigrateAsync(db);

        logger.Entries.Single(e => e.EventId.Name == "TableUnchanged").Level.Should().Be(LogLevel.Debug);
    }

    private static DmMigrationEvent Event(
        DmMigrationEventKind kind,
        DmMigrationEventLevel level,
        string message,
        string? tableKey = null
    ) => new(kind, level, message, tableKey);

    private sealed record Entry(
        LogLevel Level,
        EventId EventId,
        string Message,
        List<KeyValuePair<string, object?>> State
    );

    /// <summary>
    /// Records what it is given, and reports levels below <see cref="MinimumLevel"/> as off.
    /// </summary>
    private sealed class CapturingLogger : ILogger
    {
        public LogLevel MinimumLevel { get; init; } = LogLevel.Trace;

        public List<Entry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= MinimumLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            var values = state as IEnumerable<KeyValuePair<string, object?>> ?? [];
            Entries.Add(new Entry(logLevel, eventId, formatter(state, exception), values.ToList()));
        }
    }
}
