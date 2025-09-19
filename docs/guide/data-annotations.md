# Data Annotations

DapperMatic provides comprehensive data annotation attributes that allow you to define database schema directly on your C# classes and properties. These attributes work with the factory methods to automatically generate `DmTable` and `DmView` objects from your annotated classes.

## Overview

Data annotations provide a declarative way to define your database schema using attributes. Here's how they work with DapperMatic's factory methods:

### Table Generation Example

```csharp
[DmTable("dbo", "app_users")]
[DmIndex(columnNames: new[] { "Email" }, isUnique: true, indexName: "IX_Users_Email")]
public class User
{
    [DmColumn("user_id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmColumn("email", length: 200, isNullable: false)]
    public string Email { get; set; }

    [DmColumn("full_name", length: 100, isNullable: false)]
    public string FullName { get; set; }

    [DmColumn("created_at", defaultExpression: "GETDATE()", isNullable: false)]
    public DateTime CreatedAt { get; set; }
}

// Generate a DmTable from the annotated class
DmTable userTable = DmTableFactory.GetTable(typeof(User));

// Create the table in your database
await connection.CreateTableIfNotExistsAsync("dbo", userTable);
```

### View Generation Example

```csharp
[DmView(@"
    SELECT 
        u.user_id,
        u.email,
        u.full_name,
        COUNT(o.order_id) as OrderCount
    FROM {0}.app_users u
    LEFT JOIN {0}.orders o ON u.user_id = o.user_id
    WHERE u.created_at >= DATEADD(month, -1, GETDATE())
    GROUP BY u.user_id, u.email, u.full_name",
    schemaName: "dbo",
    viewName: "vw_recent_users")]
public class RecentUserSummary
{
    public int UserId { get; set; }
    public string Email { get; set; }
    public string FullName { get; set; }
    public int OrderCount { get; set; }
}

// Generate a DmView from the annotated class
DmView recentUsersView = DmViewFactory.GetView(typeof(RecentUserSummary));

// Create the view in your database
await connection.CreateViewIfNotExistsAsync("dbo", recentUsersView);
```

### Why Use Annotations?

This approach offers several key benefits:

- **Code-first development** - Define schema alongside your domain models
- **Type safety** - Compile-time validation of your schema definitions
- **Maintainability** - Schema and code stay in sync
- **IntelliSense support** - Full IDE support with parameter hints
- **Automatic generation** - Factory methods convert your classes into DapperMatic models
- **Database creation** - Generated models work directly with DapperMatic's DDL methods

## Getting Started Without Annotations

DapperMatic can work with plain C# classes (POCOs) without any annotations using its built-in convention-based mapping. Let's start with a simple example:

```csharp
// A plain C# class without any annotations
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

// Generate a DmTable from the plain class
DmTable table = DmTableFactory.GetTable(typeof(Customer));

// What DapperMatic infers by convention:
// - Table name: "Customer" (class name)
// - Column names: "Id", "Name", "Email", "CreatedAt", "IsActive" (property names)
// - Data types: Mapped from C# types (int → INT, string → NVARCHAR, etc.)
// - Nullability: Reference types nullable, value types not nullable
// - Primary key: "Id" property (by convention)
// - Auto-increment: "Id" property if it's an integer type
```

This works great for simple scenarios, but you might want more control over your database schema. Here's where annotations become valuable:

### Why Use Annotations?

**1. Explicit Control Over Naming**
```csharp
// Without annotations - uses C# naming
public class Customer  // → "Customer" table
{
    public string Name { get; set; }  // → "Name" column
}

// With annotations - uses database naming conventions
[DmTable("dbo", "customers")]  // → "dbo.customers" table
public class Customer
{
    [DmColumn("customer_name")]  // → "customer_name" column
    public string Name { get; set; }
}
```

**2. Precise Data Type Specifications**
```csharp
// Without annotations - uses default mappings
public string Email { get; set; }  // → NVARCHAR(MAX) or similar

// With annotations - precise control
[DmColumn(length: 200, isNullable: false)]  // → NVARCHAR(200) NOT NULL
public string Email { get; set; }
```

**3. Database Constraints and Relationships**
```csharp
// Without annotations - basic table structure only
public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }  // Just a column
    public decimal Total { get; set; }
}

// With annotations - full constraint definitions
public class Order
{
    [DmColumn(isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmForeignKeyConstraint(typeof(Customer), new[] { "Id" }, 
                           constraintName: "FK_Orders_Customers")]
    public int CustomerId { get; set; }

    [DmColumn(precision: 10, scale: 2)]
    [DmCheckConstraint("CK_Orders_Total_Positive", "Total > 0")]
    public decimal Total { get; set; }
}
```

**4. Performance Optimizations**
```csharp
// Without annotations - no indexes created
public class Customer
{
    public string Email { get; set; }  // No index
}

// With annotations - strategic indexing
public class Customer
{
    [DmIndex(isUnique: true, indexName: "IX_Customers_Email")]
    public string Email { get; set; }  // Unique index for fast lookups
}
```

The key insight is that **annotations make your intentions explicit** to both DapperMatic and future developers. They bridge the gap between your C# domain model and the precise database schema you need in production.

## Supported Standard Annotations

DapperMatic recognizes and supports many well-known .NET annotations, making it easy to work with existing codebases and popular ORMs. This allows for gradual migration and interoperability.

### System.ComponentModel.DataAnnotations

DapperMatic understands these standard .NET validation and schema attributes:

```csharp
public class Product
{
    [Key]  // Recognized as primary key
    public int Id { get; set; }

    [Required]  // Makes column NOT NULL
    [StringLength(100)]  // Sets column length to 100
    public string Name { get; set; }

    [MaxLength(500)]  // Alternative to StringLength
    public string? Description { get; set; }

    // Both StringLength and MaxLength are supported for setting column lengths
}
```

**Supported Attributes:**
- `[Key]` - Marks property as primary key
- `[Required]` - Makes column non-nullable (overrides C# nullability)
- `[StringLength(length)]` - Sets maximum string length
- `[MaxLength(length)]` - Alternative way to set maximum length

### System.ComponentModel.DataAnnotations.Schema

Entity Framework attributes are also recognized:

```csharp
[Table("products", Schema = "catalog")]  // Table name and schema
public class Product
{
    [Column("product_id")]  // Column name mapping
    public int Id { get; set; }

    [Column("product_name")]
    public string Name { get; set; }

    [ForeignKey(nameof(Category))]  // Basic foreign key support
    public int CategoryId { get; set; }

    [NotMapped]  // Exclude from database mapping
    public string ComputedValue => $"{Name} - {Id}";

    [InverseProperty("Products")]  // Navigation property hint
    public Category Category { get; set; }
}
```

**Supported Attributes:**
- `[Table(name, Schema = "schema")]` - Specifies table name and schema
- `[Column("column_name")]` - Maps property to specific column name
- `[ForeignKey("reference")]` - Defines foreign key relationships
- `[NotMapped]` - Excludes properties from database mapping
- `[InverseProperty("property")]` - Provides navigation hints

### Microsoft.EntityFrameworkCore Attributes

Modern Entity Framework Core annotations are supported:

```csharp
public class User
{
    // EF Core Index attribute support
    [Index(IsUnique = true, Name = "IX_Users_Email")]
    public string Email { get; set; }
}
```

**Supported Attributes:**
- `[Index]` - Creates database indexes with unique and naming options

### ServiceStack.OrmLite Attributes

DapperMatic recognizes ServiceStack annotations for easy migration:

```csharp
// ServiceStack style annotations (referenced by name, not directly)
[Alias("app_users")]  // Table name mapping
[Schema("dbo")]       // Schema specification
public class User
{
    [PrimaryKey]      // Primary key designation
    [Alias("user_id")] // Column name mapping
    public int Id { get; set; }

    [Required]        // Non-nullable column
    [Alias("username")]
    public string Name { get; set; }

    [Ignore]          // Exclude from mapping
    public string TempValue { get; set; }
}
```

**Supported Attributes (by reflection):**
- `[Alias("name")]` - Maps to table or column names
- `[Schema("name")]` - Specifies database schema
- `[PrimaryKey]` - Marks primary key columns
- `[Required]` - Makes columns non-nullable
- `[Ignore]` - Excludes properties from mapping

### Mixed Framework Example

You can use multiple annotation styles together - DapperMatic will recognize them all:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("orders", Schema = "sales")]  // EF Core style
public class Order
{
    [Key]  // Standard .NET
    [DmColumn(isAutoIncrement: true)]  // DapperMatic specific
    public int Id { get; set; }

    [Required]  // Standard .NET  
    [StringLength(50)]  // Standard .NET
    [DmIndex(indexName: "IX_Orders_OrderNumber")]  // DapperMatic specific
    public string OrderNumber { get; set; }

    [Column("customer_id")]  // EF Core
    [ForeignKey(nameof(Customer))]  // EF Core
    [DmForeignKeyConstraint(typeof(Customer), new[] { "Id" })]  // DapperMatic specific
    public int CustomerId { get; set; }

    [NotMapped]  // EF Core
    public Customer Customer { get; set; }
}
```

### Why Support Multiple Annotation Styles?

**1. Migration Path**
```csharp
// Start with existing EF Core model
[Table("users")]
public class User
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; }
}

// Gradually add DapperMatic-specific features
[Table("users")]
[DmCheckConstraint("CK_Users_Name_NotEmpty", "LEN(Name) > 0")]  // Add business rules
public class User
{
    [Key]
    [DmColumn(isAutoIncrement: true)]  // More explicit control
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    [DmIndex(isUnique: true)]  // Add performance optimizations
    public string Name { get; set; }
}
```

**2. Team Flexibility**
Teams can use familiar annotations while gaining DapperMatic's DDL capabilities.

**3. Existing Codebase Integration**
Works with established Entity Framework, ServiceStack, or plain .NET models.

### Annotation Precedence

When multiple annotations specify the same information, DapperMatic follows this precedence:

1. **DapperMatic-specific attributes** (highest priority)
2. **Entity Framework attributes**
3. **Standard .NET attributes**
4. **ServiceStack attributes** (lowest priority)
5. **Convention-based mapping** (fallback)

```csharp
public class Example
{
    [Column("ef_name")]           // EF Core: "ef_name"
    [DmColumn("dm_name")]         // DapperMatic: "dm_name" (wins)
    public string Name { get; set; }  // Result: column named "dm_name"
}
```

## Table and View Annotations

### DmTableAttribute

Defines the table name and schema for a class.

```csharp
[DmTable("dbo", "Users")]
public class User
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
}

// Generate DmTable from the annotated class
DmTable table = DmTableFactory.GetTable(typeof(User));
```

**Parameters:**
- `schemaName` (optional) - The database schema name
- `tableName` (optional) - The table name (defaults to class name)

**Usage Patterns:**
```csharp
// Schema and table name specified
[DmTable("dbo", "app_users")]
public class User { }

// Only schema specified (uses class name as table)
[DmTable("dbo")]
public class Users { }

// Only table name specified (no schema)
[DmTable(tableName: "users")]
public class User { }

// Use class name for table, no schema
[DmTable]
public class Users { }
```

### DmViewAttribute

Defines a database view with its SQL definition.

```csharp
[DmView(@"
    SELECT 
        u.Id,
        u.Username,
        u.Email,
        u.CreatedAt
    FROM {0}.Users u 
    WHERE u.IsActive = 1", 
    schemaName: "dbo", 
    viewName: "ActiveUsers")]
public class ActiveUserView
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Generate DmView from the annotated class
DmView view = DmViewFactory.GetView(typeof(ActiveUserView));
```

**Parameters:**
- `definition` (required) - SQL definition for the view (use `{0}` for schema placeholder)
- `schemaName` (optional) - The database schema name
- `viewName` (optional) - The view name (defaults to class name)

## Column Annotations

### DmColumnAttribute

The most comprehensive attribute for defining column properties.

```csharp
public class Product
{
    [DmColumn("product_id", isAutoIncrement: true, isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("product_name", length: 100, isNullable: false)]
    public string Name { get; set; }

    [DmColumn("description", length: 500, isNullable: true)]
    public string? Description { get; set; }

    [DmColumn("price", precision: 10, scale: 2, isNullable: false)]
    public decimal Price { get; set; }

    [DmColumn("category_id", isForeignKey: true, 
              referencedTableName: "Categories", 
              referencedColumnName: "Id")]
    public int CategoryId { get; set; }

    [DmColumn("created_at", defaultExpression: "GETDATE()", isNullable: false)]
    public DateTime CreatedAt { get; set; }

    [DmColumn("is_active", defaultExpression: "1", isNullable: false)]
    public bool IsActive { get; set; }
}
```

**Key Parameters:**
- `columnName` - Database column name
- `providerDataType` - Specific database type (e.g., "nvarchar(100)")
- `length` - Maximum length for strings/binary data
- `precision` - Total digits for numeric types
- `scale` - Decimal places for numeric types
- `isNullable` - Whether column allows NULL
- `isPrimaryKey` - Whether column is part of primary key
- `isAutoIncrement` - Whether column auto-increments
- `isUnique` - Whether column has unique constraint
- `isIndexed` - Whether to create an index
- `checkExpression` - Check constraint expression
- `defaultExpression` - Default value expression
- `isForeignKey` - Whether column is a foreign key
- `referencedTableName` - Referenced table for foreign keys
- `referencedColumnName` - Referenced column for foreign keys
- `onDelete` / `onUpdate` - Foreign key actions

### Provider-Specific Data Types

Use the `providerDataType` parameter to specify database-specific types:

```csharp
public class Document
{
    // Different types for different providers
    [DmColumn("content", 
              providerDataType: "{sqlserver:nvarchar(max),mysql:longtext,postgresql:text,sqlite:text}")]
    public string Content { get; set; }

    // JSON column support
    [DmColumn("metadata", 
              providerDataType: "{sqlserver:nvarchar(max),mysql:json,postgresql:jsonb,sqlite:text}")]
    public string Metadata { get; set; }

    // UUID/GUID handling
    [DmColumn("document_id", 
              providerDataType: "{sqlserver:uniqueidentifier,mysql:char(36),postgresql:uuid,sqlite:text}")]
    public Guid DocumentId { get; set; }
}
```

### DmIgnoreAttribute

Excludes properties from database mapping.

```csharp
public class User
{
    public int Id { get; set; }
    
    [DmColumn("username")]
    public string Username { get; set; }
    
    // This property won't be included in the database table
    [DmIgnore]
    public string FullName => $"{FirstName} {LastName}";
    
    [DmIgnore]
    public List<Order> Orders { get; set; } = new();
}
```

## Constraint Annotations

### DmPrimaryKeyConstraintAttribute

Defines primary key constraints at the class or property level.

```csharp
// Single column primary key on property
public class User
{
    [DmPrimaryKeyConstraint(constraintName: "PK_Users")]
    public int Id { get; set; }
}

// Composite primary key on class
[DmPrimaryKeyConstraint(new[] { "UserId", "RoleId" }, "PK_UserRoles")]
public class UserRole
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
}
```

### DmForeignKeyConstraintAttribute

Defines foreign key relationships.

```csharp
// Foreign key on property with type reference
public class Order
{
    [DmForeignKeyConstraint(
        referencedType: typeof(User),
        referencedColumnNames: new[] { "Id" },
        constraintName: "FK_Orders_Users",
        onDelete: DmForeignKeyAction.Cascade)]
    public int UserId { get; set; }
}

// Foreign key on class with explicit table name
[DmForeignKeyConstraint(
    new[] { "CategoryId" }, 
    referencedTableName: "Categories",
    referencedColumnNames: new[] { "Id" },
    constraintName: "FK_Products_Categories")]
public class Product
{
    public int CategoryId { get; set; }
}
```

**Foreign Key Actions:**
- `NoAction` - No action (default)
- `Restrict` - Prevent the action
- `Cascade` - Cascade the action
- `SetNull` - Set to NULL
- `SetDefault` - Set to default value

### DmUniqueConstraintAttribute

Creates unique constraints on single or multiple columns.

```csharp
// Single column unique constraint
public class User
{
    [DmUniqueConstraint(constraintName: "UQ_Users_Email")]
    public string Email { get; set; }
}

// Multi-column unique constraint on class
[DmUniqueConstraint(new[] { "FirstName", "LastName", "DateOfBirth" }, "UQ_Users_Natural")]
public class User
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime DateOfBirth { get; set; }
}
```

### DmCheckConstraintAttribute

Defines business rule constraints.

```csharp
public class Product
{
    [DmCheckConstraint("CK_Products_Price_Positive", "Price > 0")]
    public decimal Price { get; set; }
}

// Class-level check constraint
[DmCheckConstraint("CK_Users_Age_Valid", "Age >= 0 AND Age <= 150")]
public class User
{
    public int Age { get; set; }
}
```

### DmDefaultConstraintAttribute

Provides default values for columns.

```csharp
public class User
{
    [DmDefaultConstraint("DF_Users_CreatedAt", "GETDATE()")]
    public DateTime CreatedAt { get; set; }

    [DmDefaultConstraint("DF_Users_IsActive", "1")]
    public bool IsActive { get; set; }

    [DmDefaultConstraint("DF_Users_Id", "NEWID()")]
    public Guid Id { get; set; }
}
```

## Index Annotations

### DmIndexAttribute

Creates database indexes for performance optimization.

```csharp
public class User
{
    // Simple index on property
    [DmIndex(indexName: "IX_Users_Email")]
    public string Email { get; set; }

    // Unique index on property
    [DmIndex(isUnique: true, indexName: "IX_Users_Username")]
    public string Username { get; set; }
}

// Composite index on class
[DmIndex(columnNames: new[] { "LastName", "FirstName" }, indexName: "IX_Users_Name")]
[DmIndex(isUnique: true, columnNames: new[] { "Email" }, indexName: "IX_Users_Email_Unique")]
public class User
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
}
```

## Complete Example

Here's a comprehensive example showing multiple annotations on a single class:

```csharp
[DmTable("dbo", "Orders")]
[DmIndex(columnNames: new[] { "UserId", "OrderDate" }, indexName: "IX_Orders_User_Date")]
[DmCheckConstraint("CK_Orders_TotalAmount_Positive", "TotalAmount > 0")]
[DmForeignKeyConstraint(
    new[] { "UserId" }, 
    typeof(User), 
    new[] { "Id" },
    "FK_Orders_Users",
    DmForeignKeyAction.Restrict)]
public class Order
{
    [DmColumn("order_id", isAutoIncrement: true, isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("user_id", isNullable: false, isIndexed: true)]
    public int UserId { get; set; }

    [DmColumn("order_number", length: 50, isNullable: false)]
    [DmUniqueConstraint(constraintName: "UQ_Orders_OrderNumber")]
    public string OrderNumber { get; set; }

    [DmColumn("order_date", isNullable: false, isIndexed: true)]
    [DmDefaultConstraint("DF_Orders_OrderDate", "GETDATE()")]
    public DateTime OrderDate { get; set; }

    [DmColumn("total_amount", precision: 10, scale: 2, isNullable: false)]
    public decimal TotalAmount { get; set; }

    [DmColumn("status", length: 20, isNullable: false)]
    [DmDefaultConstraint("DF_Orders_Status", "'Pending'")]
    [DmCheckConstraint("CK_Orders_Status_Valid", 
                       "Status IN ('Pending', 'Processing', 'Shipped', 'Delivered', 'Cancelled')")]
    public string Status { get; set; }

    [DmColumn("notes", length: 1000, isNullable: true)]
    public string? Notes { get; set; }

    // Navigation properties ignored
    [DmIgnore]
    public User User { get; set; }

    [DmIgnore]
    public List<OrderItem> Items { get; set; } = new();
}

// Usage
DmTable orderTable = DmTableFactory.GetTable(typeof(Order));
await connection.CreateTableIfNotExistsAsync("dbo", orderTable);
```

## Best Practices

1. **Use meaningful constraint names** - Include table name and purpose
2. **Specify nullability explicitly** - Don't rely on defaults
3. **Group related annotations** - Put class-level constraints together
4. **Use type references** - Prefer `typeof(User)` over string table names for foreign keys
5. **Document complex expressions** - Add comments for complex check constraints
6. **Consider provider differences** - Use provider-specific types when needed
7. **Validate at compile time** - Attributes provide compile-time validation
8. **Use consistent naming** - Follow consistent patterns for constraint names

## Migration from Models

You can gradually migrate from manual `DmTable` creation to data annotations:

```csharp
// Before: Manual model creation
var table = new DmTable("Users")
{
    Columns = new[] { /* ... */ },
    PrimaryKey = new DmPrimaryKeyConstraint("PK_Users", "Id")
};

// After: Data annotations + factory
[DmTable("dbo", "Users")]
public class User
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }
}

DmTable table = DmTableFactory.GetTable(typeof(User));
```

Data annotations provide a powerful, type-safe way to define your database schema directly in your C# code, making your applications more maintainable and reducing the gap between your domain models and database structure.