// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// An expression the DML methods cannot write as SQL. Nothing is evaluated in memory instead: the query is refused
/// before anything is sent.
/// </summary>
public sealed class DmTranslationException : NotSupportedException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmTranslationException"/> class.
    /// </summary>
    /// <param name="expression">The part of the expression that cannot be translated.</param>
    /// <param name="reason">Why, and what to write instead.</param>
    public DmTranslationException(Expression expression, string reason)
        : base($"{expression} cannot be translated to SQL: {reason}")
    {
        Expression = expression.ToString();
    }

    /// <summary>
    /// Gets the part of the expression that cannot be translated.
    /// </summary>
    public string Expression { get; }
}
