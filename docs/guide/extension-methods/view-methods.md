# View Methods

View methods provide comprehensive functionality for managing database views across all supported providers. Views are virtual tables created by queries that can simplify complex data access patterns and provide security abstraction.

## Quick Navigation

- [View Existence Checking](#view-existence-checking)
  - [DoesViewExistAsync](#doesviewexistasync) - Check if view exists
- [View Creation](#view-creation)
  - [CreateViewIfNotExistsAsync (Model)](#createviewifnotexistsasync-dmview) - Create from DmView model
  - [CreateViewIfNotExistsAsync (Parameters)](#createviewifnotexistsasync-parameters) - Create with individual parameters
- [View Modification](#view-modification)
  - [UpdateViewIfExistsAsync](#updateviewifexistsasync) - Update existing view definition
- [View Discovery](#view-discovery)
  - [GetViewNamesAsync](#getviewnamesasync-names-only) - Get list of view names with filtering
  - [GetViewsAsync](#getviewsasync-full-models) - Get complete view models with definitions
  - [GetViewAsync](#getviewasync) - Get single view model by name
- [View Deletion](#view-deletion)
  - [DropViewIfExistsAsync](#dropviewifexistsasync) - Drop view permanently

## View Existence Checking

### DoesViewExistAsync

Check if a view exists in the database.

```csharp
// Check if view exists
bool exists = await connection.DoesViewExistAsync("app", "vw_active_employees");

if (exists)
{
    Console.WriteLine("View 'vw_active_employees' already exists");
}
else
{
    Console.WriteLine("View 'vw_active_employees' does not exist");
    await connection.CreateViewIfNotExistsAsync(view);
}


// With transaction and cancellation
using var transaction = connection.BeginTransaction();
bool exists = await connection.DoesViewExistAsync(
    "app",
    "vw_active_employees",
    tx: transaction,
    cancellationToken: cancellationToken
);
```

**Parameters:**

- `schemaName` - Schema containing the view
- `viewName` - Name of the view to check
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if view exists, `false` otherwise

## View Creation

### CreateViewIfNotExistsAsync (DmView)

Create a view only if it doesn't already exist using a DmView model.

```csharp
// Create view if it doesn't exist
bool created = await connection.CreateViewIfNotExistsAsync(view);

if (created)
{
    Console.WriteLine("View 'vw_active_employees' was created");
}
else
{
    Console.WriteLine("View 'vw_active_employees' already existed");
}
```

**Parameters:**

- `view` - DmView model defining the view structure (includes SchemaName and ViewName)
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if view was created, `false` if it already existed

### CreateViewIfNotExistsAsync (Parameters)

Create a view using individual parameters for convenience.

```csharp
// Simple view creation
bool created = await connection.CreateViewIfNotExistsAsync(
    schemaName: "app",
    viewName: "vw_employees_not_yet_onboarded",
    viewDefinition: "SELECT * FROM app.employees WHERE is_onboarded = 0"
);

// Complex view with detailed query
bool created = await connection.CreateViewIfNotExistsAsync(
    schemaName: "reporting",
    viewName: "vw_monthly_sales_summary",
    viewDefinition: @"
        SELECT
            YEAR(order_date) as order_year,
            MONTH(order_date) as order_month,
            COUNT(*) as total_orders,
            SUM(order_total) as total_revenue,
            AVG(order_total) as average_order_value,
            COUNT(DISTINCT customer_id) as unique_customers
        FROM app.orders
        WHERE order_status = 'completed'
        GROUP BY YEAR(order_date), MONTH(order_date)",
    tx: transaction,
    : 60,
    cancellationToken: cancellationToken
);

// View with joins and filtering
bool created = await connection.CreateViewIfNotExistsAsync(
    "hr",
    "vw_employee_details",
    @"SELECT
        e.employee_id,
        e.first_name + ' ' + e.last_name as full_name,
        e.email,
        e.hire_date,
        d.department_name,
        m.first_name + ' ' + m.last_name as manager_name,
        e.salary
    FROM hr.employees e
    LEFT JOIN hr.departments d ON e.department_id = d.department_id
    LEFT JOIN hr.employees m ON e.manager_id = m.employee_id
    WHERE e.is_active = 1"
);
```

**Parameters:**

- `schemaName` - Schema to create the view in
- `viewName` - Name of the view to create
- `viewDefinition` - SQL query defining the view
- `tx` (optional) - Database transaction
- `` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if view was created, `false` if it already existed

## View Modification

### UpdateViewIfExistsAsync

Update the definition of an existing view if it exists.

```csharp
// Update existing view definition using view name and new definition
bool updated = await connection.UpdateViewIfExistsAsync(
    "app", 
    "vw_active_employees", 
    @"SELECT
        e.employee_id,
        e.first_name,
        e.last_name,
        e.email,
        e.hire_date,
        e.termination_date, -- Added termination date
        d.department_name,
        d.department_code    -- Added department code
    FROM app.employees e
    INNER JOIN app.departments d ON e.department_id = d.department_id
    WHERE e.is_active = 1
      AND e.termination_date IS NULL"
);

if (updated)
{
    Console.WriteLine("View was updated");
}
else
{
    Console.WriteLine("View did not exist");
}

// With transaction and timeout
using var transaction = connection.BeginTransaction();
bool updated = await connection.UpdateViewIfExistsAsync(
    "app",
    "vw_active_employees",
    newViewDefinition,
    tx: transaction,
    : 60,
    cancellationToken: cancellationToken
);
```

**Parameters:**

- `schemaName` - Schema containing the view
- `viewName` - Name of the view to update
- `viewDefinition` - New SQL query defining the view
- `tx` (optional) - Database transaction
- `` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if view was updated, `false` if it didn't exist

## View Discovery

### GetViewNamesAsync (Names Only)

Retrieve a list of view names, with optional filtering.

```csharp
// Get all view names in schema
List<string> allViews = await connection.GetViewNamesAsync("app");
foreach (string viewName in allViews)
{
    Console.WriteLine($"Found view: {viewName}");
}

// Get view names with wildcard filter
List<string> reportViews = await connection.GetViewNamesAsync("reporting", "vw_*");
// Finds: vw_sales_summary, vw_inventory_report, etc.

// Get views with pattern matching
List<string> employeeViews = await connection.GetViewNamesAsync("hr", "*employee*");
// Finds: vw_employee_details, vw_active_employees, etc.
```

**Parameters:**

- `schemaName` - Schema to search for views
- `nameFilter` (optional) - Wildcard pattern to filter view names (`*` = any characters, `?` = single character)
- `tx` (optional) - Database transaction
- `` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `List<string>` - List of matching view names

### GetViewsAsync (Full Models)

Retrieve complete DmView models for existing views.

```csharp
// Get all views with full structure information
List<DmView> views = await connection.GetViewsAsync("app");

foreach (var view in views)
{
    Console.WriteLine($"View: {view.ViewName}");
    Console.WriteLine($"  Schema: {view.SchemaName}");
    Console.WriteLine($"  Definition Length: {view.ViewDefinition?.Length ?? 0} characters");

    // Show first 200 characters of definition
    if (!string.IsNullOrEmpty(view.ViewDefinition))
    {
        var preview = view.ViewDefinition.Length > 200
            ? view.ViewDefinition.Substring(0, 200) + "..."
            : view.ViewDefinition;
        Console.WriteLine($"  Definition: {preview}");
    }
}

// Get specific views with pattern
List<DmView> reportingViews = await connection.GetViewsAsync("reporting", "vw_sales*");

// With transaction
using var transaction = connection.BeginTransaction();
List<DmView> views = await connection.GetViewsAsync("app", tx: transaction);
```

**Parameters:**

- `schemaName` - Schema to search for views
- `nameFilter` (optional) - Wildcard pattern to filter view names
- `tx` (optional) - Database transaction
- `` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `List<DmView>` - List of complete DmView models

### GetViewAsync

Retrieve a single DmView model for a specific view.

```csharp
// Get specific view structure
DmView? view = await connection.GetViewAsync("app", "vw_active_employees");

if (view != null)
{
    Console.WriteLine($"View '{view.ViewName}' details:");
    Console.WriteLine($"  Schema: {view.SchemaName}");
    Console.WriteLine($"  Definition:");
    Console.WriteLine(view.ViewDefinition);

    // Analyze view dependencies (basic parsing)
    var tables = ExtractTableReferences(view.ViewDefinition);
    Console.WriteLine($"  References tables: {string.Join(", ", tables)}");
}
else
{
    Console.WriteLine("View not found");
}

// Helper method to extract table references (simplified)
private List<string> ExtractTableReferences(string? viewDefinition)
{
    if (string.IsNullOrEmpty(viewDefinition)) return new List<string>();

    // Basic regex to find table references (this is a simplified example)
    var fromPattern = @"FROM\s+(\w+\.?\w+)";
    var joinPattern = @"JOIN\s+(\w+\.?\w+)";

    var matches = new List<string>();
    matches.AddRange(System.Text.RegularExpressions.Regex.Matches(viewDefinition, fromPattern,
        System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        .Cast<System.Text.RegularExpressions.Match>()
        .Select(m => m.Groups[1].Value));

    matches.AddRange(System.Text.RegularExpressions.Regex.Matches(viewDefinition, joinPattern,
        System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        .Cast<System.Text.RegularExpressions.Match>()
        .Select(m => m.Groups[1].Value));

    return matches.Distinct().ToList();
}
```

**Parameters:**

- `schemaName` - Schema containing the view
- `viewName` - Name of the view to retrieve
- `tx` (optional) - Database transaction
- `` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `DmView?` - Complete view model, or `null` if view doesn't exist

## View Deletion

### DropViewIfExistsAsync

Delete a view permanently.

```csharp
// Drop view only if it exists
bool dropped = await connection.DropViewIfExistsAsync("app", "old_view");

if (dropped)
{
    Console.WriteLine("View was dropped successfully");
}
else
{
    Console.WriteLine("View did not exist");
}

// Batch drop multiple views
var viewsToRemove = new[] { "vw_temp_report", "vw_test_data", "vw_backup_view" };
foreach (var viewName in viewsToRemove)
{
    bool wasDropped = await connection.DropViewIfExistsAsync("app", viewName);
    Console.WriteLine($"View '{viewName}': {(wasDropped ? "Dropped" : "Not found")}");
}
```

**Parameters:**

- `schemaName` - Schema containing the view
- `viewName` - Name of the view to drop
- `tx` (optional) - Database transaction
- `` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if view was dropped, `false` if it didn't exist

## Practical Examples

### View-Based Security Layer

```csharp
public async Task CreateSecurityViewsAsync(IDbConnection connection)
{
    using var transaction = connection.BeginTransaction();
    try
    {
        // Create view that excludes sensitive salary information for non-managers
        await connection.CreateViewIfNotExistsAsync(
            "hr",
            "vw_employee_public_info",
            @"SELECT
                employee_id,
                first_name,
                last_name,
                email,
                department_name,
                hire_date,
                job_title
              FROM hr.vw_employee_details
              -- Salary information excluded for security",
            tx: transaction
        );

        // Create view for managers that includes salary information
        await connection.CreateViewIfNotExistsAsync(
            "hr",
            "vw_employee_manager_view",
            @"SELECT
                e.*,
                s.salary,
                s.bonus,
                s.effective_date
              FROM hr.vw_employee_details e
              LEFT JOIN hr.salaries s ON e.employee_id = s.employee_id
              WHERE s.is_current = 1
                OR s.is_current IS NULL",
            tx: transaction
        );

        // Create view for audit purposes
        await connection.CreateViewIfNotExistsAsync(
            "audit",
            "vw_employee_changes",
            @"SELECT
                ah.audit_id,
                ah.table_name,
                ah.operation_type,
                ah.changed_by,
                ah.changed_date,
                e.first_name + ' ' + e.last_name as employee_name
              FROM audit.audit_history ah
              LEFT JOIN hr.employees e ON ah.record_id = e.employee_id
              WHERE ah.table_name IN ('employees', 'salaries', 'departments')
              ORDER BY ah.changed_date DESC",
            tx: transaction
        );

        transaction.Commit();
        Console.WriteLine("Security views created successfully");
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

### Reporting Views Management

```csharp
public async Task CreateReportingViewsAsync(IDbConnection connection)
{
    var reportingViews = new Dictionary<string, string>
    {
        ["vw_monthly_sales_summary"] = @"
            SELECT
                YEAR(order_date) as year,
                MONTH(order_date) as month,
                DATENAME(month, order_date) as month_name,
                COUNT(*) as total_orders,
                SUM(order_total) as total_revenue,
                AVG(order_total) as avg_order_value,
                COUNT(DISTINCT customer_id) as unique_customers
            FROM sales.orders
            WHERE order_status = 'completed'
            GROUP BY YEAR(order_date), MONTH(order_date), DATENAME(month, order_date)",

        ["vw_top_customers"] = @"
            SELECT TOP 100
                c.customer_id,
                c.customer_name,
                c.email,
                COUNT(o.order_id) as total_orders,
                SUM(o.order_total) as total_spent,
                AVG(o.order_total) as avg_order_value,
                MAX(o.order_date) as last_order_date
            FROM sales.customers c
            INNER JOIN sales.orders o ON c.customer_id = o.customer_id
            WHERE o.order_status = 'completed'
            GROUP BY c.customer_id, c.customer_name, c.email
            ORDER BY total_spent DESC",

        ["vw_inventory_alerts"] = @"
            SELECT
                p.product_id,
                p.product_name,
                p.sku,
                i.quantity_on_hand,
                i.reorder_point,
                i.max_stock_level,
                CASE
                    WHEN i.quantity_on_hand <= i.reorder_point THEN 'REORDER'
                    WHEN i.quantity_on_hand <= (i.reorder_point * 1.2) THEN 'LOW STOCK'
                    WHEN i.quantity_on_hand >= i.max_stock_level THEN 'OVERSTOCK'
                    ELSE 'NORMAL'
                END as stock_status
            FROM inventory.products p
            INNER JOIN inventory.stock_levels i ON p.product_id = i.product_id
            WHERE i.quantity_on_hand <= i.reorder_point
               OR i.quantity_on_hand >= i.max_stock_level"
    };

    using var transaction = connection.BeginTransaction();
    try
    {
        foreach (var (viewName, viewDefinition) in reportingViews)
        {
            bool created = await connection.CreateViewIfNotExistsAsync(
                "reporting", viewName, viewDefinition, tx: transaction);

            Console.WriteLine($"Reporting view '{viewName}': {(created ? "Created" : "Already exists")}");
        }

        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

### View Dependency Analysis

```csharp
public async Task AnalyzeViewDependenciesAsync(IDbConnection connection, string schema)
{
    var views = await connection.GetViewsAsync(schema);
    var dependencies = new Dictionary<string, List<string>>();

    foreach (var view in views)
    {
        if (string.IsNullOrEmpty(view.ViewDefinition)) continue;

        // Extract table and view references from the definition
        var references = ExtractDatabaseReferences(view.ViewDefinition);
        dependencies[view.ViewName] = references;
    }

    // Analyze dependency chains
    Console.WriteLine($"View Dependency Analysis for schema '{schema}':");
    Console.WriteLine($"Total views: {views.Count}");

    // Find views with no dependencies (base views)
    var baseViews = dependencies.Where(kvp => !kvp.Value.Any()).Select(kvp => kvp.Key).ToList();
    Console.WriteLine($"Base views (no dependencies): {baseViews.Count}");
    foreach (var baseView in baseViews)
    {
        Console.WriteLine($"  - {baseView}");
    }

    // Find views that depend on other views
    var dependentViews = dependencies.Where(kvp => kvp.Value.Any(r => r.StartsWith("vw_"))).ToList();
    Console.WriteLine($"Views with view dependencies: {dependentViews.Count}");
    foreach (var (viewName, deps) in dependentViews)
    {
        var viewDeps = deps.Where(d => d.StartsWith("vw_")).ToList();
        Console.WriteLine($"  - {viewName} depends on: {string.Join(", ", viewDeps)}");
    }

    // Find potential circular dependencies
    DetectCircularDependencies(dependencies);
}

private List<string> ExtractDatabaseReferences(string viewDefinition)
{
    var references = new List<string>();

    // Patterns to match FROM and JOIN clauses
    var patterns = new[]
    {
        @"FROM\s+(\w+\.)?(\w+)",
        @"JOIN\s+(\w+\.)?(\w+)",
        @"UPDATE\s+(\w+\.)?(\w+)",
        @"INSERT\s+INTO\s+(\w+\.)?(\w+)"
    };

    foreach (var pattern in patterns)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(
            viewDefinition, pattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            // Get the table/view name (last capture group)
            var tableName = match.Groups[match.Groups.Count - 1].Value;
            if (!string.IsNullOrWhiteSpace(tableName))
            {
                references.Add(tableName);
            }
        }
    }

    return references.Distinct().ToList();
}

private void DetectCircularDependencies(Dictionary<string, List<string>> dependencies)
{
    var visited = new HashSet<string>();
    var inStack = new HashSet<string>();
    var circularDeps = new List<string>();

    foreach (var view in dependencies.Keys)
    {
        if (HasCircularDependency(view, dependencies, visited, inStack, new List<string>()))
        {
            circularDeps.Add(view);
        }
    }

    if (circularDeps.Any())
    {
        Console.WriteLine($"⚠️  Potential circular dependencies found in views:");
        foreach (var view in circularDeps.Distinct())
        {
            Console.WriteLine($"  - {view}");
        }
    }
    else
    {
        Console.WriteLine("✅ No circular dependencies detected");
    }
}

private bool HasCircularDependency(string view, Dictionary<string, List<string>> deps,
    HashSet<string> visited, HashSet<string> inStack, List<string> path)
{
    if (inStack.Contains(view))
    {
        Console.WriteLine($"    Circular path: {string.Join(" -> ", path)} -> {view}");
        return true;
    }

    if (visited.Contains(view)) return false;

    visited.Add(view);
    inStack.Add(view);
    path.Add(view);

    if (deps.ContainsKey(view))
    {
        var viewDeps = deps[view].Where(d => d.StartsWith("vw_"));
        foreach (var dep in viewDeps)
        {
            if (HasCircularDependency(dep, deps, visited, inStack, path))
            {
                return true;
            }
        }
    }

    inStack.Remove(view);
    path.RemoveAt(path.Count - 1);

    return false;
}
```

### View Performance Optimization

```csharp
public async Task OptimizeViewPerformanceAsync(IDbConnection connection, string schema)
{
    var views = await connection.GetViewsAsync(schema);

    foreach (var view in views)
    {
        if (string.IsNullOrEmpty(view.ViewDefinition)) continue;

        Console.WriteLine($"Analyzing view: {view.ViewName}");

        // Check for common performance issues
        var issues = new List<string>();

        if (view.ViewDefinition.ToUpper().Contains("SELECT *"))
        {
            issues.Add("Uses SELECT * - consider specifying explicit columns");
        }

        if (view.ViewDefinition.ToUpper().Contains("FUNCTION"))
        {
            issues.Add("Contains functions - may impact performance");
        }

        if (view.ViewDefinition.ToUpper().Contains("SUBQUERY") ||
            view.ViewDefinition.Count(c => c == '(') > 2)
        {
            issues.Add("Contains subqueries - consider JOIN alternatives");
        }

        if (view.ViewDefinition.ToUpper().Contains("ORDER BY") &&
            !view.ViewDefinition.ToUpper().Contains("TOP"))
        {
            issues.Add("Has ORDER BY without TOP - may cause unnecessary sorting");
        }

        if (issues.Any())
        {
            Console.WriteLine($"  Performance considerations:");
            foreach (var issue in issues)
            {
                Console.WriteLine($"    - {issue}");
            }
        }
        else
        {
            Console.WriteLine($"  ✅ No obvious performance issues detected");
        }

        // Suggest indexed views for SQL Server
        var provider = connection.GetDbProviderType();
        if (provider == DbProviderType.SqlServer &&
            view.ViewDefinition.ToUpper().Contains("GROUP BY"))
        {
            Console.WriteLine($"  💡 Consider creating indexed view for aggregation performance");
        }
    }
}
```

View methods provide powerful abstraction capabilities for your database, enabling you to create logical data layers, improve security through controlled access, and simplify complex queries for application consumption across all supported database providers.
