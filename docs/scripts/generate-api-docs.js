import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const packagesDir = path.join(__dirname, "../packages");
const apiOutputDir = path.join(__dirname, "../api");

// Ensure output directory exists
if (!fs.existsSync(apiOutputDir)) {
  fs.mkdirSync(apiOutputDir, { recursive: true });
}

// Helper to sanitize names for URLs
function sanitizeUrlName(name) {
  return name
    .toLowerCase()
    .replace(/[^a-z0-9-_.]/g, "-")
    .replace(/-+/g, "-")
    .replace(/^-|-$/g, "");
}

// Helper to sanitize inheritance URLs (preserves path structure)
function sanitizeInheritanceUrl(url) {
  return url
    .split("/")
    .map((part) => sanitizeUrlName(part))
    .join("/");
}

// Helper to format documentation text
function formatDocumentation(text) {
  if (!text) return "";
  // Convert <see cref="T:TypeName" /> to inline code
  return text.replace(/<see cref="[A-Z]:([^"]+)" \/>/g, "`$1`");
}

// Helper to decode Unicode-escaped angle brackets and format types for display
function decodeTypeString(typeStr) {
  if (!typeStr) return "void";
  return typeStr.replace(/\\u003C/g, "<").replace(/\\u003E/g, ">");
}

// Helper to extract linkable type from a type string (removes generics, nullables, etc.)
function extractBaseTypeName(typeStr) {
  if (!typeStr) return null;

  const decoded = decodeTypeString(typeStr);

  // Handle array types: Type[] -> Type
  let baseType = decoded.replace(/\[\]$/, "");

  // Handle nullable types: Type? -> Type
  baseType = baseType.replace(/\?$/, "");

  // Handle generic types: Task<Type> -> Task, List<Type> -> List
  const genericMatch = baseType.match(/^([^<]+)</);
  if (genericMatch) {
    return genericMatch[1];
  }

  return baseType;
}

// Helper to extract inner types from generics (for linking) - handles nested generics recursively
function extractGenericTypes(typeStr) {
  if (!typeStr) return [];

  const decoded = decodeTypeString(typeStr);
  const result = [];

  // Find all type names that could be documented (not system types)
  // This regex matches type names (word characters, no dots for namespace)
  const typeMatches = decoded.match(/\b[A-Z][a-zA-Z0-9_]*\b/g) || [];

  for (const match of typeMatches) {
    // Skip common system/primitive types but include our documented types
    if (!isPrimitiveType(match) && match.length > 1) {
      result.push(match);
    }
  }

  return [...new Set(result)]; // Remove duplicates
}

// Helper to format a type with proper linking
function formatTypeWithLinks(
  typeStr,
  documentedTypes,
  assemblyName,
  wrapInCode = true
) {
  if (!typeStr) return wrapInCode ? "`void`" : "void";

  const decoded = decodeTypeString(typeStr);
  let result = decoded;

  // Get all potential linkable types from this type string
  const baseType = extractBaseTypeName(typeStr);
  const genericTypes = extractGenericTypes(typeStr);
  const allTypes = [baseType, ...genericTypes].filter(Boolean);

  // Replace each documented type with a link (in order of specificity to avoid conflicts)
  const replacements = [];

  for (const type of allTypes) {
    if (isPrimitiveType(type)) continue;

    const typeKey = findDocumentedTypeKey(type, documentedTypes);
    if (typeKey) {
      const link = `[${type}](/api/${sanitizeUrlName(
        assemblyName
      )}/${typeKey})`;
      replacements.push({ type, link });
    }
  }

  // Sort by length (longer first) to avoid partial replacements
  replacements.sort((a, b) => b.type.length - a.type.length);

  // Apply replacements, but only if the type isn't already linked
  for (const { type, link } of replacements) {
    const regex = new RegExp(`\\b${escapeRegex(type)}\\b(?!\\]|\\))`, "g");
    result = result.replace(regex, link);
  }

  return wrapInCode ? `\`${result}\`` : result;
}

// Helper to check if a type is primitive
function isPrimitiveType(type) {
  const primitives = [
    "void",
    "bool",
    "byte",
    "sbyte",
    "char",
    "decimal",
    "double",
    "float",
    "int",
    "uint",
    "long",
    "ulong",
    "short",
    "ushort",
    "string",
    "object",
    "Task",
    "List",
    "Dictionary",
    "Array",
    "IEnumerable",
    "ICollection",
    "Version",
    "Type",
    "Attribute",
    "Exception",
    "CancellationToken",
    "IDbConnection",
    "IDbTransaction",
  ];
  return primitives.includes(type);
}

// Helper to find a documented type key (case-insensitive search)
function findDocumentedTypeKey(typeName, documentedTypes) {
  const searchName = typeName.toLowerCase();

  // Direct match first
  for (const key of documentedTypes) {
    if (key.toLowerCase().endsWith(`/${searchName}`)) {
      return key;
    }
  }

  // Handle generic type mapping: if searching for "DbProviderTypeMapBase" but have "dbprovidertypemapbase-1"
  // Try searching for the generic version
  for (const key of documentedTypes) {
    if (key.toLowerCase().endsWith(`/${searchName}-1`)) {
      return key;
    }
  }

  // Fallback: search for the type name anywhere in the key
  for (const key of documentedTypes) {
    const keyParts = key.toLowerCase().split("/");
    if (keyParts.includes(searchName)) {
      return key;
    }
  }

  return null;
}

// Helper to escape special regex characters
function escapeRegex(string) {
  return string.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

// Helper to create a shorter display name for namespaces
function createDisplayName(namespaceName, assemblyName) {
  // If the namespace exactly matches the assembly, use a root identifier
  if (namespaceName === assemblyName) {
    return "📦 Root";
  }

  // If the namespace starts with the assembly name, replace it with a shorter identifier
  if (namespaceName.startsWith(assemblyName + ".")) {
    const remainder = namespaceName.slice(assemblyName.length + 1);
    return `📦 / ${remainder}`;
  }

  // Otherwise, use the full namespace name
  return namespaceName;
}

// Helper to create clickable type links for parameters
function createTypeLink(typeName, assemblyName, documentedTypes) {
  if (!typeName || isPrimitiveType(typeName)) {
    return typeName;
  }

  // Reuse the same logic as inheritance links
  const foundTypeKey = findDocumentedTypeKey(typeName, documentedTypes);
  if (foundTypeKey) {
    const typeUrl = `/api/${sanitizeUrlName(assemblyName)}/${foundTypeKey}`;
    return `[${typeName}](${typeUrl})`;
  }

  return typeName;
}

// Helper to extract parameter types from commentId
function extractParameterTypes(commentId) {
  if (!commentId || commentId === "NOT FOUND") {
    return [];
  }

  // Find the parameters part: everything after the first '(' and before the last ')'
  const paramStart = commentId.indexOf("(");
  const paramEnd = commentId.lastIndexOf(")");
  if (paramStart === -1 || paramEnd === -1 || paramEnd <= paramStart) {
    return [];
  }

  const paramString = commentId.substring(paramStart + 1, paramEnd);
  if (!paramString.trim()) {
    return [];
  }

  return paramString.split(",").map((p) => {
    let type = p.trim();
    // Remove ref (@) and out (&) indicators
    if (type.endsWith("@") || type.endsWith("&")) {
      type = type.slice(0, -1);
    }
    // Remove namespace prefixes to get clean type names
    const parts = type.split(".");
    let cleanType = parts[parts.length - 1];

    // Handle some common type mappings
    if (cleanType === "String") cleanType = "string";
    else if (cleanType === "Int32") cleanType = "int";
    else if (cleanType === "Boolean") cleanType = "bool";
    else if (cleanType === "Double") cleanType = "double";
    else if (cleanType === "Single") cleanType = "float";
    else if (cleanType === "Int64") cleanType = "long";
    else if (cleanType === "Int16") cleanType = "short";
    else if (cleanType === "Byte") cleanType = "byte";
    else if (cleanType === "Object") cleanType = "object";

    return cleanType;
  });
}

// Generate method signature
function getMethodSignature(method) {
  const params = method.parameters || [];

  // Extract parameter types from commentId if available
  const parameterTypes = extractParameterTypes(method.commentId);

  const paramStr = params
    .map((p, index) => {
      // Use extracted type from commentId if available, otherwise fallback to p.type or "object"
      const paramType = parameterTypes[index] || p.type || "object";
      return `${paramType} ${p.name}`;
    })
    .join(", ");

  const methodName = method.title || method.name || "Method";
  return `${method.returnType || "void"} ${methodName}(${paramStr})`;
}

// Generate markdown for a type (class, interface, enum, etc.)
function generateTypeMarkdown(
  type,
  namespaceName,
  assemblyName,
  documentedTypes = new Set()
) {
  let markdown = `# ${type.name}\n\n`;
  markdown += `**Namespace:** [${namespaceName}](/api/${sanitizeUrlName(
    assemblyName
  )}/${sanitizeUrlName(namespaceName)})\n\n`;
  markdown += `**Assembly:** [${assemblyName}](/api/${sanitizeUrlName(
    assemblyName
  )})\n\n`;

  if (type.summary) {
    markdown += `## Summary\n\n${formatDocumentation(type.summary)}\n\n`;
  }

  // Inheritance information
  if (
    type.baseType ||
    (type.implementedInterfaces && type.implementedInterfaces.length > 0)
  ) {
    markdown += `## Inheritance\n\n`;

    if (type.baseType) {
      const baseTypeKey = sanitizeInheritanceUrl(type.baseType.url);
      // Extract just the type name for the generic lookup
      const typeNameOnly = baseTypeKey.split("/").pop();
      const foundTypeKey = findDocumentedTypeKey(typeNameOnly, documentedTypes);
      if (foundTypeKey) {
        const baseUrl = `/api/${sanitizeUrlName(assemblyName)}/${foundTypeKey}`;
        markdown += `**Base Class:** [${type.baseType.displayName}](${baseUrl})\n\n`;
      } else {
        markdown += `**Base Class:** ${type.baseType.displayName}\n\n`;
      }
    }

    if (type.implementedInterfaces && type.implementedInterfaces.length > 0) {
      markdown += `**Implemented Interfaces:**\n\n`;
      for (const iface of type.implementedInterfaces) {
        const ifaceTypeKey = sanitizeInheritanceUrl(iface.url);
        // Extract just the type name for the generic lookup
        const ifaceTypeNameOnly = ifaceTypeKey.split("/").pop();
        const foundIfaceKey = findDocumentedTypeKey(
          ifaceTypeNameOnly,
          documentedTypes
        );
        if (foundIfaceKey) {
          const ifaceUrl = `/api/${sanitizeUrlName(
            assemblyName
          )}/${foundIfaceKey}`;
          markdown += `- [${iface.displayName}](${ifaceUrl})\n`;
        } else {
          markdown += `- ${iface.displayName}\n`;
        }
      }
      markdown += "\n";
    }
  }

  // Type info badges
  const badges = [];
  if (type.payloadInfo) {
    if (type.payloadInfo.isStatic) badges.push("`static`");
    if (type.payloadInfo.isAbstract) badges.push("`abstract`");
    if (type.payloadInfo.isSealed) badges.push("`sealed`");
    if (type.payloadInfo.accessLevel)
      badges.push(`\`${type.payloadInfo.accessLevel.toLowerCase()}\``);
  }

  if (badges.length > 0) {
    markdown += badges.join(" ") + "\n\n";
  }

  // Note about inheritance - since the JSON doesn't include this info
  if (
    type.elementType === "Class" &&
    type.payloadInfo &&
    type.payloadInfo.isAbstract
  ) {
    markdown += `> **Note:** This is an abstract base class. Concrete implementations can be found in provider-specific namespaces.\n\n`;
  }

  // For interfaces, note that they define contracts
  if (type.elementType === "Interface") {
    markdown += `> **Note:** This is an interface that defines a contract. Look for implementing classes in the same or related namespaces.\n\n`;
  }

  // Generate table of contents for types with many members
  const tocSections = [];
  if (type.constructors && type.constructors.length > 0) {
    tocSections.push(
      `[Constructors](#constructors) (${type.constructors.length})`
    );
  }
  if (type.methods && type.methods.length > 0) {
    tocSections.push(`[Methods](#methods) (${type.methods.length})`);
  }
  if (type.properties && type.properties.length > 0) {
    tocSections.push(`[Properties](#properties) (${type.properties.length})`);
  }
  if (type.fields && type.fields.length > 0) {
    tocSections.push(`[Fields](#fields) (${type.fields.length})`);
  }
  if (type.elementType === "Enum" && type.enumMembers) {
    const enumCount = type.enumMembers.filter(
      (m) => m.name !== "value__"
    ).length;
    tocSections.push(`[Enum Members](#enum-members) (${enumCount})`);
  }

  if (tocSections.length > 0) {
    markdown += `## Contents\n\n`;
    markdown += tocSections.join(" | ") + "\n\n";
  }

  // Constructors
  if (type.constructors && type.constructors.length > 0) {
    markdown += `## Constructors\n\n`;
    for (const ctor of type.constructors) {
      markdown += `### ${type.name}\n\n`;
      if (ctor.summary) {
        markdown += `${formatDocumentation(ctor.summary)}\n\n`;
      }
      // Extract parameter types from constructor's commentId
      const ctorParamTypes = extractParameterTypes(ctor.commentId);

      markdown += `\`\`\`csharp\n${type.name}(${(ctor.parameters || [])
        .map((p, index) => {
          const paramType = ctorParamTypes[index] || p.type || "object";
          return `${paramType} ${p.name}`;
        })
        .join(", ")})\n\`\`\`\n\n`;

      if (ctor.parameters && ctor.parameters.length > 0) {
        markdown += `#### Parameters\n\n`;

        // Extract parameter types from commentId
        const parameterTypes = extractParameterTypes(ctor.commentId);

        ctor.parameters.forEach((param, index) => {
          const paramType = parameterTypes[index] || param.type || "object";
          const typeLink = createTypeLink(
            paramType,
            assemblyName,
            documentedTypes
          );
          markdown += `- **${param.name}** (${typeLink}) - ${
            param.text || "No description"
          }\n`;
        });
        markdown += "\n";
      }
    }
  }

  // Methods
  if (type.methods && type.methods.length > 0) {
    markdown += `## Methods\n\n`;

    // Add a quick reference table for methods
    if (type.methods.length > 5) {
      markdown += `| Method | Summary |\n`;
      markdown += `|--------|------|\n`;
      for (const method of type.methods) {
        const methodName = method.title || method.name;
        const summary = method.summary
          ? formatDocumentation(method.summary)
              .replace(/\n/g, " ")
              .substring(0, 100) + (method.summary.length > 100 ? "..." : "")
          : "";
        const anchor = methodName.toLowerCase().replace(/[^a-z0-9]/g, "-");
        markdown += `| [${methodName}](#${anchor}) | ${summary} |\n`;
      }
      markdown += "\n---\n\n";
    }

    for (const method of type.methods) {
      markdown += `### ${method.title || method.name}\n\n`;
      if (method.summary) {
        markdown += `${formatDocumentation(method.summary)}\n\n`;
      }

      markdown += `\`\`\`csharp\n${getMethodSignature(method)}\n\`\`\`\n\n`;

      if (method.parameters && method.parameters.length > 0) {
        markdown += `#### Parameters\n\n`;

        // Extract parameter types from commentId
        const parameterTypes = extractParameterTypes(method.commentId);

        method.parameters.forEach((param, index) => {
          const paramType = parameterTypes[index] || param.type || "object";
          const typeLink = createTypeLink(
            paramType,
            assemblyName,
            documentedTypes
          );
          markdown += `- **${param.name}** (${typeLink}) - ${
            param.text || "No description"
          }\n`;
        });
        markdown += "\n";
      }

      // Show return type information
      if (method.returnType && method.returnType !== "void") {
        markdown += `#### Returns\n\n`;
        const returnType = formatTypeWithLinks(
          method.returnType,
          documentedTypes,
          assemblyName,
          false
        );
        const typeDisplay =
          returnType.includes("[") && returnType.includes("](")
            ? returnType
            : `\`${returnType}\``;

        if (method.returns) {
          markdown += `**Type:** ${typeDisplay}\n\n${method.returns}\n\n`;
        } else {
          markdown += `**Type:** ${typeDisplay}\n\n`;
        }
      } else if (method.returns) {
        markdown += `#### Returns\n\n${method.returns}\n\n`;
      }
    }
  }

  // Properties
  if (type.properties && type.properties.length > 0) {
    markdown += `## Properties\n\n`;
    for (const prop of type.properties) {
      markdown += `### ${prop.name}\n\n`;
      if (prop.summary) {
        markdown += `${formatDocumentation(prop.summary)}\n\n`;
      }
      const propType = prop.returnType || prop.type || "object";
      const formattedType = formatTypeWithLinks(
        propType,
        documentedTypes,
        assemblyName,
        false
      );
      // If the result contains links, don't wrap in code; otherwise wrap in code
      if (formattedType.includes("[") && formattedType.includes("](")) {
        markdown += `**Type:** ${formattedType}\n\n`;
      } else {
        markdown += `**Type:** \`${formattedType}\`\n\n`;
      }
    }
  }

  // Fields
  if (type.fields && type.fields.length > 0) {
    markdown += `## Fields\n\n`;
    for (const field of type.fields) {
      markdown += `### ${field.name}\n\n`;
      if (field.summary) {
        markdown += `${formatDocumentation(field.summary)}\n\n`;
      }
      const fieldType = field.returnType || field.type || "object";
      const formattedType = formatTypeWithLinks(
        fieldType,
        documentedTypes,
        assemblyName,
        false
      );
      // If the result contains links, don't wrap in code; otherwise wrap in code
      if (formattedType.includes("[") && formattedType.includes("](")) {
        markdown += `**Type:** ${formattedType}\n\n`;
      } else {
        markdown += `**Type:** \`${formattedType}\`\n\n`;
      }
    }
  }

  // Enum members
  if (type.elementType === "Enum" && type.enumMembers) {
    markdown += `## Enum Members\n\n`;
    markdown += "| Name | Value | Description |\n";
    markdown += "|------|-------|-------------|\n";

    let enumValueIndex = 0;
    for (const enumMember of type.enumMembers) {
      // Skip the internal value__ member
      if (enumMember.name === "value__") continue;

      // The value might be explicitly set, or we infer it from position (0, 1, 2, etc.)
      const value =
        enumMember.value !== undefined && enumMember.value !== ""
          ? enumMember.value
          : enumValueIndex++;

      const description = formatDocumentation(enumMember.summary || "");
      markdown += `| ${enumMember.name} | ${value} | ${description} |\n`;
    }
    markdown += "\n";
  }

  return markdown;
}

// Generate markdown for a namespace
function generateNamespaceMarkdown(namespace, assemblyName) {
  let markdown = `# ${namespace.self.displayName}\n\n`;
  markdown += `**Assembly:** [${assemblyName}](/api/${sanitizeUrlName(
    assemblyName
  )})\n\n`;

  const typesByCategory = {
    Classes: [],
    Interfaces: [],
    Structs: [],
    Enums: [],
    Delegates: [],
  };

  // Sort types by category
  for (const type of namespace.types) {
    if (type.payloadInfo.accessLevel !== "Public") continue;

    switch (type.elementType) {
      case "Class":
        typesByCategory.Classes.push(type);
        break;
      case "Interface":
        typesByCategory.Interfaces.push(type);
        break;
      case "Struct":
        typesByCategory.Structs.push(type);
        break;
      case "Enum":
        typesByCategory.Enums.push(type);
        break;
      case "Delegate":
        typesByCategory.Delegates.push(type);
        break;
    }
  }

  // Generate sections for each category
  for (const [category, types] of Object.entries(typesByCategory)) {
    if (types.length === 0) continue;

    markdown += `## ${category}\n\n`;
    markdown += "| Name | Description |\n";
    markdown += "|------|-------------|\n";

    for (const type of types.sort((a, b) => a.name.localeCompare(b.name))) {
      const typeUrl = `/api/${sanitizeUrlName(assemblyName)}/${sanitizeUrlName(
        namespace.self.displayName
      )}/${sanitizeUrlName(type.name)}`;
      markdown += `| [${type.name}](${typeUrl}) | ${formatDocumentation(
        type.summary || ""
      )} |\n`;
    }

    markdown += "\n";
  }

  return markdown;
}

// Generate markdown for an assembly
function generateAssemblyMarkdown(assemblyName, apiData) {
  let markdown = `# ${assemblyName}\n\n`;

  if (apiData.description) {
    markdown += `${apiData.description}\n\n`;
  }

  markdown += `## Namespaces\n\n`;
  markdown += "| Namespace | Description |\n";
  markdown += "|----------|-------------|\n";

  for (const namespace of apiData.data) {
    const namespaceUrl = `/api/${sanitizeUrlName(
      assemblyName
    )}/${sanitizeUrlName(namespace.self.displayName)}`;
    markdown += `| [${namespace.self.displayName}](${namespaceUrl}) | Contains ${namespace.types.length} public types |\n`;
  }

  markdown += "\n";

  return markdown;
}

// Main function to generate API documentation
function generateApiDocs() {
  console.log("Generating API documentation...");

  // Get all JSON files in packages directory
  const jsonFiles = fs
    .readdirSync(packagesDir)
    .filter((file) => file.endsWith(".json"));

  // First pass: collect all documented types
  const documentedTypes = new Set();
  for (const jsonFile of jsonFiles) {
    const jsonPath = path.join(packagesDir, jsonFile);
    const apiData = JSON.parse(fs.readFileSync(jsonPath, "utf-8"));

    for (const namespace of apiData.data) {
      for (const type of namespace.types) {
        if (type.payloadInfo.accessLevel !== "Public") continue;
        const typeKey = `${sanitizeUrlName(
          namespace.self.displayName
        )}/${sanitizeUrlName(type.name)}`;
        documentedTypes.add(typeKey);
      }
    }
  }

  // Generate main API index
  let apiIndexMarkdown = `# API Reference\n\n`;
  apiIndexMarkdown += `This section contains the complete API reference for all assemblies.\n\n`;
  apiIndexMarkdown += `## Assemblies\n\n`;
  apiIndexMarkdown += "| Assembly | Description |\n";
  apiIndexMarkdown += "|----------|-------------|\n";

  const assemblyConfig = [];

  for (const jsonFile of jsonFiles) {
    const assemblyName = path.basename(jsonFile, ".json");
    const jsonPath = path.join(packagesDir, jsonFile);
    const apiData = JSON.parse(fs.readFileSync(jsonPath, "utf-8"));

    // Add to index
    apiIndexMarkdown += `| [${assemblyName}](/api/${sanitizeUrlName(
      assemblyName
    )}) | ${apiData.description || "No description available"} |\n`;

    // Create assembly directory
    const assemblyDir = path.join(apiOutputDir, sanitizeUrlName(assemblyName));
    if (!fs.existsSync(assemblyDir)) {
      fs.mkdirSync(assemblyDir, { recursive: true });
    }

    // Generate assembly index page
    const assemblyMarkdown = generateAssemblyMarkdown(assemblyName, apiData);
    fs.writeFileSync(path.join(assemblyDir, "index.md"), assemblyMarkdown);

    // Add to config
    const assemblyConfigItem = {
      text: assemblyName,
      link: `/api/${sanitizeUrlName(assemblyName)}/`,
      items: [],
    };

    // Generate pages for each namespace
    for (const namespace of apiData.data) {
      const namespaceName = namespace.self.displayName;
      const namespaceDir = path.join(
        assemblyDir,
        sanitizeUrlName(namespaceName)
      );

      if (!fs.existsSync(namespaceDir)) {
        fs.mkdirSync(namespaceDir, { recursive: true });
      }

      // Generate namespace index page
      const namespaceMarkdown = generateNamespaceMarkdown(
        namespace,
        assemblyName
      );
      fs.writeFileSync(path.join(namespaceDir, "index.md"), namespaceMarkdown);

      // Add namespace to config (without individual types)
      const namespaceConfigItem = {
        text: createDisplayName(namespaceName, assemblyName),
        link: `/api/${sanitizeUrlName(assemblyName)}/${sanitizeUrlName(
          namespaceName
        )}/`,
      };

      // Generate pages for each type
      for (const type of namespace.types) {
        if (type.payloadInfo.accessLevel !== "Public") continue;

        const typeMarkdown = generateTypeMarkdown(
          type,
          namespaceName,
          assemblyName,
          documentedTypes
        );
        const typeFileName = `${sanitizeUrlName(type.name)}.md`;
        fs.writeFileSync(path.join(namespaceDir, typeFileName), typeMarkdown);
      }

      assemblyConfigItem.items.push(namespaceConfigItem);
    }

    assemblyConfig.push(assemblyConfigItem);
  }

  // Write main API index
  fs.writeFileSync(path.join(apiOutputDir, "index.md"), apiIndexMarkdown);

  // Generate config snippet
  console.log("\nAdd this to your VitePress config.js sidebar configuration:");
  console.log(
    JSON.stringify(
      {
        "/api/": [
          {
            text: "API Reference",
            items: [{ text: "Overview", link: "/api/" }, ...assemblyConfig],
          },
        ],
      },
      null,
      2
    )
  );

  console.log("\nAPI documentation generation complete!");
}

// Run the generator
generateApiDocs();
