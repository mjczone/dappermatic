# Models

DapperMatic uses a model-first approach with `Dm*` prefixed classes to define database schema objects. These models provide a strongly-typed way to create and manipulate database structures.

- [DmCheckConstraint](#dmcheckconstraint) ([src](https://github.com/mjczone/dappermatic/blob/main/src/MJCZone.DapperMatic/Models/DmCheckConstraint.cs))
- [DmColumn](#dmcolumn) ([src](https://github.com/mjczone/dappermatic/blob/main/src/MJCZone.DapperMatic/Models/DmColumn.cs))
- [DmColumnOrder](#dmcolumnorder) ([src](https://github.com/mjczone/dappermatic/blob/main/src/MJCZone.DapperMatic/Models/DmColumnOrder.cs))
- [DmConstraint](#dmconstraint) ([src](https://github.com/mjczone/dappermatic/blob/main/src/MJCZone.DapperMatic/Models/DmConstraint.cs))
- [DmConstraintType](#dmconstrainttype) ([src](https://github.com/mjczone/dappermatic/blob/main/src/MJCZone.DapperMatic/Models/DmConstraintType.cs))
- [DmDefaultConstraint](#dmdefaultconstraint) ([src](https://github.com/mjczone/dappermatic/blob/main/src/MJCZone.DapperMatic/Models/DmDefaultConstraint.cs))
- [DmForeignKeyAction](#dmforeignkeyaction) ([src](https://github.com/mjczone/dappermatic/blob/main/src/MJCZone.DapperMatic/Models/DmForeignKeyAction.cs))
- [DmForeignKeyConstraint](#dmforeignkeyconstraint) ([src](https://github.com/mjczone/dappermatic/blob/main/src/MJCZone.DapperMatic/Models/DmForeignKeyConstraint.cs))
- [DmIndex](#dmindex) ([src](https://github.com/mjczone/dappermatic/blob/main/src/MJCZone.DapperMatic/Models/DmIndex.cs))
- [DmOrderedColumn](#dmorderedcolumn) ([src](https://github.com/mjczone/dappermatic/blob/main/src/MJCZone.DapperMatic/Models/DmOrderedColumn.cs))
- [DmPrimaryKeyConstraint](#dmprimarykeyconstraint) ([src](https://github.com/mjczone/dappermatic/blob/main/src/MJCZone.DapperMatic/Models/DmPrimaryKeyConstraint.cs))
- [DmTable](#dmtable) ([src](https://github.com/mjczone/dappermatic/blob/main/src/MJCZone.DapperMatic/Models/DmTable.cs))
- [DmUniqueConstraint](#dmuniqueconstraint) ([src](https://github.com/mjczone/dappermatic/blob/main/src/MJCZone.DapperMatic/Models/DmUniqueConstraint.cs))
- [DmView](#dmview) ([src](https://github.com/mjczone/dappermatic/blob/main/src/MJCZone.DapperMatic/Models/DmView.cs))

## Core Models

### DmTable

The `DmTable` class represents a database table with all its components.

📝 **Test Examples:** [DatabaseMethodsTests.Tables.cs](https://github.com/mjczone/dappermatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.Tables.cs)

```csharp
var table = new DmTable(null /* or schemaName */, "Users")
{
    TableName = "Users",
    Columns = new[]
    {
        new DmColumn("Id", typeof(int)) { IsNullable = false, IsAutoIncrement = true },
        new DmColumn("Username", typeof(string)) { MaxLength = 50, IsNullable = false },
        new DmColumn("Email", typeof(string)) { MaxLength = 100, IsNullable = false },
        new DmColumn("IsActive", typeof(bool)) { IsNullable = false, DefaultValue = "1" },
        new DmColumn("CreatedAt", typeof(DateTime)) { IsNullable = false }
    },
    PrimaryKeyConstraint = new DmPrimaryKeyConstraint("PK_Users", "Id"),
    Indexes = new[]
    {
        new DmIndex("IX_Users_Username", new[] { "Username" }) { IsUnique = true },
        new DmIndex("IX_Users_Email", new[] { "Email" }) { IsUnique = true }
    },
    CheckConstraints = new[]
    {
        new DmCheckConstraint("CK_Users_Username_Length", "LEN(Username) > 0")
    },
    DefaultConstraints = new[]
    {
        new DmDefaultConstraint("DF_Users_CreatedAt", "CreatedAt", "GETDATE()")
    }
};
```

**Properties:**

- `TableName` - Name of the table
- `Columns` - Array of column definitions
- `PrimaryKey` - Primary key constraint (optional)
- `ForeignKeys` - Foreign key constraints
- `CheckConstraints` - Check constraints
- `DefaultConstraints` - Default value constraints
- `UniqueConstraints` - Unique constraints
- `Indexes` - Table indexes

### DmColumn

Defines a table column with its data type and properties.

📝 **Test Examples:** [DatabaseMethodsTests.Columns.cs](https://github.com/mjczone/dappermatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.Columns.cs)

```csharp
// Auto-increment primary key
var idColumn = new DmColumn("Id", typeof(int))
{
    IsNullable = false,
    IsAutoIncrement = true
};

// String column with length
var nameColumn = new DmColumn("Name", typeof(string))
{
    Length = 100,
    IsNullable = false
};

// Decimal column with precision/scale
var priceColumn = new DmColumn("Price", typeof(decimal))
{
    Precision = 10,
    Scale = 2,
    IsNullable = true
};

// Column with static default value
var statusColumn = new DmColumn("Status", typeof(string))
{
    Length = 20,
    IsNullable = false,
    DefaultExpression = "'Active'"
};

// Column with provider-specific default expression
var isActiveColumn = new DmColumn("IsActive", typeof(bool))
{
    IsNullable = false,
    DefaultExpressionFunc = CommonProviderDefaultExpressions.TrueValue
};

// Column with provider-specific check constraint
var usernameColumn = new DmColumn("Username", typeof(string))
{
    Length = 50,
    IsNullable = false,
    CheckExpressionFunc = CommonProviderCheckExpressions.StringLengthGtCheck("Username", 0)
};
```

**Key Properties:**

**Basic Properties:**
- `SchemaName` - Schema name of the column (optional)
- `TableName` - Table name containing this column
- `ColumnName` - Name of the column
- `DotnetType` - .NET type (automatically mapped to SQL type)
- `ProviderDataTypes` - Dictionary of provider-specific SQL type names

**Size and Precision:**
- `Length` - Maximum length for string/binary types (replaces deprecated MaxLength)
- `Precision` - Total digits for numeric types
- `Scale` - Decimal places for numeric types

**Column Characteristics:**
- `IsNullable` - Whether the column allows NULL values
- `IsPrimaryKey` - Whether the column is a primary key
- `IsAutoIncrement` - Whether the column auto-increments
- `IsUnique` - Whether the column has a unique constraint
- `IsUnicode` - Whether the column explicitly supports unicode characters
- `IsFixedLength` - Whether the column has fixed length (e.g., CHAR vs VARCHAR)
- `IsIndexed` - Whether the column is indexed

**Foreign Key Properties:**
- `IsForeignKey` - Whether the column is a foreign key
- `ReferencedTableName` - Referenced table name (for foreign keys)
- `ReferencedColumnName` - Referenced column name (for foreign keys)
- `OnDelete` - Action on delete (for foreign keys)
- `OnUpdate` - Action on update (for foreign keys)

**Default and Check Expressions:**
- `DefaultExpression` - Static default value expression
- `DefaultExpressionFunc` - Provider-specific default expression function
- `CheckExpression` - Static check constraint expression
- `CheckExpressionFunc` - Provider-specific check constraint function

**Helper Methods:**
- `GetDefaultExpression(DbProviderType)` - Gets appropriate default expression for provider
- `GetCheckExpression(DbProviderType)` - Gets appropriate check expression for provider
- `IsNumeric()` - Returns true if column is numeric type
- `IsText()` - Returns true if column is text type
- `IsDateTime()` - Returns true if column is date/time type
- `IsBoolean()` - Returns true if column is boolean type

### DmColumnOrder

An enumeration that specifies the sort order of a column in indexes and constraints.

```csharp
// Ascending order (default)
var ascendingColumn = new DmOrderedColumn("CreatedAt", DmColumnOrder.Ascending);

// Descending order
var descendingColumn = new DmOrderedColumn("LastUpdated", DmColumnOrder.Descending);

// Use in composite index
var compositeIndex = new DmIndex("IX_Products_Category_Price", new[]
{
    new DmOrderedColumn("CategoryId", DmColumnOrder.Ascending),
    new DmOrderedColumn("Price", DmColumnOrder.Descending)
});
```

**Available Values:**

- `Ascending` - Sorts the column in ascending order (A-Z, 0-9, earliest to latest)
- `Descending` - Sorts the column in descending order (Z-A, 9-0, latest to earliest)

### DmView

Represents a database view with its definition.

📝 **Test Examples:** [DatabaseMethodsTests.Views.cs](https://github.com/mjczone/dappermatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.Views.cs)

```csharp
var view = new DmView("ActiveUsers")
{
    ViewName = "ActiveUsers",
    Definition = @"
        SELECT
            Id,
            Username,
            Email,
            CreatedAt
        FROM Users
        WHERE IsActive = 1"
};
```

**Properties:**

- `ViewName` - Name of the view
- `Definition` - SQL definition of the view

## Constraint Models

### DmConstraintType

An enumeration that categorizes different types of database constraints.

```csharp
// Used internally by DapperMatic to identify constraint types
// when introspecting database schemas

// Example: Checking constraint type during schema inspection
public void ProcessConstraint(string constraintName, DmConstraintType constraintType)
{
    switch (constraintType)
    {
        case DmConstraintType.PrimaryKey:
            Console.WriteLine($"{constraintName} is a primary key constraint");
            break;
        case DmConstraintType.ForeignKey:
            Console.WriteLine($"{constraintName} is a foreign key constraint");
            break;
        case DmConstraintType.Unique:
            Console.WriteLine($"{constraintName} is a unique constraint");
            break;
        case DmConstraintType.Check:
            Console.WriteLine($"{constraintName} is a check constraint");
            break;
        case DmConstraintType.Default:
            Console.WriteLine($"{constraintName} is a default constraint");
            break;
    }
}
```

**Available Values:**

- `PrimaryKey` - Identifies primary key constraints
- `ForeignKey` - Identifies foreign key constraints  
- `Unique` - Identifies unique constraints
- `Check` - Identifies check constraints
- `Default` - Identifies default value constraints

### DmPrimaryKeyConstraint

Defines a primary key constraint on one or more columns.

📝 **Test Examples:** [DatabaseMethodsTests.PrimaryKeyConstraints.cs](https://github.com/mjczone/dappermatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.PrimaryKeyConstraints.cs)

```csharp
// Single column primary key
var singlePK = new DmPrimaryKeyConstraint("PK_Users", "Id");

// Composite primary key
var compositePK = new DmPrimaryKeyConstraint("PK_OrderItems", new[] { "OrderId", "ProductId" });
```

### DmForeignKeyConstraint

Defines relationships between tables.

📝 **Test Examples:** [DatabaseMethodsTests.ForeignKeyConstraints.cs](https://github.com/mjczone/dappermatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.ForeignKeyConstraints.cs)

```csharp
// Basic foreign key
var basicFK = new DmForeignKeyConstraint(
    constraintName: "FK_Orders_Users",
    columnNames: new[] { "UserId" },
    referencedTableName: "Users",
    referencedColumnNames: new[] { "Id" }
);

// Foreign key with cascade actions
var cascadeFK = new DmForeignKeyConstraint(
    constraintName: "FK_OrderItems_Orders",
    columnNames: new[] { "OrderId" },
    referencedTableName: "Orders",
    referencedColumnNames: new[] { "Id" }
)
{
    OnDelete = DmForeignKeyAction.Cascade,
    OnUpdate = DmForeignKeyAction.Restrict
};
```

### DmForeignKeyAction

An enumeration that specifies the action to take when a referenced key is updated or deleted.

```csharp
// Create foreign key with cascade delete
var orderItemsFK = new DmForeignKeyConstraint(
    "FK_OrderItems_Orders",
    new[] { "OrderId" },
    "Orders",
    new[] { "Id" }
)
{
    OnDelete = DmForeignKeyAction.Cascade,    // Delete child records
    OnUpdate = DmForeignKeyAction.Restrict    // Prevent parent updates
};

// Create foreign key that sets NULL on delete
var customerOrdersFK = new DmForeignKeyConstraint(
    "FK_Orders_Customers",
    new[] { "CustomerId" },
    "Customers", 
    new[] { "Id" }
)
{
    OnDelete = DmForeignKeyAction.SetNull,    // Set CustomerId to NULL
    OnUpdate = DmForeignKeyAction.NoAction    // Allow parent updates
};

// Parse from string representation
var cascadeAction = DmForeignKeyActionExtensions.Parse("CASCADE");        // DmForeignKeyAction.Cascade
var noActionAction = DmForeignKeyActionExtensions.Parse("NO ACTION");     // DmForeignKeyAction.NoAction
var restrictAction = DmForeignKeyActionExtensions.Parse("RESTRICT");      // DmForeignKeyAction.Restrict

// Extension method usage
string sqlAction = DmForeignKeyAction.Cascade.ToSql(); // Returns "CASCADE"
var actionFromString = "RESTRICT".ToForeignKeyAction(); // Returns DmForeignKeyAction.Restrict
```

**Available Values:**

- `NoAction` - No action taken (default behavior)
- `Restrict` - Reject the delete or update operation for the parent table
- `Cascade` - Automatically delete or update matching rows in the child table
- `SetNull` - Set the foreign key columns in the child table to NULL

### DmUniqueConstraint

Ensures uniqueness across one or more columns.

📝 **Test Examples:** [DatabaseMethodsTests.UniqueConstraints.cs](https://github.com/mjczone/dappermatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.UniqueConstraints.cs)

```csharp
// Single column unique constraint
var emailUnique = new DmUniqueConstraint("UQ_Users_Email", new[] { "Email" });

// Multi-column unique constraint
var compositeUnique = new DmUniqueConstraint(
    "UQ_UserProfiles_UserId_ProfileType",
    new[] { "UserId", "ProfileType" }
);
```

### DmCheckConstraint

Defines business rules at the database level.

📝 **Test Examples:** [DatabaseMethodsTests.CheckConstraints.cs](https://github.com/mjczone/dappermatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.CheckConstraints.cs)

```csharp
// Simple value check
var ageCheck = new DmCheckConstraint("CK_Users_Age", "Age >= 0 AND Age <= 150");

// Complex business rule
var emailCheck = new DmCheckConstraint(
    "CK_Users_Email_Format",
    "Email LIKE '%@%.%' AND LEN(Email) > 5"
);

// Date range check
var dateCheck = new DmCheckConstraint(
    "CK_Orders_ValidDateRange",
    "OrderDate >= '2020-01-01' AND OrderDate <= GETDATE()"
);
```

### DmDefaultConstraint

Provides default values for columns.

📝 **Test Examples:** [DatabaseMethodsTests.DefaultConstraints.cs](https://github.com/mjczone/dappermatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.DefaultConstraints.cs)

```csharp
// Current timestamp default
var createdAtDefault = new DmDefaultConstraint(
    "DF_Users_CreatedAt",
    "CreatedAt",
    "GETDATE()"
);

// GUID default
var idDefault = new DmDefaultConstraint(
    "DF_Sessions_Id",
    "Id",
    "NEWID()"
);

// Constant value default
var statusDefault = new DmDefaultConstraint(
    "DF_Users_Status",
    "Status",
    "'Pending'"
);
```

## Index Models

### DmIndex

Defines database indexes for performance optimization.

📝 **Test Examples:** [DatabaseMethodsTests.Indexes.cs](https://github.com/mjczone/dappermatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.Indexes.cs)

```csharp
// Simple index
var nameIndex = new DmIndex("IX_Users_LastName", new[] { "LastName" });

// Unique index
var emailIndex = new DmIndex("IX_Users_Email", new[] { "Email" })
{
    IsUnique = true
};

// Composite index with column ordering
var compositeIndex = new DmIndex("IX_Orders_Date_Status", new[]
{
    new DmOrderedColumn("OrderDate", DmColumnOrder.Descending),
    new DmOrderedColumn("Status", DmColumnOrder.Ascending)
});

// Filtered index (SQL Server)
var filteredIndex = new DmIndex("IX_Users_Active", new[] { "Username" })
{
    IsUnique = true,
    Filter = "IsActive = 1"  // Provider-specific feature
};
```

**Properties:**

- `IndexName` - Name of the index
- `Columns` - Columns included in the index
- `IsUnique` - Whether the index enforces uniqueness
- `Filter` - Filter expression (provider-specific)

### DmOrderedColumn

Represents a column in an ordered list with a specified sort direction, commonly used in indexes and constraints.

```csharp
// Create ordered columns for an index
var ascendingColumn = new DmOrderedColumn("CategoryId", DmColumnOrder.Ascending);
var descendingColumn = new DmOrderedColumn("Price", DmColumnOrder.Descending);

// Use in composite index definition
var productIndex = new DmIndex("IX_Products_Category_Price", new[]
{
    new DmOrderedColumn("CategoryId", DmColumnOrder.Ascending),
    new DmOrderedColumn("Price", DmColumnOrder.Descending),
    new DmOrderedColumn("Name", DmColumnOrder.Ascending)  // Default is Ascending
});

// Create with default ascending order
var simpleColumn = new DmOrderedColumn("CreatedAt"); // Defaults to Ascending

// Parse from string representation
var parsedAscending = DmOrderedColumn.Parse("CategoryId");        // CategoryId ASC
var parsedDescending = DmOrderedColumn.Parse("Price DESC");       // Price DESC
var parsedCaseSensitive = DmOrderedColumn.Parse("price desc");    // price DESC

// String representation
Console.WriteLine(ascendingColumn.ToString());  // "CategoryId"
Console.WriteLine(descendingColumn.ToString()); // "Price DESC"
Console.WriteLine(descendingColumn.ToString(false)); // "Price" (excludes order)
```

**Properties:**

- `ColumnName` - Name of the column (required)
- `Order` - Sort order direction (required, defaults to Ascending in constructor)

**Methods:**

- `ToString()` - Returns column name with DESC suffix for descending columns
- `ToString(bool includeOrder)` - Controls whether to include order direction in output

## Factory Methods

### DmTableFactory

Generate table models from .NET classes using attributes.

📝 **Test Examples:** [DatabaseMethodsTests.TableFactory.cs](https://github.com/mjczone/dappermatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.TableFactory.cs)

```csharp
// Define a class with attributes
[Table("app_employees")]
public class Employee
{
    [Key]
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; }

    [MaxLength(200)]
    public string Email { get; set; }

    public DateTime HireDate { get; set; }
}

// Generate DmTable from class
DmTable table = DmTableFactory.GetTable(typeof(Employee));
```

### DmViewFactory

Generate view models from .NET classes.

📝 **Test Examples:** [DatabaseMethodsTests.Views.cs](https://github.com/mjczone/dappermatic/blob/main/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.Views.cs)

```csharp
[View("vw_active_employees")]
public class ActiveEmployeeView
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public DateTime HireDate { get; set; }
}

// Generate DmView from class
DmView view = DmViewFactory.GetView(typeof(ActiveEmployeeView));
```

## Complete Example

Here's a comprehensive example showing how to create a complete table with all types of constraints:

```csharp
var ordersTable = new DmTable(null /* or schemaName */, "Orders")
{
    Columns = new[]
    {
        new DmColumn("Id", typeof(int)) { IsNullable = false, IsAutoIncrement = true },
        new DmColumn("UserId", typeof(int)) { IsNullable = false },
        new DmColumn("OrderNumber", typeof(string)) { MaxLength = 50, IsNullable = false },
        new DmColumn("OrderDate", typeof(DateTime)) { IsNullable = false },
        new DmColumn("TotalAmount", typeof(decimal)) { Precision = 10, Scale = 2, IsNullable = false },
        new DmColumn("Status", typeof(string)) { MaxLength = 20, IsNullable = false },
        new DmColumn("CreatedAt", typeof(DateTime)) { IsNullable = false },
        new DmColumn("UpdatedAt", typeof(DateTime)) { IsNullable = true }
    },
    PrimaryKeyConstraint = new DmPrimaryKeyConstraint("PK_Orders", "Id"),
    ForeignKeyConstraints = new[]
    {
        new DmForeignKeyConstraint("FK_Orders_Users", new[] { "UserId" }, "Users", new[] { "Id" })
        {
            OnDelete = DmForeignKeyAction.Restrict
        }
    },
    UniqueConstraints = new[]
    {
        new DmUniqueConstraint("UQ_Orders_OrderNumber", new[] { "OrderNumber" })
    },
    CheckConstraints = new[]
    {
        new DmCheckConstraint("CK_Orders_TotalAmount", "TotalAmount >= 0"),
        new DmCheckConstraint("CK_Orders_Status", "Status IN ('Pending', 'Processing', 'Shipped', 'Delivered', 'Cancelled')")
    },
    DefaultConstraints = new[]
    {
        new DmDefaultConstraint("DF_Orders_Status", "Status", "'Pending'"),
        new DmDefaultConstraint("DF_Orders_CreatedAt", "CreatedAt", "GETDATE()")
    },
    Indexes = new[]
    {
        new DmIndex("IX_Orders_UserId", new[] { "UserId" }),
        new DmIndex("IX_Orders_OrderDate", new[] { "OrderDate" }),
        new DmIndex("IX_Orders_Status_Date", new[]
        {
            new DmOrderedColumn("Status", DmColumnOrder.Ascending),
            new DmOrderedColumn("OrderDate", DmColumnOrder.Descending)
        })
    }
};

// Create the table
await connection.CreateTableIfNotExistsAsync(ordersTable);
```

## Best Practices

1. **Use meaningful names** for constraints and indexes
2. **Always specify nullability** explicitly
3. **Set appropriate string lengths** to avoid truncation
4. **Use check constraints** for business rules
5. **Create indexes** on foreign key columns
6. **Consider composite indexes** for common query patterns
7. **Use factory methods** when working with existing classes
