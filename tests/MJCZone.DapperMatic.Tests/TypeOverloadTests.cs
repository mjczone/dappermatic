// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using MJCZone.DapperMatic.Dml;
using MJCZone.DapperMatic.Migrations;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// Every method that takes a model as a type argument also takes it as a <see cref="Type"/>, for models known only
/// at run time. The exceptions take expressions over the model, or options holding them, which need it at compile
/// time.
/// </summary>
public class TypeOverloadTests
{
    public static TheoryData<Type> Classes() =>
        [typeof(DbConnectionExtensions), typeof(DmlConnectionExtensions), typeof(DmScope), typeof(DmMigrator)];

    [Theory]
    [MemberData(nameof(Classes))]
    public void Each_generic_model_method_has_a_type_overload(Type declaring)
    {
        var methods = declaring.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        var missing = new List<string>();
        var checkedCount = 0;

        foreach (var generic in methods.Where(m => m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1))
        {
            var t = generic.GetGenericArguments()[0];
            var parameters = generic.GetParameters();
            if (parameters.Any(p => IsTypedOver(p.ParameterType, t)) || generic.Name == nameof(DmScope.From))
            {
                continue;
            }

            checkedCount++;
            var isExtension = generic.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute));
            var typeAt = isExtension ? 1 : 0;
            var found = methods.Any(m =>
                !m.IsGenericMethodDefinition
                && m.Name == generic.Name
                && m.GetParameters() is var candidate
                && candidate.Length == parameters.Length + 1
                && candidate[typeAt].ParameterType == typeof(Type)
                && candidate
                    .Where((_, i) => i != typeAt)
                    .Select(p => p.Name)
                    .SequenceEqual(parameters.Select(p => p.Name))
            );

            if (!found)
            {
                missing.Add($"{generic.Name}({string.Join(", ", parameters.Select(p => p.Name))})");
            }
        }

        Assert.True(checkedCount > 0);
        Assert.True(missing.Count == 0, $"{declaring.Name} has no Type overload of: {string.Join("; ", missing)}");
    }

    // An expression over T, or an options object of T holding them (DmUpsertOptions<T>): T is needed at compile
    // time. A row (T) or rows (IEnumerable<T>) are not.
    private static bool IsTypedOver(Type parameterType, Type t) =>
        parameterType.IsGenericType
        && parameterType.GetGenericTypeDefinition() != typeof(IEnumerable<>)
        && parameterType.ToString().Contains(t.Name, StringComparison.Ordinal);
}
