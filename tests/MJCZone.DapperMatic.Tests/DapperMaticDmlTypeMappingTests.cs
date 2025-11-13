// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Xml.Linq;
using Dapper;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.TypeMapping;
using Xunit;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// Base test class for DapperMatic DML type mapping functionality.
/// Tests run sequentially (non-parallel) due to global Dapper type mapping state.
/// </summary>
[Collection("DapperMaticDmlTests")]
public abstract class DapperMaticDmlTypeMappingTests : TestBase
{
    protected DapperMaticDmlTypeMappingTests(ITestOutputHelper output)
        : base(output) { }

    /// <summary>
    /// Opens a database connection for the specific provider.
    /// Implemented by provider-specific test classes.
    /// </summary>
    public abstract Task<IDbConnection> OpenConnectionAsync();

    [Fact]
    protected virtual async Task Should_map_columns_using_dmcolumn_attribute_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Create table with database column names different from property names
        var table = new DmTable
        {
            TableName = "test_users",
            Columns =
            [
                new DmColumn("user_id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("first_name", typeof(string), length: 100),
                new DmColumn("last_name", typeof(string), length: 100),
                new DmColumn("email_address", typeof(string), length: 255),
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Insert data using database column names
        await db.ExecuteAsync(
            "INSERT INTO test_users (first_name, last_name, email_address) VALUES (@firstName, @lastName, @email)",
            new
            {
                firstName = "John",
                lastName = "Doe",
                email = "john.doe@example.com",
            }
        );

        // Query and map to class with DmColumn attributes
        var users = (
            await db.QueryAsync<TestUserWithDmColumn>(
                "SELECT user_id, first_name, last_name, email_address FROM test_users"
            )
        ).ToList();

        Assert.Single(users);
        Assert.True(users[0].UserId > 0);
        Assert.Equal("John", users[0].FirstName);
        Assert.Equal("Doe", users[0].LastName);
        Assert.Equal("john.doe@example.com", users[0].EmailAddress);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_users");
    }

    [Fact]
    protected virtual async Task Should_support_modern_records_with_parameterized_constructors_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping with record support
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions
            {
                EnableRecordSupport = true,
                HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting,
            }
        );

        // Create table
        var table = new DmTable
        {
            TableName = "test_products",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("name", typeof(string), length: 100),
                new DmColumn("price", typeof(decimal), precision: 10, scale: 2),
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Insert data
        await db.ExecuteAsync(
            "INSERT INTO test_products (name, price) VALUES (@name, @price)",
            new { name = "Widget", price = 19.99m }
        );

        // Query and map to record with parameterized constructor
        var products = (await db.QueryAsync<TestProductRecord>("SELECT id, name, price FROM test_products")).ToList();

        Assert.Single(products);
        Assert.True(products[0].Id > 0);
        Assert.Equal("Widget", products[0].Name);
        Assert.Equal(19.99m, products[0].Price);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_products");
    }

    [Fact]
    protected virtual async Task Should_ignore_properties_with_dmignore_attribute_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Create table
        var table = new DmTable
        {
            TableName = "test_employees",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("name", typeof(string), length: 100),
                new DmColumn("salary", typeof(decimal), precision: 10, scale: 2),
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Insert data including salary column
        await db.ExecuteAsync(
            "INSERT INTO test_employees (name, salary) VALUES (@name, @salary)",
            new { name = "Alice", salary = 75000m }
        );

        // Query including salary column (but it should be ignored in mapping)
        var employees = (
            await db.QueryAsync<TestEmployeeWithIgnore>("SELECT id, name, salary FROM test_employees")
        ).ToList();

        Assert.Single(employees);
        Assert.True(employees[0].Id > 0);
        Assert.Equal("Alice", employees[0].Name);
        // Salary should not be set (ignored) - default value is 0
        Assert.Equal(0m, employees[0].Salary);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_employees");
    }

    [Fact]
    protected virtual async Task Should_support_ef_core_column_attribute_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Create table with database column names different from property names
        var table = new DmTable
        {
            TableName = "test_customers",
            Columns =
            [
                new DmColumn("customer_id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("full_name", typeof(string), length: 100),
                new DmColumn("email_addr", typeof(string), length: 255),
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Insert data using database column names
        await db.ExecuteAsync(
            "INSERT INTO test_customers (full_name, email_addr) VALUES (@name, @email)",
            new { name = "Bob Smith", email = "bob@example.com" }
        );

        // Query and map to class with EF Core Column attributes
        var customers = (
            await db.QueryAsync<TestCustomerWithEfCoreAttribute>(
                "SELECT customer_id, full_name, email_addr FROM test_customers"
            )
        ).ToList();

        Assert.Single(customers);
        Assert.True(customers[0].CustomerId > 0);
        Assert.Equal("Bob Smith", customers[0].FullName);
        Assert.Equal("bob@example.com", customers[0].EmailAddress);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_customers");
    }

    [Fact]
    protected virtual async Task Should_ignore_properties_with_ef_core_notmapped_attribute_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Create table
        var table = new DmTable
        {
            TableName = "test_orders",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("total", typeof(decimal), precision: 10, scale: 2),
                new DmColumn("tax", typeof(decimal), precision: 10, scale: 2),
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Insert data including tax column
        await db.ExecuteAsync(
            "INSERT INTO test_orders (total, tax) VALUES (@total, @tax)",
            new { total = 100.00m, tax = 8.50m }
        );

        // Query including tax column (but it should be ignored in mapping)
        var orders = (
            await db.QueryAsync<TestOrderWithEfCoreNotMapped>("SELECT id, total, tax FROM test_orders")
        ).ToList();

        Assert.Single(orders);
        Assert.True(orders[0].Id > 0);
        Assert.Equal(100.00m, orders[0].Total);
        // Tax should not be set (ignored) - default value is 0
        Assert.Equal(0m, orders[0].Tax);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_orders");
    }

    [Fact]
    protected virtual async Task Should_support_custom_type_handler_for_string_enum_mapping_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Remove default enum type handling, then register custom type handler
        // This ensures our string-based handler overrides the default integer-based enum handling
        SqlMapper.RemoveTypeMap(typeof(OrderStatus));
        SqlMapper.AddTypeHandler(new OrderStatusStringTypeHandler());

        // Create table with VARCHAR column for enum storage (legacy database scenario)
        var table = new DmTable
        {
            TableName = "test_orders_with_string_enum",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("order_number", typeof(string), length: 50),
                new DmColumn("status", typeof(string), length: 20), // VARCHAR, not INT
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Insert test data - manually convert enum to string for parameters
        // (Dapper's type handlers primarily work for reading, not writing parameters)
        await db.ExecuteAsync(
            "INSERT INTO test_orders_with_string_enum (order_number, status) VALUES (@orderNumber, @status)",
            new { orderNumber = "ORD-001", status = OrderStatus.Pending.ToString() }
        );

        await db.ExecuteAsync(
            "INSERT INTO test_orders_with_string_enum (order_number, status) VALUES (@orderNumber, @status)",
            new { orderNumber = "ORD-002", status = OrderStatus.Shipped.ToString() }
        );

        await db.ExecuteAsync(
            "INSERT INTO test_orders_with_string_enum (order_number, status) VALUES (@orderNumber, @status)",
            new { orderNumber = "ORD-003", status = OrderStatus.Delivered.ToString() }
        );

        // Query data - custom type handler converts strings back to enums
        var orders = (
            await db.QueryAsync<TestOrderWithStringEnum>(
                "SELECT id, order_number, status FROM test_orders_with_string_enum ORDER BY id"
            )
        ).ToList();

        // Verify mapping worked correctly
        Assert.Equal(3, orders.Count);

        Assert.Equal("ORD-001", orders[0].OrderNumber);
        Assert.Equal(OrderStatus.Pending, orders[0].Status);

        Assert.Equal("ORD-002", orders[1].OrderNumber);
        Assert.Equal(OrderStatus.Shipped, orders[1].Status);

        Assert.Equal("ORD-003", orders[2].OrderNumber);
        Assert.Equal(OrderStatus.Delivered, orders[2].Status);

        // Verify database actually stores strings (not integers)
        var statusValues = (
            await db.QueryAsync<string>("SELECT status FROM test_orders_with_string_enum ORDER BY id")
        ).ToList();

        Assert.Equal("Pending", statusValues[0]);
        Assert.Equal("Shipped", statusValues[1]);
        Assert.Equal("Delivered", statusValues[2]);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_orders_with_string_enum");
    }

    // Verification test removed - confirmed XDocument requires custom handler during development.
    // Keeping this test causes conflicts due to Dapper's global type handler registration.
    // The implementation test below validates the handler works correctly across all providers.

    [Fact]
    protected virtual async Task Should_support_xdocument_for_xml_data_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping with XDocumentTypeHandler
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Create table with TEXT/VARCHAR column for XML data
        var table = new DmTable
        {
            TableName = "test_documents",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("title", typeof(string), length: 100),
                new DmColumn("metadata", typeof(XDocument), isNullable: true), // XDocument column (nullable)
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Create test XML data with complex structure
        var testXml = new XDocument(
            new XElement(
                "metadata",
                new XElement("author", "John Doe"),
                new XElement("category", "Technology"),
                new XElement(
                    "tags",
                    new XElement("tag", "xml"),
                    new XElement("tag", "database"),
                    new XElement("tag", "testing")
                ),
                new XElement("created", "2025-01-31"),
                new XElement("rating", "5")
            )
        );

        // Insert data using XDocument - handler should serialize to string
        await db.ExecuteAsync(
            "INSERT INTO test_documents (title, metadata) VALUES (@title, @metadata)",
            new { title = "Test Document", metadata = testXml }
        );

        // Query data - handler should deserialize string back to XDocument
        var documents = (
            await db.QueryAsync<TestDocumentWithXml>("SELECT id, title, metadata FROM test_documents")
        ).ToList();

        // Verify results
        Assert.Single(documents);
        Assert.True(documents[0].Id > 0);
        Assert.Equal("Test Document", documents[0].Title);
        Assert.NotNull(documents[0].Metadata);

        // Verify XML structure was preserved
        var metadata = documents[0].Metadata!;
        Assert.NotNull(metadata.Root);
        Assert.Equal("metadata", metadata.Root.Name.LocalName);

        // Verify author element
        var authorElement = metadata.Root.Element("author");
        Assert.NotNull(authorElement);
        Assert.Equal("John Doe", authorElement.Value);

        // Verify category element
        var categoryElement = metadata.Root.Element("category");
        Assert.NotNull(categoryElement);
        Assert.Equal("Technology", categoryElement.Value);

        // Verify tags element and children
        var tagsElement = metadata.Root.Element("tags");
        Assert.NotNull(tagsElement);
        var tags = tagsElement.Elements("tag").Select(e => e.Value).ToList();
        Assert.Equal(3, tags.Count);
        Assert.Contains("xml", tags);
        Assert.Contains("database", tags);
        Assert.Contains("testing", tags);

        // Verify other elements
        var createdElement = metadata.Root.Element("created");
        Assert.NotNull(createdElement);
        Assert.Equal("2025-01-31", createdElement.Value);

        var ratingElement = metadata.Root.Element("rating");
        Assert.NotNull(ratingElement);
        Assert.Equal("5", ratingElement.Value);

        // Test null XDocument handling
        await db.ExecuteAsync(
            "INSERT INTO test_documents (title, metadata) VALUES (@title, @metadata)",
            new { title = "Document Without Metadata", metadata = (XDocument?)null }
        );

        var documentsWithNull = (
            await db.QueryAsync<TestDocumentWithXml>(
                "SELECT id, title, metadata FROM test_documents WHERE title = @title",
                new { title = "Document Without Metadata" }
            )
        ).ToList();

        Assert.Single(documentsWithNull);
        Assert.Null(documentsWithNull[0].Metadata);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_documents");
    }

    // Verification tests removed - confirmed JSON/Dictionary/List require custom handlers during development.
    // Keeping these tests would cause conflicts due to Dapper's global type handler registration.
    // The implementation tests below validate the handlers work correctly across all providers.

    [Fact]
    protected virtual async Task Should_support_jsondocument_for_json_data_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping with JsonDocumentTypeHandler
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Create table with TEXT/VARCHAR column for JSON data
        var table = new DmTable
        {
            TableName = "test_json_documents",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("title", typeof(string), length: 100),
                new DmColumn("json_data", typeof(JsonDocument), isNullable: true),
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Create test JSON data with complex structure
        var testJson = JsonDocument.Parse(
            """
            {
                "name": "John Doe",
                "age": 30,
                "email": "john@example.com",
                "address": {
                    "street": "123 Main St",
                    "city": "Springfield",
                    "zip": "12345"
                },
                "tags": ["developer", "tester", "architect"]
            }
            """
        );

        // Insert data using JsonDocument - handler should serialize to JSON string
        await db.ExecuteAsync(
            "INSERT INTO test_json_documents (title, json_data) VALUES (@title, @jsonData)",
            new { title = "Test Document", jsonData = testJson }
        );

        // Query data - handler should deserialize JSON string back to JsonDocument
        var documents = (
            await db.QueryAsync<TestDocumentWithJson>("SELECT id, title, json_data FROM test_json_documents")
        ).ToList();

        // Verify results
        Assert.Single(documents);
        Assert.True(documents[0].Id > 0);
        Assert.Equal("Test Document", documents[0].Title);
        Assert.NotNull(documents[0].JsonData);

        // Verify JSON structure was preserved
        var jsonData = documents[0].JsonData!;
        Assert.Equal("John Doe", jsonData.RootElement.GetProperty("name").GetString());
        Assert.Equal(30, jsonData.RootElement.GetProperty("age").GetInt32());
        Assert.Equal("john@example.com", jsonData.RootElement.GetProperty("email").GetString());
        Assert.Equal("Springfield", jsonData.RootElement.GetProperty("address").GetProperty("city").GetString());
        Assert.Equal(3, jsonData.RootElement.GetProperty("tags").GetArrayLength());

        // Test null handling
        await db.ExecuteAsync(
            "INSERT INTO test_json_documents (title, json_data) VALUES (@title, @jsonData)",
            new { title = "Document Without JSON", jsonData = (JsonDocument?)null }
        );

        var documentsWithNull = (
            await db.QueryAsync<TestDocumentWithJson>(
                "SELECT id, title, json_data FROM test_json_documents WHERE title = @title",
                new { title = "Document Without JSON" }
            )
        ).ToList();

        Assert.Single(documentsWithNull);
        Assert.Null(documentsWithNull[0].JsonData);

        // Cleanup
        testJson.Dispose();
        await db.DropTableIfExistsAsync(null, "test_json_documents");
    }

    [Fact]
    protected virtual async Task Should_support_dictionary_for_json_data_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping with DictionaryTypeHandler
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Create table with TEXT/VARCHAR column for dictionary data
        // Note: We use string type for DDL to ensure TEXT/VARCHAR column across all databases
        // The DictionaryTypeHandler will serialize Dictionary to JSON and handle the mapping
        var table = new DmTable
        {
            TableName = "test_dict_documents",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("title", typeof(string), length: 100),
                new DmColumn("settings", typeof(string), length: 4000, isNullable: true),
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Create test dictionary with key-value pairs
        var testDict = new Dictionary<string, string>
        {
            ["theme"] = "dark",
            ["language"] = "en",
            ["timezone"] = "UTC",
            ["notifications"] = "enabled",
        };

        // Insert data using Dictionary - handler should serialize to JSON string
        await db.ExecuteAsync(
            "INSERT INTO test_dict_documents (title, settings) VALUES (@title, @settings)",
            new { title = "User Settings", settings = testDict }
        );

        // Query data - handler should deserialize JSON string back to Dictionary
        var documents = (
            await db.QueryAsync<TestDocumentWithDictionary>("SELECT id, title, settings FROM test_dict_documents")
        ).ToList();

        // Verify results
        Assert.Single(documents);
        Assert.True(documents[0].Id > 0);
        Assert.Equal("User Settings", documents[0].Title);
        Assert.NotNull(documents[0].Settings);
        Assert.Equal(4, documents[0].Settings!.Count);

        // Verify dictionary contents
        Assert.Equal("dark", documents[0].Settings!["theme"]);
        Assert.Equal("en", documents[0].Settings!["language"]);
        Assert.Equal("UTC", documents[0].Settings!["timezone"]);
        Assert.Equal("enabled", documents[0].Settings!["notifications"]);

        // Test null handling
        await db.ExecuteAsync(
            "INSERT INTO test_dict_documents (title, settings) VALUES (@title, @settings)",
            new { title = "Document Without Settings", settings = (Dictionary<string, string>?)null }
        );

        var documentsWithNull = (
            await db.QueryAsync<TestDocumentWithDictionary>(
                "SELECT id, title, settings FROM test_dict_documents WHERE title = @title",
                new { title = "Document Without Settings" }
            )
        ).ToList();

        Assert.Single(documentsWithNull);
        Assert.Null(documentsWithNull[0].Settings);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_dict_documents");
    }

    [Fact]
    protected virtual async Task Should_support_list_for_json_data_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping with ListTypeHandler
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Create table with TEXT/VARCHAR column for list data
        var table = new DmTable
        {
            TableName = "test_list_documents",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("title", typeof(string), length: 100),
                new DmColumn("tags", typeof(List<string>), isNullable: true),
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Create test list with multiple items
        var testList = new List<string> { "programming", "database", "testing", "architecture", "performance" };

        // Insert data using List - handler should serialize to JSON array string
        await db.ExecuteAsync(
            "INSERT INTO test_list_documents (title, tags) VALUES (@title, @tags)",
            new { title = "Article Tags", tags = testList }
        );

        // Query data - handler should deserialize JSON array string back to List
        var documents = (
            await db.QueryAsync<TestDocumentWithList>("SELECT id, title, tags FROM test_list_documents")
        ).ToList();

        // Verify results
        Assert.Single(documents);
        Assert.True(documents[0].Id > 0);
        Assert.Equal("Article Tags", documents[0].Title);
        Assert.NotNull(documents[0].Tags);
        Assert.Equal(5, documents[0].Tags!.Count);

        // Verify list contents
        Assert.Equal("programming", documents[0].Tags![0]);
        Assert.Equal("database", documents[0].Tags![1]);
        Assert.Equal("testing", documents[0].Tags![2]);
        Assert.Equal("architecture", documents[0].Tags![3]);
        Assert.Equal("performance", documents[0].Tags![4]);

        // Test null handling
        await db.ExecuteAsync(
            "INSERT INTO test_list_documents (title, tags) VALUES (@title, @tags)",
            new { title = "Document Without Tags", tags = (List<string>?)null }
        );

        var documentsWithNull = (
            await db.QueryAsync<TestDocumentWithList>(
                "SELECT id, title, tags FROM test_list_documents WHERE title = @title",
                new { title = "Document Without Tags" }
            )
        ).ToList();

        Assert.Single(documentsWithNull);
        Assert.Null(documentsWithNull[0].Tags);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_list_documents");
    }

    [Fact]
    protected virtual async Task Should_support_string_array_with_smart_handler_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping with SmartArrayTypeHandler
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Create table with array column
        // PostgreSQL: Uses native text[] array type
        // Other providers: Uses JSON array serialization in TEXT/VARCHAR column
        var table = new DmTable
        {
            TableName = "test_string_arrays",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("title", typeof(string), length: 100),
                new DmColumn("tags", typeof(string[]), isNullable: true),
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Create test array
        var testTags = new[] { "csharp", "database", "orm", "dapper", "testing" };

        // Insert data using string array
        // PostgreSQL: SmartArrayTypeHandler passes array directly (native)
        // Other providers: SmartArrayTypeHandler serializes to JSON
        await db.ExecuteAsync(
            "INSERT INTO test_string_arrays (title, tags) VALUES (@title, @tags)",
            new { title = "Blog Post", tags = testTags }
        );

        // Query data - handler deserializes appropriately per provider
        var documents = (
            await db.QueryAsync<TestDocumentWithStringArray>("SELECT id, title, tags FROM test_string_arrays")
        ).ToList();

        // Verify results
        Assert.Single(documents);
        Assert.True(documents[0].Id > 0);
        Assert.Equal("Blog Post", documents[0].Title);
        Assert.NotNull(documents[0].Tags);
        Assert.Equal(5, documents[0].Tags!.Length);

        // Verify array contents and order preservation
        Assert.Equal("csharp", documents[0].Tags![0]);
        Assert.Equal("database", documents[0].Tags![1]);
        Assert.Equal("orm", documents[0].Tags![2]);
        Assert.Equal("dapper", documents[0].Tags![3]);
        Assert.Equal("testing", documents[0].Tags![4]);

        // Test null handling
        await db.ExecuteAsync(
            "INSERT INTO test_string_arrays (title, tags) VALUES (@title, @tags)",
            new { title = "Document Without Tags", tags = (string[]?)null }
        );

        var documentsWithNull = (
            await db.QueryAsync<TestDocumentWithStringArray>(
                "SELECT id, title, tags FROM test_string_arrays WHERE title = @title",
                new { title = "Document Without Tags" }
            )
        ).ToList();

        Assert.Single(documentsWithNull);
        Assert.Null(documentsWithNull[0].Tags);

        // Test empty array handling
        await db.ExecuteAsync(
            "INSERT INTO test_string_arrays (title, tags) VALUES (@title, @tags)",
            new { title = "Empty Tags", tags = Array.Empty<string>() }
        );

        var documentsWithEmpty = (
            await db.QueryAsync<TestDocumentWithStringArray>(
                "SELECT id, title, tags FROM test_string_arrays WHERE title = @title",
                new { title = "Empty Tags" }
            )
        ).ToList();

        Assert.Single(documentsWithEmpty);
        Assert.NotNull(documentsWithEmpty[0].Tags);
        Assert.Empty(documentsWithEmpty[0].Tags!);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_string_arrays");
    }

    [Fact]
    protected virtual async Task Should_support_int_array_with_smart_handler_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping with SmartArrayTypeHandler
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Create table with int array column
        // PostgreSQL: Uses native int4[] array type
        // Other providers: Uses JSON array serialization
        var table = new DmTable
        {
            TableName = "test_int_arrays",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("title", typeof(string), length: 100),
                new DmColumn("scores", typeof(int[]), isNullable: true),
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Create test array with various integer values
        var testScores = new[] { 95, 87, 92, 88, 100, 76, 84 };

        // Insert data using int array
        await db.ExecuteAsync(
            "INSERT INTO test_int_arrays (title, scores) VALUES (@title, @scores)",
            new { title = "Test Scores", scores = testScores }
        );

        // Query data
        var documents = (
            await db.QueryAsync<TestDocumentWithIntArray>("SELECT id, title, scores FROM test_int_arrays")
        ).ToList();

        // Verify results
        Assert.Single(documents);
        Assert.True(documents[0].Id > 0);
        Assert.Equal("Test Scores", documents[0].Title);
        Assert.NotNull(documents[0].Scores);
        Assert.Equal(7, documents[0].Scores!.Length);

        // Verify array contents and order preservation
        Assert.Equal(95, documents[0].Scores![0]);
        Assert.Equal(87, documents[0].Scores![1]);
        Assert.Equal(92, documents[0].Scores![2]);
        Assert.Equal(88, documents[0].Scores![3]);
        Assert.Equal(100, documents[0].Scores![4]);
        Assert.Equal(76, documents[0].Scores![5]);
        Assert.Equal(84, documents[0].Scores![6]);

        // Test null handling
        await db.ExecuteAsync(
            "INSERT INTO test_int_arrays (title, scores) VALUES (@title, @scores)",
            new { title = "No Scores", scores = (int[]?)null }
        );

        var documentsWithNull = (
            await db.QueryAsync<TestDocumentWithIntArray>(
                "SELECT id, title, scores FROM test_int_arrays WHERE title = @title",
                new { title = "No Scores" }
            )
        ).ToList();

        Assert.Single(documentsWithNull);
        Assert.Null(documentsWithNull[0].Scores);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_int_arrays");
    }

    [Fact]
    protected virtual async Task Should_support_datetime_array_with_smart_handler_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping with SmartArrayTypeHandler
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Create table with DateTime array column
        // PostgreSQL: Uses native timestamp[] array type
        // Other providers: Uses JSON array serialization
        var table = new DmTable
        {
            TableName = "test_datetime_arrays",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("title", typeof(string), length: 100),
                new DmColumn("timestamps", typeof(DateTime[]), isNullable: true),
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Create test array with DateTime values
        var testTimestamps = new[]
        {
            new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            new DateTime(2025, 2, 20, 14, 45, 0, DateTimeKind.Utc),
            new DateTime(2025, 3, 25, 18, 15, 0, DateTimeKind.Utc),
        };

        // Insert data using DateTime array
        await db.ExecuteAsync(
            "INSERT INTO test_datetime_arrays (title, timestamps) VALUES (@title, @timestamps)",
            new { title = "Event Timeline", timestamps = testTimestamps }
        );

        // Query data
        var documents = (
            await db.QueryAsync<TestDocumentWithDateTimeArray>("SELECT id, title, timestamps FROM test_datetime_arrays")
        ).ToList();

        // Verify results
        Assert.Single(documents);
        Assert.True(documents[0].Id > 0);
        Assert.Equal("Event Timeline", documents[0].Title);
        Assert.NotNull(documents[0].Timestamps);
        Assert.Equal(3, documents[0].Timestamps!.Length);

        // Verify array contents (allowing for minor precision differences)
        Assert.Equal(testTimestamps![0].Year, documents[0].Timestamps![0].Year);
        Assert.Equal(testTimestamps![0].Month, documents[0].Timestamps![0].Month);
        Assert.Equal(testTimestamps![0].Day, documents[0].Timestamps![0].Day);
        Assert.Equal(testTimestamps![0].Hour, documents[0].Timestamps![0].Hour);
        Assert.Equal(testTimestamps![0].Minute, documents[0].Timestamps![0].Minute);

        Assert.Equal(testTimestamps![1].Year, documents[0].Timestamps![1].Year);
        Assert.Equal(testTimestamps[1].Month, documents[0].Timestamps![1].Month);
        Assert.Equal(testTimestamps![1].Day, documents[0].Timestamps![1].Day);

        Assert.Equal(testTimestamps[2].Year, documents[0].Timestamps![2].Year);
        Assert.Equal(testTimestamps[2].Month, documents[0].Timestamps![2].Month);
        Assert.Equal(testTimestamps[2].Day, documents[0].Timestamps![2].Day);

        // Test null handling
        await db.ExecuteAsync(
            "INSERT INTO test_datetime_arrays (title, timestamps) VALUES (@title, @timestamps)",
            new { title = "No Timestamps", timestamps = (DateTime[]?)null }
        );

        var documentsWithNull = (
            await db.QueryAsync<TestDocumentWithDateTimeArray>(
                "SELECT id, title, timestamps FROM test_datetime_arrays WHERE title = @title",
                new { title = "No Timestamps" }
            )
        ).ToList();

        Assert.Single(documentsWithNull);
        Assert.Null(documentsWithNull[0].Timestamps);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_datetime_arrays");
    }

    // TODO: INVESTIGATE: SmartIPAddressTypeHandler does not seem to be invoked for some providers (e.g., SQL Server).
    [Fact]
    protected virtual async Task Should_support_ipaddress_with_smart_handler_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping with SmartIPAddressTypeHandler
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Create table with IPAddress column
        // PostgreSQL: Uses native inet type
        // Other providers: Uses string serialization
        var table = new DmTable
        {
            TableName = "test_ip_addresses",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("title", typeof(string), length: 100),
                new DmColumn("ip_address", typeof(IPAddress), isNullable: true),
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Create test IPAddress values
        var testIPv4 = IPAddress.Parse("192.168.1.100");
        var testIPv6 = IPAddress.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7334");

        // Insert IPv4 document
        await db.ExecuteAsync(
            "INSERT INTO test_ip_addresses (title, ip_address) VALUES (@title, @ip_address)",
            new { title = "IPv4 Address", ip_address = testIPv4 }
        );

        // Insert IPv6 document
        await db.ExecuteAsync(
            "INSERT INTO test_ip_addresses (title, ip_address) VALUES (@title, @ip_address)",
            new { title = "IPv6 Address", ip_address = testIPv6 }
        );

        // Insert null IP
        await db.ExecuteAsync(
            "INSERT INTO test_ip_addresses (title, ip_address) VALUES (@title, @ip_address)",
            new { title = "No IP", ip_address = (IPAddress?)null }
        );

        // Query IPv4 back
        var ipv4Documents = (
            await db.QueryAsync<TestDocumentWithIPAddress>(
                "SELECT id, title, ip_address FROM test_ip_addresses WHERE title = @title",
                new { title = "IPv4 Address" }
            )
        ).ToList();

        Assert.Single(ipv4Documents);
        Assert.NotNull(ipv4Documents[0].IPAddress);
        Assert.Equal(testIPv4, ipv4Documents[0].IPAddress);

        // Query IPv6 back
        var ipv6Documents = (
            await db.QueryAsync<TestDocumentWithIPAddress>(
                "SELECT id, title, ip_address FROM test_ip_addresses WHERE title = @title",
                new { title = "IPv6 Address" }
            )
        ).ToList();

        Assert.Single(ipv6Documents);
        Assert.NotNull(ipv6Documents[0].IPAddress);
        Assert.Equal(testIPv6, ipv6Documents[0].IPAddress);

        // Query null IP back
        var documentsWithNull = (
            await db.QueryAsync<TestDocumentWithIPAddress>(
                "SELECT id, title, ip_address FROM test_ip_addresses WHERE title = @title",
                new { title = "No IP" }
            )
        ).ToList();

        Assert.Single(documentsWithNull);
        Assert.Null(documentsWithNull[0].IPAddress);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_ip_addresses");
    }

    [Fact]
    protected virtual async Task Should_support_physicaladdress_with_smart_handler_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping with SmartPhysicalAddressTypeHandler
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Create table with PhysicalAddress column
        // PostgreSQL: Uses native macaddr type
        // Other providers: Uses string serialization
        var table = new DmTable
        {
            TableName = "test_mac_addresses",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("title", typeof(string), length: 100),
                new DmColumn("mac_address", typeof(PhysicalAddress), isNullable: true),
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Create test PhysicalAddress values
        var testMac1 = PhysicalAddress.Parse("00-11-22-33-44-55");
        var testMac2 = PhysicalAddress.Parse("AA-BB-CC-DD-EE-FF");

        // Insert MAC address 1
        await db.ExecuteAsync(
            "INSERT INTO test_mac_addresses (title, mac_address) VALUES (@title, @mac_address)",
            new { title = "Device 1", mac_address = testMac1 }
        );

        // Insert MAC address 2
        await db.ExecuteAsync(
            "INSERT INTO test_mac_addresses (title, mac_address) VALUES (@title, @mac_address)",
            new { title = "Device 2", mac_address = testMac2 }
        );

        // Insert null MAC
        await db.ExecuteAsync(
            "INSERT INTO test_mac_addresses (title, mac_address) VALUES (@title, @mac_address)",
            new { title = "No MAC", mac_address = (PhysicalAddress?)null }
        );

        // Query MAC 1 back
        var mac1Documents = (
            await db.QueryAsync<TestDocumentWithPhysicalAddress>(
                "SELECT id, title, mac_address FROM test_mac_addresses WHERE title = @title",
                new { title = "Device 1" }
            )
        ).ToList();

        Assert.Single(mac1Documents);
        Assert.NotNull(mac1Documents[0].MacAddress);
        Assert.Equal(testMac1, mac1Documents[0].MacAddress);

        // Query MAC 2 back
        var mac2Documents = (
            await db.QueryAsync<TestDocumentWithPhysicalAddress>(
                "SELECT id, title, mac_address FROM test_mac_addresses WHERE title = @title",
                new { title = "Device 2" }
            )
        ).ToList();

        Assert.Single(mac2Documents);
        Assert.NotNull(mac2Documents[0].MacAddress);
        Assert.Equal(testMac2, mac2Documents[0].MacAddress);

        // Query null MAC back
        var documentsWithNull = (
            await db.QueryAsync<TestDocumentWithPhysicalAddress>(
                "SELECT id, title, mac_address FROM test_mac_addresses WHERE title = @title",
                new { title = "No MAC" }
            )
        ).ToList();

        Assert.Single(documentsWithNull);
        Assert.Null(documentsWithNull[0].MacAddress);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_mac_addresses");
    }

    [Fact]
    protected virtual async Task Should_support_npgsqlcidr_with_smart_handler_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping with SmartNpgsqlCidrTypeHandler
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Get NpgsqlCidr type via reflection
        var npgsqlCidrType = Type.GetType("NpgsqlTypes.NpgsqlCidr, Npgsql");
        if (npgsqlCidrType == null)
        {
            Output.WriteLine("NpgsqlCidr type not available, skipping test");
            return;
        }

        // Create table with NpgsqlCidr column
        // PostgreSQL: Uses native cidr type
        // Other providers: Uses string serialization (CIDR notation)
        var table = new DmTable
        {
            TableName = "test_cidr_networks",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("title", typeof(string), length: 100),
                new DmColumn("network", npgsqlCidrType, isNullable: true),
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Create test CIDR values using reflection
        var parseMethod = npgsqlCidrType.GetMethod("Parse", new[] { typeof(string) });
        if (parseMethod == null)
        {
            Output.WriteLine("NpgsqlCidr.Parse method not available, skipping test");
            return;
        }

        var testIPv4Cidr = parseMethod.Invoke(null, new object[] { "192.168.0.0/24" });
        var testIPv6Cidr = parseMethod.Invoke(null, new object[] { "2001:db8::/32" });

        // Insert IPv4 CIDR document
        await db.ExecuteAsync(
            "INSERT INTO test_cidr_networks (title, network) VALUES (@title, @network)",
            new { title = "IPv4 Network", network = testIPv4Cidr }
        );

        // Insert IPv6 CIDR document
        await db.ExecuteAsync(
            "INSERT INTO test_cidr_networks (title, network) VALUES (@title, @network)",
            new { title = "IPv6 Network", network = testIPv6Cidr }
        );

        // Insert null CIDR
        await db.ExecuteAsync(
            "INSERT INTO test_cidr_networks (title, network) VALUES (@title, @network)",
            new { title = "No Network", network = (object?)null }
        );

        // Query IPv4 CIDR back
        var ipv4Documents = (
            await db.QueryAsync<TestDocumentWithNpgsqlCidr>(
                "SELECT id, title, network FROM test_cidr_networks WHERE title = @title",
                new { title = "IPv4 Network" }
            )
        ).ToList();

        Assert.Single(ipv4Documents);
        Assert.NotNull(ipv4Documents[0].Network);
        Assert.Equal(testIPv4Cidr!.ToString(), ipv4Documents[0].Network!.ToString());

        // Query IPv6 CIDR back
        var ipv6Documents = (
            await db.QueryAsync<TestDocumentWithNpgsqlCidr>(
                "SELECT id, title, network FROM test_cidr_networks WHERE title = @title",
                new { title = "IPv6 Network" }
            )
        ).ToList();

        Assert.Single(ipv6Documents);
        Assert.NotNull(ipv6Documents[0].Network);
        Assert.IsType<string>(ipv6Documents[0].Network);
        Assert.Equal(testIPv6Cidr!.ToString(), ipv6Documents[0].Network!.ToString());

        // Query IPv6 CIDR back
        var ipv6DocumentsTyped = (
            await db.QueryAsync<TestDocumentWithNpgsqlCidrTyped>(
                "SELECT id, title, network FROM test_cidr_networks WHERE title = @title",
                new { title = "IPv6 Network" }
            )
        ).ToList();

        Assert.Single(ipv6DocumentsTyped);
        Assert.NotNull(ipv6DocumentsTyped[0].Network);
        Assert.NotNull(testIPv6Cidr as NpgsqlTypes.NpgsqlCidr?);
        Assert.Equal(((NpgsqlTypes.NpgsqlCidr)testIPv6Cidr).Address, ipv6DocumentsTyped[0].Network!.Value.Address);
        Assert.Equal(((NpgsqlTypes.NpgsqlCidr)testIPv6Cidr).Netmask, ipv6DocumentsTyped[0].Network!.Value.Netmask);

        // Query IPv6 CIDR back
        var ipv6DocumentsProviderTyped = (
            await db.QueryAsync<TestDocumentWithNpgsqlCidrProviderTyped>(
                "SELECT id, title, network FROM test_cidr_networks WHERE title = @title",
                new { title = "IPv6 Network" }
            )
        ).ToList();

        Assert.Single(ipv6DocumentsProviderTyped);
        Assert.NotNull(ipv6DocumentsProviderTyped[0].Network);
        Assert.IsType<NpgsqlTypes.NpgsqlCidr>(ipv6DocumentsProviderTyped[0].Network);
        Assert.NotNull(testIPv6Cidr as NpgsqlTypes.NpgsqlCidr?);
        Assert.NotNull(ipv6DocumentsProviderTyped[0].Network! as NpgsqlTypes.NpgsqlCidr?);
        var npgsqlCidrObj = (NpgsqlTypes.NpgsqlCidr)ipv6DocumentsProviderTyped[0].Network!;
        Assert.Equal(((NpgsqlTypes.NpgsqlCidr)testIPv6Cidr).Address, npgsqlCidrObj.Address);
        Assert.Equal(((NpgsqlTypes.NpgsqlCidr)testIPv6Cidr).Netmask, npgsqlCidrObj.Netmask);

        // Query null CIDR back
        var documentsWithNull = (
            await db.QueryAsync<TestDocumentWithNpgsqlCidr>(
                "SELECT id, title, network FROM test_cidr_networks WHERE title = @title",
                new { title = "No Network" }
            )
        ).ToList();

        Assert.Single(documentsWithNull);
        Assert.Null(documentsWithNull[0].Network);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_cidr_networks");
    }

    [Fact]
    protected virtual async Task Should_support_numeric_arrays_with_smart_handler_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping with SmartArrayTypeHandler
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Create table with multiple numeric array columns
        var table = new DmTable
        {
            TableName = "test_numeric_arrays",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("long_values", typeof(long[]), isNullable: true),
                new DmColumn("short_values", typeof(short[]), isNullable: true),
                new DmColumn("double_values", typeof(double[]), isNullable: true),
                new DmColumn("float_values", typeof(float[]), isNullable: true),
                new DmColumn("decimal_values", typeof(decimal[]), isNullable: true),
                new DmColumn("bool_values", typeof(bool[]), isNullable: true),
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Insert test data
        var testData = new
        {
            longValues = new long[] { 1000L, 2000L, 3000L },
            shortValues = new short[] { (short)10, (short)20, (short)30 },
            doubleValues = new double[] { 1.5, 2.5, 3.5 },
            floatValues = new float[] { 1.1f, 2.2f, 3.3f },
            decimalValues = new decimal[] { 10.5m, 20.5m, 30.5m },
            boolValues = new bool[] { true, false, true },
        };

        await db.ExecuteAsync(
            "INSERT INTO test_numeric_arrays (long_values, short_values, double_values, float_values, decimal_values, bool_values) "
                + "VALUES (@longValues, @shortValues, @doubleValues, @floatValues, @decimalValues, @boolValues)",
            testData
        );

        // Query data back
        var results = (
            await db.QueryAsync<TestDocumentWithNumericArrays>(
                "SELECT id, long_values, short_values, double_values, float_values, decimal_values, bool_values FROM test_numeric_arrays"
            )
        ).ToList();

        // Verify results
        Assert.Single(results);
        Assert.NotNull(results[0].LongValues);
        Assert.Equal(3, results[0].LongValues!.Length);
        Assert.Equal(1000L, results[0].LongValues![0]);

        Assert.NotNull(results[0].ShortValues);
        Assert.Equal(3, results[0].ShortValues!.Length);
        Assert.Equal((short)10, results[0].ShortValues![0]);

        Assert.NotNull(results[0].DoubleValues);
        Assert.Equal(3, results[0].DoubleValues!.Length);

        Assert.NotNull(results[0].FloatValues);
        Assert.Equal(3, results[0].FloatValues!.Length);

        Assert.NotNull(results[0].DecimalValues);
        Assert.Equal(3, results[0].DecimalValues!.Length);
        Assert.Equal(10.5m, results[0].DecimalValues![0]);

        Assert.NotNull(results[0].BoolValues);
        Assert.Equal(3, results[0].BoolValues!.Length);
        Assert.True(results[0].BoolValues![0]);
        Assert.False(results[0].BoolValues![1]);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_numeric_arrays");
    }

    [Fact]
    protected virtual async Task Should_support_temporal_and_guid_arrays_with_smart_handler_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Create table with temporal and GUID array columns
        var table = new DmTable
        {
            TableName = "test_temporal_arrays",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("guid_values", typeof(Guid[]), isNullable: true),
                new DmColumn("datetimeoffset_values", typeof(DateTimeOffset[]), isNullable: true),
                new DmColumn("dateonly_values", typeof(DateOnly[]), isNullable: true),
                new DmColumn("timeonly_values", typeof(TimeOnly[]), isNullable: true),
                new DmColumn("timespan_values", typeof(TimeSpan[]), isNullable: true),
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Insert test data
        var testData = new
        {
            guidValues = new[] { Guid.NewGuid(), Guid.NewGuid() },
            datetimeoffsetValues = new[] { DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1) },
            dateonlyValues = new[] { new DateOnly(2025, 1, 15), new DateOnly(2025, 1, 16) },
            timeonlyValues = new[] { new TimeOnly(10, 30, 0), new TimeOnly(18, 45, 30) },
            timespanValues = new[] { TimeSpan.FromHours(1), TimeSpan.FromHours(2) },
        };

        await db.ExecuteAsync(
            "INSERT INTO test_temporal_arrays (guid_values, datetimeoffset_values, dateonly_values, timeonly_values, timespan_values) "
                + "VALUES (@guidValues, @datetimeoffsetValues, @dateonlyValues, @timeonlyValues, @timespanValues)",
            testData
        );

        // Query data back
        var results = (
            await db.QueryAsync<TestDocumentWithTemporalArrays>(
                "SELECT id, guid_values, datetimeoffset_values, dateonly_values, timeonly_values, timespan_values FROM test_temporal_arrays"
            )
        ).ToList();

        // Verify results
        Assert.Single(results);
        Assert.NotNull(results[0].GuidValues);
        Assert.Equal(2, results[0].GuidValues!.Length);

        Assert.NotNull(results[0].DateTimeOffsetValues);
        Assert.Equal(2, results[0].DateTimeOffsetValues!.Length);

        Assert.NotNull(results[0].DateOnlyValues);
        Assert.Equal(2, results[0].DateOnlyValues!.Length);

        Assert.NotNull(results[0].TimeOnlyValues);
        Assert.Equal(2, results[0].TimeOnlyValues!.Length);

        Assert.NotNull(results[0].TimeSpanValues);
        Assert.Equal(2, results[0].TimeSpanValues!.Length);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_temporal_arrays");
    }

    [Fact]
    protected virtual async Task Should_support_npgsql_range_types_with_smart_handler_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping with NpgsqlRange handlers
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Get NpgsqlRange types via reflection
        var rangeIntType = Type.GetType("NpgsqlTypes.NpgsqlRange`1[[System.Int32, System.Private.CoreLib]], Npgsql");
        var rangeLongType = Type.GetType("NpgsqlTypes.NpgsqlRange`1[[System.Int64, System.Private.CoreLib]], Npgsql");
        var rangeDecimalType = Type.GetType(
            "NpgsqlTypes.NpgsqlRange`1[[System.Decimal, System.Private.CoreLib]], Npgsql"
        );
        var rangeDateTimeType = Type.GetType(
            "NpgsqlTypes.NpgsqlRange`1[[System.DateTime, System.Private.CoreLib]], Npgsql"
        );
        var rangeDateOnlyType = Type.GetType(
            "NpgsqlTypes.NpgsqlRange`1[[System.DateOnly, System.Private.CoreLib]], Npgsql"
        );
        var rangeDateTimeOffsetType = Type.GetType(
            "NpgsqlTypes.NpgsqlRange`1[[System.DateTimeOffset, System.Private.CoreLib]], Npgsql"
        );

        // Skip test if Npgsql types not available
        if (rangeIntType == null)
        {
            Output.WriteLine("NpgsqlRange types not available, skipping test");
            return;
        }

        // Create table with range columns
        var table = new DmTable
        {
            TableName = "test_range_types",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("int_range", rangeIntType, isNullable: true),
                new DmColumn("long_range", rangeLongType, isNullable: true),
                new DmColumn("decimal_range", rangeDecimalType, isNullable: true),
                new DmColumn("datetime_range", rangeDateTimeType, isNullable: true),
                new DmColumn("dateonly_range", rangeDateOnlyType, isNullable: true),
                new DmColumn("datetimeoffset_range", rangeDateTimeOffsetType, isNullable: true),
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Create range instances using reflection
        var createRange = (Type rangeType, object lower, object upper) =>
        {
            var ctor = rangeType.GetConstructor(new[] { lower.GetType(), upper.GetType() });
            return ctor?.Invoke(new[] { lower, upper });
        };

        var intRange = createRange(rangeIntType!, 1, 10);
        var longRange = createRange(rangeLongType!, 1000L, 2000L);
        var decimalRange = createRange(rangeDecimalType!, 10.5m, 20.5m);
        var datetimeRange = createRange(
            rangeDateTimeType!,
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            DateTime.SpecifyKind(DateTime.UtcNow.AddDays(7), DateTimeKind.Unspecified)
        );
        var dateonlyRange = createRange(rangeDateOnlyType!, new DateOnly(2025, 1, 15), new DateOnly(2025, 1, 22));
        var datetimeoffsetRange = createRange(
            rangeDateTimeOffsetType!,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7)
        );

        // Insert test data
        await db.ExecuteAsync(
            "INSERT INTO test_range_types (int_range, long_range, decimal_range, datetime_range, dateonly_range, datetimeoffset_range) "
                + "VALUES (@intRange, @longRange, @decimalRange, @datetimeRange, @dateonlyRange, @datetimeoffsetRange)",
            new
            {
                intRange,
                longRange,
                decimalRange,
                datetimeRange,
                dateonlyRange,
                datetimeoffsetRange,
            }
        );

        // Query data back
        var results = (
            await db.QueryAsync<TestDocumentWithRangeTypes>(
                "SELECT id, int_range, long_range, decimal_range, datetime_range, dateonly_range, datetimeoffset_range FROM test_range_types"
            )
        ).ToList();

        // Verify results
        Assert.Single(results);
        Assert.NotNull(results[0].IntRange);
        Assert.NotNull(results[0].LongRange);
        Assert.NotNull(results[0].DecimalRange);
        Assert.NotNull(results[0].DateTimeRange);
        Assert.NotNull(results[0].DateOnlyRange);
        Assert.NotNull(results[0].DateTimeOffsetRange);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_range_types");
    }

    [Fact]
    protected virtual async Task Should_support_npgsql_geometric_types_with_smart_handler_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping with Npgsql geometric handlers
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Get Npgsql geometric types via reflection
        var npgsqlPointType = Type.GetType("NpgsqlTypes.NpgsqlPoint, Npgsql");
        var npgsqlBoxType = Type.GetType("NpgsqlTypes.NpgsqlBox, Npgsql");
        var npgsqlCircleType = Type.GetType("NpgsqlTypes.NpgsqlCircle, Npgsql");
        var npgsqlLineType = Type.GetType("NpgsqlTypes.NpgsqlLine, Npgsql");
        var npgsqlLSegType = Type.GetType("NpgsqlTypes.NpgsqlLSeg, Npgsql");
        var npgsqlPathType = Type.GetType("NpgsqlTypes.NpgsqlPath, Npgsql");
        var npgsqlPolygonType = Type.GetType("NpgsqlTypes.NpgsqlPolygon, Npgsql");

        // Skip test if Npgsql types not available
        if (npgsqlPointType == null)
        {
            Output.WriteLine("Npgsql geometric types not available, skipping test");
            return;
        }

        // Create table with geometric columns
        var table = new DmTable
        {
            TableName = "test_npgsql_geometric_types",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("point_value", npgsqlPointType, isNullable: true),
                new DmColumn("box_value", npgsqlBoxType, isNullable: true),
                new DmColumn("circle_value", npgsqlCircleType, isNullable: true),
                new DmColumn("line_value", npgsqlLineType, isNullable: true),
                new DmColumn("lseg_value", npgsqlLSegType, isNullable: true),
                new DmColumn("path_value", npgsqlPathType, isNullable: true),
                new DmColumn("polygon_value", npgsqlPolygonType, isNullable: true),
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Create geometric instances using reflection
        var pointValue = npgsqlPointType!
            .GetConstructor(new[] { typeof(double), typeof(double) })!
            .Invoke(new object[] { 1.0, 2.0 });
        var boxValue = npgsqlBoxType!
            .GetConstructor(new[] { typeof(double), typeof(double), typeof(double), typeof(double) })!
            .Invoke(new object[] { 0.0, 0.0, 10.0, 10.0 });
        var circleValue = npgsqlCircleType!
            .GetConstructor(new[] { typeof(double), typeof(double), typeof(double) })!
            .Invoke(new object[] { 5.0, 5.0, 3.0 });
        var lineValue = npgsqlLineType!
            .GetConstructor(new[] { typeof(double), typeof(double), typeof(double) })!
            .Invoke(new object[] { 1.0, 2.0, 3.0 });

        var point1 = npgsqlPointType!
            .GetConstructor(new[] { typeof(double), typeof(double) })!
            .Invoke(new object[] { 0.0, 0.0 });
        var point2 = npgsqlPointType!
            .GetConstructor(new[] { typeof(double), typeof(double) })!
            .Invoke(new object[] { 10.0, 10.0 });
        var lsegValue = npgsqlLSegType!
            .GetConstructor(new[] { npgsqlPointType, npgsqlPointType })!
            .Invoke(new[] { point1, point2 });

        var points = Array.CreateInstance(npgsqlPointType!, 3);
        points.SetValue(
            npgsqlPointType!
                .GetConstructor(new[] { typeof(double), typeof(double) })!
                .Invoke(new object[] { 0.0, 0.0 }),
            0
        );
        points.SetValue(
            npgsqlPointType!
                .GetConstructor(new[] { typeof(double), typeof(double) })!
                .Invoke(new object[] { 10.0, 0.0 }),
            1
        );
        points.SetValue(
            npgsqlPointType!
                .GetConstructor(new[] { typeof(double), typeof(double) })!
                .Invoke(new object[] { 5.0, 10.0 }),
            2
        );

        var pathValue = npgsqlPathType!
            .GetConstructor(new[] { points.GetType(), typeof(bool) })!
            .Invoke(new object[] { points, false });
        var polygonValue = npgsqlPolygonType!
            .GetConstructor(new[] { points.GetType() })!
            .Invoke(new object[] { points });

        // Insert test data
        await db.ExecuteAsync(
            "INSERT INTO test_npgsql_geometric_types (point_value, box_value, circle_value, line_value, lseg_value, path_value, polygon_value) "
                + "VALUES (@pointValue, @boxValue, @circleValue, @lineValue, @lsegValue, @pathValue, @polygonValue)",
            new
            {
                pointValue,
                boxValue,
                circleValue,
                lineValue,
                lsegValue,
                pathValue,
                polygonValue,
            }
        );

        // Query data back
        var results = (
            await db.QueryAsync<TestDocumentWithNpgsqlGeometricTypes>(
                "SELECT id, point_value, box_value, circle_value, line_value, lseg_value, path_value, polygon_value FROM test_npgsql_geometric_types"
            )
        ).ToList();

        // Verify results
        Assert.Single(results);
        Assert.NotNull(results[0].PointValue);
        Assert.NotNull(results[0].BoxValue);
        Assert.NotNull(results[0].CircleValue);
        Assert.NotNull(results[0].LineValue);
        Assert.NotNull(results[0].LSegValue);
        Assert.NotNull(results[0].PathValue);
        Assert.NotNull(results[0].PolygonValue);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_npgsql_geometric_types");
    }

    [Fact]
    protected virtual async Task Should_support_nettopologysuite_basic_geometries_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        var dbType = db.GetDbProviderType();

        // Skip test if PostGIS is required but not installed
        if (dbType == DbProviderType.PostgreSql)
        {
            var postgisInstalled = await db.ExecuteScalarAsync<bool>(
                "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'postgis')"
            );
            if (!postgisInstalled)
            {
                Output.WriteLine("PostGIS not installed, skipping NetTopologySuite test on PostgreSQL");
                return;
            }
        }

        // Skip test for MySQL/MariaDB - NTS requires ST_GeomFromText() wrapper which needs custom handler
        // MySqlConnector has MySqlGeometry type but no ADO.NET level NTS support package
        if (dbType == DbProviderType.MySql)
        {
            Output.WriteLine("MySQL/MariaDB NetTopologySuite support requires custom ST_GeomFromText() handling, skipping test");
            return;
        }

        // Initialize DapperMatic type mapping
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Get NetTopologySuite types via reflection
        var ntsPointType = Type.GetType("NetTopologySuite.Geometries.Point, NetTopologySuite");
        var ntsLineStringType = Type.GetType("NetTopologySuite.Geometries.LineString, NetTopologySuite");
        var ntsPolygonType = Type.GetType("NetTopologySuite.Geometries.Polygon, NetTopologySuite");

        // Skip test if NetTopologySuite not available
        if (ntsPointType == null)
        {
            Output.WriteLine("NetTopologySuite not available, skipping test");
            return;
        }

        // Create table with NTS geometry columns
        var table = new DmTable
        {
            TableName = "test_nts_basic_geometries",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("title", typeof(string), length: 100),
                new DmColumn("point_geom", ntsPointType, isNullable: true),
                new DmColumn("line_geom", ntsLineStringType, isNullable: true),
                new DmColumn("polygon_geom", ntsPolygonType, isNullable: true),
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Create NetTopologySuite geometries using WKT
        var geometryFactoryType = Type.GetType("NetTopologySuite.Geometries.GeometryFactory, NetTopologySuite");
        var wktReaderType = Type.GetType("NetTopologySuite.IO.WKTReader, NetTopologySuite");

        if (geometryFactoryType == null || wktReaderType == null)
        {
            Output.WriteLine("NetTopologySuite geometry creation types not available, skipping test");
            await db.DropTableIfExistsAsync(null, "test_nts_basic_geometries");
            return;
        }

        var factory = Activator.CreateInstance(geometryFactoryType);
        var reader = Activator.CreateInstance(wktReaderType, factory);
        var readMethod = wktReaderType.GetMethod("Read", new[] { typeof(string) });

        var pointGeom = readMethod!.Invoke(reader, new object[] { "POINT(1 2)" });
        var lineGeom = readMethod!.Invoke(reader, new object[] { "LINESTRING(0 0, 10 10, 20 20)" });
        var polygonGeom = readMethod!.Invoke(reader, new object[] { "POLYGON((0 0, 10 0, 10 10, 0 10, 0 0))" });

        // Insert test data
        await db.ExecuteAsync(
            "INSERT INTO test_nts_basic_geometries (title, point_geom, line_geom, polygon_geom) "
                + "VALUES (@title, @pointGeom, @lineGeom, @polygonGeom)",
            new
            {
                title = "NTS Basic Geometries",
                pointGeom,
                lineGeom,
                polygonGeom,
            }
        );

        // Query data back
        var results = (
            await db.QueryAsync<TestDocumentWithNTSGeometries>(
                "SELECT id, title, point_geom, line_geom, polygon_geom FROM test_nts_basic_geometries"
            )
        ).ToList();

        // Verify results
        Assert.Single(results);
        Assert.Equal("NTS Basic Geometries", results[0].Title);
        Assert.NotNull(results[0].PointGeom);
        Assert.NotNull(results[0].LineGeom);
        Assert.NotNull(results[0].PolygonGeom);

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_nts_basic_geometries");
    }

    [Fact]
    protected virtual async Task Should_support_provider_specific_spatial_types_with_cross_provider_fallback_Async()
    {
        using var db = await OpenConnectionAsync();

        if (db.GetDbProviderType() == DbProviderType.PostgreSql)
        {
            // If PostGIS is not installed, skip the test
            var postgisInstalled = await db.ExecuteScalarAsync<bool>(
                "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'postgis')"
            );
            if (!postgisInstalled)
            {
                Output.WriteLine("PostGIS extension not installed, skipping test");
                return;
            }
        }

        await InitFreshSchemaAsync(db, null);

        // Initialize DapperMatic type mapping with provider-specific spatial handlers
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Test is conditional on provider-specific types being available
        // Try MySqlGeometry first
        var mySqlGeometryType =
            Type.GetType("MySql.Data.MySqlClient.MySqlGeometry, MySql.Data")
            ?? Type.GetType("MySqlConnector.MySqlGeometry, MySqlConnector");

        // Try SQL Server types (even though we don't support these at this time)
        var sqlGeometryType = Type.GetType("Microsoft.SqlServer.Types.SqlGeometry, Microsoft.SqlServer.Types");
        var sqlGeographyType = Type.GetType("Microsoft.SqlServer.Types.SqlGeography, Microsoft.SqlServer.Types");
        var sqlHierarchyIdType = Type.GetType("Microsoft.SqlServer.Types.SqlHierarchyId, Microsoft.SqlServer.Types");

        // Skip if no provider-specific types available
        if (mySqlGeometryType == null && sqlGeometryType == null)
        {
            Output.WriteLine("No provider-specific spatial types available, skipping test");
            return;
        }

        // Test whichever types are available
        var hasTypes = false;

        // Test MySqlGeometry if available
        if (mySqlGeometryType != null)
        {
            hasTypes = true;
            var table = new DmTable
            {
                TableName = "test_mysql_geometry",
                Columns =
                [
                    new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                    new DmColumn("geom", mySqlGeometryType, isNullable: true),
                ],
            };

            await db.CreateTableIfNotExistsAsync(table);

            // Create MySqlGeometry from WKT
            var parseMethod = mySqlGeometryType.GetMethod("Parse", new[] { typeof(string) });
            if (parseMethod != null)
            {
                var geom = parseMethod.Invoke(null, new object[] { "POINT(1 2)" });
                await db.ExecuteAsync("INSERT INTO test_mysql_geometry (geom) VALUES (@geom)", new { geom });

                var results = (await db.QueryAsync<dynamic>("SELECT id, geom FROM test_mysql_geometry")).ToList();
                Assert.Single(results);
                Assert.NotNull(results[0].geom);
            }

            await db.DropTableIfExistsAsync(null, "test_mysql_geometry");
        }

        // Test SQL Server types if available
        if (sqlHierarchyIdType != null)
        {
            hasTypes = true;
            var table = new DmTable
            {
                TableName = "test_sql_hierarchyid",
                Columns =
                [
                    new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                    new DmColumn("path", sqlHierarchyIdType, isNullable: true),
                ],
            };

            await db.CreateTableIfNotExistsAsync(table);

            // Create SqlHierarchyId from string path
            var parseMethod = sqlHierarchyIdType.GetMethod("Parse", new[] { typeof(string) });
            if (parseMethod != null)
            {
                var path = parseMethod.Invoke(null, new object[] { "/1/2/3/" });
                await db.ExecuteAsync("INSERT INTO test_sql_hierarchyid (path) VALUES (@path)", new { path });

                var results = (await db.QueryAsync<dynamic>("SELECT id, path FROM test_sql_hierarchyid")).ToList();
                Assert.Single(results);
                Assert.NotNull(results[0].path);
            }

            await db.DropTableIfExistsAsync(null, "test_sql_hierarchyid");
        }

        Assert.True(hasTypes, "At least one provider-specific type should have been tested");
    }

    #region Test Helper Classes

    /// <summary>
    /// Test class with DmColumn attributes for column name mapping.
    /// </summary>
    public class TestUserWithDmColumn
    {
        [DmColumn("user_id")]
        public int UserId { get; set; }

        [DmColumn("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [DmColumn("last_name")]
        public string LastName { get; set; } = string.Empty;

        [DmColumn("email_address")]
        public string EmailAddress { get; set; } = string.Empty;
    }

    /// <summary>
    /// Test record with parameterized constructor for modern C# support.
    /// </summary>
    public record TestProductRecord(int Id, string Name, decimal Price);

    /// <summary>
    /// Test class with DmIgnore attribute to test property exclusion.
    /// </summary>
    public class TestEmployeeWithIgnore
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        [DmIgnore]
        public decimal Salary { get; set; }
    }

    /// <summary>
    /// Test class with EF Core Column attribute for column name mapping.
    /// </summary>
    public class TestCustomerWithEfCoreAttribute
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("customer_id")]
        public int CustomerId { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Schema.Column("email_addr")]
        public string EmailAddress { get; set; } = string.Empty;
    }

    /// <summary>
    /// Test class with EF Core NotMapped attribute to test property exclusion.
    /// </summary>
    public class TestOrderWithEfCoreNotMapped
    {
        public int Id { get; set; }
        public decimal Total { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public decimal Tax { get; set; }
    }

    /// <summary>
    /// Test enum for string-based enum mapping.
    /// Used to demonstrate custom type handler for legacy databases.
    /// </summary>
    public enum OrderStatus
    {
        Pending = 0,
        Processing = 1,
        Shipped = 2,
        Delivered = 3,
    }

    /// <summary>
    /// Test class for string-based enum column mapping.
    /// Demonstrates how to map VARCHAR columns to .NET enums using custom type handlers.
    /// </summary>
    public class TestOrderWithStringEnum
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("order_number")]
        public string OrderNumber { get; set; } = string.Empty;

        [DmColumn("status")]
        public OrderStatus Status { get; set; }
    }

    /// <summary>
    /// Custom type handler that converts between database VARCHAR columns and OrderStatus enum.
    /// This demonstrates how to override DapperMatic's default integer-based enum handling
    /// for legacy databases that store enums as strings.
    /// </summary>
    public class OrderStatusStringTypeHandler : SqlMapper.TypeHandler<OrderStatus>
    {
        /// <summary>
        /// Parse database value (string) to .NET enum.
        /// </summary>
        public override OrderStatus Parse(object value)
        {
            // Handle null/DBNull
            if (value == null || value == DBNull.Value)
            {
                return OrderStatus.Pending; // Default value
            }

            // Convert string to enum
            var stringValue = value.ToString();
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return OrderStatus.Pending; // Default value
            }

            // Parse string to enum (case-insensitive)
            if (Enum.TryParse<OrderStatus>(stringValue, ignoreCase: true, out var result))
            {
                return result;
            }

            // Fallback to default if parsing fails
            return OrderStatus.Pending;
        }

        /// <summary>
        /// Set database parameter value (convert .NET enum to string).
        /// </summary>
        public override void SetValue(IDbDataParameter parameter, OrderStatus value)
        {
            // Convert enum to string for VARCHAR column
            parameter.Value = value.ToString();
        }
    }

    /// <summary>
    /// Test class for testing XDocument with custom type handler.
    /// Used to verify XDocumentTypeHandler serialization and deserialization.
    /// </summary>
    public class TestDocumentWithXml
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("title")]
        public string Title { get; set; } = string.Empty;

        [DmColumn("metadata")]
        public XDocument? Metadata { get; set; }
    }

    /// <summary>
    /// Test class for testing JsonDocument with custom type handler.
    /// Used to verify JsonDocumentTypeHandler serialization and deserialization.
    /// </summary>
    public class TestDocumentWithJson
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("title")]
        public string Title { get; set; } = string.Empty;

        [DmColumn("json_data")]
        public JsonDocument? JsonData { get; set; }
    }

    /// <summary>
    /// Test class for testing Dictionary with custom type handler.
    /// Used to verify DictionaryTypeHandler serialization and deserialization.
    /// </summary>
    public class TestDocumentWithDictionary
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("title")]
        public string Title { get; set; } = string.Empty;

        [DmColumn("settings")]
        public Dictionary<string, string>? Settings { get; set; }
    }

    /// <summary>
    /// Test class for testing List with custom type handler.
    /// Used to verify ListTypeHandler serialization and deserialization.
    /// </summary>
    public class TestDocumentWithList
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("title")]
        public string Title { get; set; } = string.Empty;

        [DmColumn("tags")]
        public List<string>? Tags { get; set; }
    }

    /// <summary>
    /// Test class for string array type handler (Phase 5).
    /// </summary>
    public class TestDocumentWithStringArray
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("title")]
        public string Title { get; set; } = string.Empty;

        [DmColumn("tags")]
        public string[]? Tags { get; set; }
    }

    /// <summary>
    /// Test class for int array type handler (Phase 5).
    /// </summary>
    public class TestDocumentWithIntArray
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("title")]
        public string Title { get; set; } = string.Empty;

        [DmColumn("scores")]
        public int[]? Scores { get; set; }
    }

    /// <summary>
    /// Test class for DateTime array type handler (Phase 5).
    /// </summary>
    public class TestDocumentWithDateTimeArray
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("title")]
        public string Title { get; set; } = string.Empty;

        [DmColumn("timestamps")]
        public DateTime[]? Timestamps { get; set; }
    }

    /// <summary>
    /// Test class for IPAddress type handler (Phase 6).
    /// </summary>
    public class TestDocumentWithIPAddress
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("title")]
        public string Title { get; set; } = string.Empty;

        [DmColumn("ip_address")]
        public IPAddress? IPAddress { get; set; }
    }

    /// <summary>
    /// Test class for PhysicalAddress type handler (Phase 6).
    /// </summary>
    public class TestDocumentWithPhysicalAddress
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("title")]
        public string Title { get; set; } = string.Empty;

        [DmColumn("mac_address")]
        public PhysicalAddress? MacAddress { get; set; }
    }

    /// <summary>
    /// Test class for NpgsqlCidr type handler (Phase 6).
    /// </summary>
    public class TestDocumentWithNpgsqlCidr
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("title")]
        public string Title { get; set; } = string.Empty;

        [DmColumn("network")]
        public NpgsqlTypes.NpgsqlCidr? Network { get; set; }
    }

    /// <summary>
    /// Test class for NpgsqlCidr type handler (Phase 6).
    /// </summary>
    public class TestDocumentWithNpgsqlCidrTyped
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("title")]
        public string Title { get; set; } = string.Empty;

        [DmColumn("network")]
        public NpgsqlTypes.NpgsqlCidr? Network { get; set; }
    }

    /// <summary>
    /// Test class for NpgsqlCidr type handler (Phase 6).
    /// </summary>
    public class TestDocumentWithNpgsqlCidrProviderTyped
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("title")]
        public string Title { get; set; } = string.Empty;

        [DmColumn("network", providerDataType: "{postgresql:cidr}")]
        public object? Network { get; set; }
    }

    /// <summary>
    /// Test class for numeric array type handlers (Phase 5).
    /// </summary>
    public class TestDocumentWithNumericArrays
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("long_values")]
        public long[]? LongValues { get; set; }

        [DmColumn("short_values")]
        public short[]? ShortValues { get; set; }

        [DmColumn("double_values")]
        public double[]? DoubleValues { get; set; }

        [DmColumn("float_values")]
        public float[]? FloatValues { get; set; }

        [DmColumn("decimal_values")]
        public decimal[]? DecimalValues { get; set; }

        [DmColumn("bool_values")]
        public bool[]? BoolValues { get; set; }
    }

    /// <summary>
    /// Test class for temporal and GUID array type handlers (Phase 5).
    /// </summary>
    public class TestDocumentWithTemporalArrays
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("guid_values")]
        public Guid[]? GuidValues { get; set; }

        [DmColumn("datetimeoffset_values")]
        public DateTimeOffset[]? DateTimeOffsetValues { get; set; }

        [DmColumn("dateonly_values")]
        public DateOnly[]? DateOnlyValues { get; set; }

        [DmColumn("timeonly_values")]
        public TimeOnly[]? TimeOnlyValues { get; set; }

        [DmColumn("timespan_values")]
        public TimeSpan[]? TimeSpanValues { get; set; }
    }

    /// <summary>
    /// Test class for NpgsqlRange type handlers (Phase 7).
    /// </summary>
    public class TestDocumentWithRangeTypes
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("int_range")]
        public NpgsqlTypes.NpgsqlRange<int>? IntRange { get; set; }

        [DmColumn("long_range")]
        public NpgsqlTypes.NpgsqlRange<long>? LongRange { get; set; }

        [DmColumn("decimal_range")]
        public NpgsqlTypes.NpgsqlRange<decimal>? DecimalRange { get; set; }

        [DmColumn("datetime_range")]
        public NpgsqlTypes.NpgsqlRange<DateTime>? DateTimeRange { get; set; }

        [DmColumn("dateonly_range")]
        public NpgsqlTypes.NpgsqlRange<DateOnly>? DateOnlyRange { get; set; }

        [DmColumn("datetimeoffset_range")]
        public NpgsqlTypes.NpgsqlRange<DateTimeOffset>? DateTimeOffsetRange { get; set; }
    }

    /// <summary>
    /// Test class for Npgsql geometric type handlers (Phase 8).
    /// </summary>
    public class TestDocumentWithNpgsqlGeometricTypes
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("point_value")]
        public NpgsqlTypes.NpgsqlPoint? PointValue { get; set; }

        [DmColumn("box_value")]
        public NpgsqlTypes.NpgsqlBox? BoxValue { get; set; }

        [DmColumn("circle_value")]
        public NpgsqlTypes.NpgsqlCircle? CircleValue { get; set; }

        [DmColumn("line_value")]
        public NpgsqlTypes.NpgsqlLine? LineValue { get; set; }

        [DmColumn("lseg_value")]
        public NpgsqlTypes.NpgsqlLSeg? LSegValue { get; set; }

        [DmColumn("path_value")]
        public NpgsqlTypes.NpgsqlPath? PathValue { get; set; }

        [DmColumn("polygon_value")]
        public NpgsqlTypes.NpgsqlPolygon? PolygonValue { get; set; }
    }

    /// <summary>
    /// Test class for NetTopologySuite geometry type handlers (Phase 9).
    /// </summary>
    public class TestDocumentWithNTSGeometries
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("title")]
        public string Title { get; set; } = string.Empty;

        [DmColumn("point_geom")]
        public NetTopologySuite.Geometries.Point? PointGeom { get; set; }

        [DmColumn("line_geom")]
        public NetTopologySuite.Geometries.LineString? LineGeom { get; set; }

        [DmColumn("polygon_geom")]
        public NetTopologySuite.Geometries.Polygon? PolygonGeom { get; set; }
    }

    #endregion // #region Test Helper Classes
}
