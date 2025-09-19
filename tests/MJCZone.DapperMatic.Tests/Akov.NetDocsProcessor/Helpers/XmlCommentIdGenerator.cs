// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Reflection;
using System.Text;

namespace Akov.NetDocsProcessor.Helpers;

/// <summary>
/// Generates XML documentation comment IDs from reflection information
/// when Roslyn symbols are not available.
/// </summary>
internal static class XmlCommentIdGenerator
{
    /// <summary>
    /// Generates an XML documentation comment ID for a type.
    /// </summary>
    /// <param name="type">The type to generate the comment ID for.</param>
    /// <returns>The XML documentation comment ID string.</returns>
    public static string GenerateForType(Type type)
    {
        var sb = new StringBuilder("T:");
        AppendTypeName(sb, type, includeGenericParameters: true);
        return sb.ToString();
    }

    /// <summary>
    /// Generates an XML documentation comment ID for a method.
    /// </summary>
    /// <param name="method">The method to generate the comment ID for.</param>
    /// <returns>The XML documentation comment ID string.</returns>
    public static string GenerateForMethod(MethodBase method)
    {
        var sb = new StringBuilder("M:");
        
        // Append declaring type
        if (method.DeclaringType != null)
        {
            AppendTypeName(sb, method.DeclaringType, includeGenericParameters: true);
            sb.Append('.');
        }
        
        // Append method name
        AppendMethodName(sb, method);
        
        // Append parameters
        var parameters = method.GetParameters();
        if (parameters.Length > 0)
        {
            sb.Append('(');
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0) sb.Append(',');
                AppendParameterType(sb, parameters[i]);
            }
            sb.Append(')');
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates an XML documentation comment ID for a property.
    /// </summary>
    /// <param name="property">The property to generate the comment ID for.</param>
    /// <returns>The XML documentation comment ID string.</returns>
    public static string GenerateForProperty(PropertyInfo property)
    {
        var sb = new StringBuilder("P:");
        
        // Append declaring type
        if (property.DeclaringType != null)
        {
            AppendTypeName(sb, property.DeclaringType, includeGenericParameters: true);
            sb.Append('.');
        }
        
        // Append property name
        sb.Append(property.Name);
        
        // Handle indexer properties
        var indexParameters = property.GetIndexParameters();
        if (indexParameters.Length > 0)
        {
            sb.Append('(');
            for (int i = 0; i < indexParameters.Length; i++)
            {
                if (i > 0) sb.Append(',');
                AppendParameterType(sb, indexParameters[i]);
            }
            sb.Append(')');
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates an XML documentation comment ID for a field.
    /// </summary>
    /// <param name="field">The field to generate the comment ID for.</param>
    /// <returns>The XML documentation comment ID string.</returns>
    public static string GenerateForField(FieldInfo field)
    {
        var sb = new StringBuilder("F:");
        
        // Append declaring type
        if (field.DeclaringType != null)
        {
            AppendTypeName(sb, field.DeclaringType, includeGenericParameters: true);
            sb.Append('.');
        }
        
        // Append field name
        sb.Append(field.Name);
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates an XML documentation comment ID for an event.
    /// </summary>
    /// <param name="eventInfo">The event to generate the comment ID for.</param>
    /// <returns>The XML documentation comment ID string.</returns>
    public static string GenerateForEvent(EventInfo eventInfo)
    {
        var sb = new StringBuilder("E:");
        
        // Append declaring type
        if (eventInfo.DeclaringType != null)
        {
            AppendTypeName(sb, eventInfo.DeclaringType, includeGenericParameters: true);
            sb.Append('.');
        }
        
        // Append event name
        sb.Append(eventInfo.Name);
        
        return sb.ToString();
    }

    private static void AppendTypeName(StringBuilder sb, Type type, bool includeGenericParameters)
    {
        if (type.IsGenericParameter)
        {
            // For generic type parameters, use backtick notation
            if (type.DeclaringMethod != null)
            {
                // Method generic parameter - use double backtick
                sb.Append("``").Append(type.GenericParameterPosition);
            }
            else
            {
                // Type generic parameter - use single backtick
                sb.Append('`').Append(type.GenericParameterPosition);
            }
            return;
        }

        if (type.IsArray)
        {
            AppendTypeName(sb, type.GetElementType()!, includeGenericParameters);
            sb.Append("[]");
            return;
        }

        if (type.IsByRef)
        {
            AppendTypeName(sb, type.GetElementType()!, includeGenericParameters);
            sb.Append('@');
            return;
        }

        if (type.IsPointer)
        {
            AppendTypeName(sb, type.GetElementType()!, includeGenericParameters);
            sb.Append('*');
            return;
        }

        // Handle nested types
        if (type.IsNested)
        {
            AppendTypeName(sb, type.DeclaringType!, includeGenericParameters);
            sb.Append('.');
        }
        else if (!string.IsNullOrEmpty(type.Namespace))
        {
            sb.Append(type.Namespace).Append('.');
        }

        // Get the type name without generic parameters
        var typeName = type.Name;
        if (type.IsGenericType)
        {
            var backtickIndex = typeName.IndexOf('`');
            if (backtickIndex >= 0)
            {
                typeName = typeName.Substring(0, backtickIndex);
            }
        }

        sb.Append(typeName);

        // Append generic parameter count for generic types
        if (type.IsGenericType && includeGenericParameters)
        {
            var genericArgs = type.GetGenericArguments();
            var ownGenericParameterCount = genericArgs.Length;
            
            // For nested generic types, subtract parent's generic parameter count
            if (type.DeclaringType?.IsGenericType == true)
            {
                ownGenericParameterCount -= type.DeclaringType.GetGenericArguments().Length;
            }
            
            if (ownGenericParameterCount > 0)
            {
                sb.Append('`').Append(ownGenericParameterCount);
            }
        }
    }

    private static void AppendMethodName(StringBuilder sb, MethodBase method)
    {
        if (method is MethodInfo methodInfo)
        {
            // Handle special method names
            if (method.IsSpecialName)
            {
                if (method.Name.StartsWith("get_"))
                {
                    // This shouldn't happen as properties are handled separately
                    sb.Append(method.Name);
                    return;
                }
                if (method.Name.StartsWith("set_"))
                {
                    // This shouldn't happen as properties are handled separately
                    sb.Append(method.Name);
                    return;
                }
                if (method.Name.StartsWith("add_"))
                {
                    // This shouldn't happen as events are handled separately
                    sb.Append(method.Name);
                    return;
                }
                if (method.Name.StartsWith("remove_"))
                {
                    // This shouldn't happen as events are handled separately
                    sb.Append(method.Name);
                    return;
                }
            }

            // Handle operators
            switch (method.Name)
            {
                case "op_Addition":
                    sb.Append("op_Addition");
                    break;
                case "op_Subtraction":
                    sb.Append("op_Subtraction");
                    break;
                case "op_Multiply":
                    sb.Append("op_Multiply");
                    break;
                case "op_Division":
                    sb.Append("op_Division");
                    break;
                case "op_Modulus":
                    sb.Append("op_Modulus");
                    break;
                case "op_BitwiseAnd":
                    sb.Append("op_BitwiseAnd");
                    break;
                case "op_BitwiseOr":
                    sb.Append("op_BitwiseOr");
                    break;
                case "op_ExclusiveOr":
                    sb.Append("op_ExclusiveOr");
                    break;
                case "op_LeftShift":
                    sb.Append("op_LeftShift");
                    break;
                case "op_RightShift":
                    sb.Append("op_RightShift");
                    break;
                case "op_UnaryNegation":
                    sb.Append("op_UnaryNegation");
                    break;
                case "op_UnaryPlus":
                    sb.Append("op_UnaryPlus");
                    break;
                case "op_LogicalNot":
                    sb.Append("op_LogicalNot");
                    break;
                case "op_OnesComplement":
                    sb.Append("op_OnesComplement");
                    break;
                case "op_Increment":
                    sb.Append("op_Increment");
                    break;
                case "op_Decrement":
                    sb.Append("op_Decrement");
                    break;
                case "op_True":
                    sb.Append("op_True");
                    break;
                case "op_False":
                    sb.Append("op_False");
                    break;
                case "op_Equality":
                    sb.Append("op_Equality");
                    break;
                case "op_Inequality":
                    sb.Append("op_Inequality");
                    break;
                case "op_LessThan":
                    sb.Append("op_LessThan");
                    break;
                case "op_GreaterThan":
                    sb.Append("op_GreaterThan");
                    break;
                case "op_LessThanOrEqual":
                    sb.Append("op_LessThanOrEqual");
                    break;
                case "op_GreaterThanOrEqual":
                    sb.Append("op_GreaterThanOrEqual");
                    break;
                case "op_Implicit":
                    sb.Append("op_Implicit");
                    break;
                case "op_Explicit":
                    sb.Append("op_Explicit");
                    break;
                default:
                    sb.Append(method.Name);
                    break;
            }
        }
        else
        {
            sb.Append(method.Name);
        }

        // Append generic parameter count for generic methods
        if (method.IsGenericMethodDefinition)
        {
            var genericArgs = method.GetGenericArguments();
            sb.Append("``").Append(genericArgs.Length);
        }
    }

    private static void AppendParameterType(StringBuilder sb, ParameterInfo parameter)
    {
        var parameterType = parameter.ParameterType;
        
        // Handle ref and out parameters
        if (parameterType.IsByRef)
        {
            parameterType = parameterType.GetElementType()!;
            AppendTypeName(sb, parameterType, includeGenericParameters: false);
            sb.Append('@');
        }
        else
        {
            AppendTypeName(sb, parameterType, includeGenericParameters: false);
        }
    }
}