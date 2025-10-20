// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests.Logging;

public class TestLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public TestLoggerProvider(ITestOutputHelper output)
    {
        _output = output;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new TestLogger(_output, categoryName);
    }

    public void Dispose() { }
}
