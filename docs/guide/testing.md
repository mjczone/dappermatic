# Testing Infrastructure (Contributors)

This document describes DapperMatic's internal testing infrastructure for contributors who want to understand how the library is tested or contribute to the test suite.

## Testing Philosophy

DapperMatic's testing approach is built on **real database testing** using Docker containers rather than mocks or in-memory databases. This ensures that all DDL operations are validated against actual database engines across all supported providers.

## Test Infrastructure

### Project Structure

The test project is located at `tests/MJCZone.DapperMatic.Tests/` and includes:

- **Provider-specific test classes** for SQL Server, MySQL, PostgreSQL, and SQLite
- **Database fixtures** using Testcontainers for container management
- **Comprehensive method tests** covering all DDL operations

### Dependencies

Key testing dependencies (from `MJCZone.DapperMatic.Tests.csproj`):

```xml
<PackageReference Include="xunit" Version="2.5.3" />
<PackageReference Include="Testcontainers.MsSql" Version="3.10.0" />
<PackageReference Include="Testcontainers.MySql" Version="3.10.0" />
<PackageReference Include="Testcontainers.PostgreSql" Version="3.10.0" />
<PackageReference Include="Testcontainers.MariaDb" Version="3.10.0" />
<PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.1" />
<PackageReference Include="Microsoft.Data.SQLite" Version="8.0.7" />
<PackageReference Include="MySqlConnector" Version="2.3.7" />
<PackageReference Include="Npgsql" Version="8.0.3" />
```

### Database Fixtures

Database fixtures extend `DatabaseFixtureBase<TContainer>` and manage container lifecycle:

- **SQL Server**: Multiple version fixtures (2017, 2019, 2022)
- **MySQL**: MySQL and MariaDB fixtures
- **PostgreSQL**: PostgreSQL fixture
- **SQLite**: File-based testing (no container required)

### Test Classes

- **`TestBase`**: ([src](https://github.com/mjczone/MJCZone.DapperMatic/blob/main/tests/MJCZone.DapperMatic.Tests/TestBase.cs)) Abstract base with common testing utilities
- **`DatabaseMethodsTests`**: Abstract base for all provider-specific tests
  - `Schema Tests` ([src](https://github.com/mjczone/MJCZone.DapperMatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.Schemas.cs)): Tests related to database schema operations
  - `Table Tests` ([src](https://github.com/mjczone/MJCZone.DapperMatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.Tables.cs)): Tests related to database table operations
  - `Column Tests` ([src](https://github.com/mjczone/MJCZone.DapperMatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.Columns.cs)): Tests related to database column operations
  - `Primary Key Constraint Tests` ([src](https://github.com/mjczone/MJCZone.DapperMatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.PrimaryKeyConstraints.cs)): Tests related to database primary key constraints
  - `Unique Constraint Tests` ([src](https://github.com/mjczone/MJCZone.DapperMatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.UniqueConstraints.cs)): Tests related to database unique constraints
  - `Default Constraint Tests` ([src](https://github.com/mjczone/MJCZone.DapperMatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.DefaultConstraints.cs)): Tests related to database default constraints
  - `Check Constraint Tests` ([src](https://github.com/mjczone/MJCZone.DapperMatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.CheckConstraints.cs)): Tests related to database check constraints
  - `Index Tests` ([src](https://github.com/mjczone/MJCZone.DapperMatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.Indexes.cs)): Tests related to database indexes
  - `Table Factory Tests` ([src](https://github.com/mjczone/MJCZone.DapperMatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.TableFactory.cs)): Tests related to database table factories
  - `View Tests` ([src](https://github.com/mjczone/MJCZone.DapperMatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.Views.cs)): Tests related to database views
- Provider-specific test classes inherit from `DatabaseMethodsTests`
  - `MariaDB Tests` ([src](https://github.com/mjczone/MJCZone.DapperMatic/blob/main/tests/MJCZone.DapperMatic.Tests/ProviderTests/MariaDbDatabaseMethodsTests.cs)): Tests related to MariaDB
  - `MySQL Tests` ([src](https://github.com/mjczone/MJCZone.DapperMatic/blob/main/tests/MJCZone.DapperMatic.Tests/ProviderTests/MySqlDatabaseMethodsTests.cs)): Tests related to MySQL
  - `PostgreSQL Tests` ([src](https://github.com/mjczone/MJCZone.DapperMatic/blob/main/tests/MJCZone.DapperMatic.Tests/ProviderTests/PostgreSqlDatabaseMethodsTests.cs)): Tests related to PostgreSQL
  - `SQLite Tests` ([src](https://github.com/mjczone/MJCZone.DapperMatic/blob/main/tests/MJCZone.DapperMatic.Tests/ProviderTests/SQLiteDatabaseMethodsTests.cs)): Tests related to SQLite
  - `SQL Server Tests` ([src](https://github.com/mjczone/MJCZone.DapperMatic/blob/main/tests/MJCZone.DapperMatic.Tests/ProviderTests/SqlServerDatabaseMethodsTests.cs)): Tests related to SQL Server
