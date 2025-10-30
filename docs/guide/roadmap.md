# Roadmap

## Community-Driven Development

We believe the best features come from real user needs. **Have an idea for DapperMatic?** We'd love to hear from you!

🔗 **[Share your ideas on GitHub Discussions](https://github.com/mjczone/dappermatic/discussions/categories/ideas)**

Your feedback and feature requests directly influence our development priorities. Whether it's a new database provider, enhanced DDL capabilities, or developer tooling improvements - we want to know what would make DapperMatic more valuable for your projects.

## Current Focus Areas

While we prioritize community feedback, here are the high-level areas we're actively considering:

### 🗄️ Database Provider Support

- **Oracle Database** - Enterprise database support
- **IBM DB2** - Mainframe and enterprise system integration
- **Azure SQL Database** - Cloud-specific optimizations and features

### 🔧 Enhanced DDL Capabilities  

- **Stored Procedures & Functions** - Creation, modification, and management
- **Triggers** - Database trigger lifecycle management
- **User-Defined Types** - Custom type creation and management
- **Advanced Partitioning** - Table partitioning across providers

### 🛠️ CLI Tooling

A comprehensive command-line tool for:
- **Schema Extraction** - Export existing database schemas
- **Schema Comparison** - Compare schemas across environments
- **Automatic Updates** - Apply schema changes with confidence
- **Schema Versioning** - Track and manage schema evolution
- **Documentation Generation** - Automated schema diagrams and docs

### ✅ DML Query Support (Completed - MVP Achieved)

**Full Dapper query compatibility** is now available, enabling attribute-based mapping for QueryAsync/ExecuteAsync operations:
- ✅ **Attribute Mapping** - `DmColumn`, `DmIgnore`, EF Core, and ServiceStack.OrmLite attributes work in Dapper queries
- ✅ **Modern C# Records** - Full support for records with parameterized constructors
- ✅ **XML Support** - `XDocument` serialization across all database providers
- ✅ **JSON Support** - `JsonDocument` with PostgreSQL jsonb optimization
- ✅ **Collection Support** - `Dictionary<TKey, TValue>` and `List<T>` with JSON serialization
- ✅ **Smart Array Support** - 15 array types with PostgreSQL native arrays (10-50x faster) and JSON fallback
- ✅ **Enum Support** - Native integer handling via Dapper

See the [DML Query Support Guide](/guide/dml-query-support) for complete documentation.

### ✅ ASP.NET Core Integration (Completed)

The **MJCZone.DapperMatic.AspNetCore** package is now available with:
- ✅ **Permission-Based Security** - Customizable authorization via `IDapperMaticPermissions`
- ✅ **RESTful DDL APIs** - Full HTTP endpoints for all DapperMatic operations
- ✅ **Audit Logging** - Complete audit trail via `IDapperMaticAuditLogger`
- ✅ **Datasource Management** - Multiple repository backends (in-memory, file, database)
- ✅ **Connection String Encryption** - AES-256 encryption for sensitive data

Potential future enhancements:
- **Web Dashboard** - Browser-based UI for visual schema management
- **Schema Diff Viewer** - Visual comparison tool for schema changes
- **Migration History** - Track and rollback schema versions

## How We Prioritize

Development priorities are determined by:

1. **Community requests** - Features with the most interest get attention first
2. **Real-world usage** - Issues and needs from production environments
3. **Cross-provider consistency** - Features that work across all supported databases
4. **Maintainability** - Sustainable additions that don't compromise code quality

## Stay Informed

- 💬 **[GitHub Discussions](https://github.com/mjczone/dappermatic/discussions)** - Feature requests and community chat
- 🐛 **[GitHub Issues](https://github.com/mjczone/dappermatic/issues)** - Bug reports and specific problems
- 📋 **[Project Board](https://github.com/mjczone/dappermatic/projects)** - Current development status
- 🏷️ **[Releases](https://github.com/mjczone/dappermatic/releases)** - Latest features and improvements

---

*The roadmap evolves based on community needs and feedback. Nothing here is set in stone - your input shapes what gets built next.*