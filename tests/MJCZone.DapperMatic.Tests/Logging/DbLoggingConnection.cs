using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace MJCZone.DapperMatic.Tests.Logging;

public class DbLoggingConnection : DbConnection, IDbConnection
{
    public DbConnection Inner { get; }
    public int? CommandTimeout { get; set; }

    private readonly ILogger _logger;

    public DbLoggingConnection(DbConnection inner, ILogger logger)
    {
        Inner = inner;
        _logger = logger;
    }

    public DbLoggingConnection(DbConnection inner, ILogger logger, int commandTimeout)
        : this(inner, logger)
    {
        CommandTimeout = commandTimeout;
    }

    protected override DbCommand CreateDbCommand()
    {
        var cmd = Inner.CreateCommand();
        if (CommandTimeout.HasValue)
            cmd.CommandTimeout = CommandTimeout.Value;

        return new DbLoggingCommand(cmd, _logger);
    }

    protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel)
    {
        return Inner.BeginTransaction(isolationLevel);
    }

    public override void Close() => Inner.Close();

    public override void Open() => Inner.Open();

    [AllowNull]
    public override string ConnectionString
    {
        get => Inner.ConnectionString;
        set => Inner.ConnectionString = value;
    }

    public override string Database => Inner.Database;

    public override ConnectionState State => Inner.State;

    public override string DataSource => Inner.DataSource;

    public override string ServerVersion => Inner.ServerVersion;

    public override void ChangeDatabase(string databaseName)
    {
        Inner.ChangeDatabase(databaseName);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Inner?.Dispose();
        }
        base.Dispose(disposing);
    }
}
