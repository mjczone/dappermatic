// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MJCZone.DapperMatic.AspNetCore.Accounts;

/// <summary>What an email from the built-in accounts is about.</summary>
public enum DapperMaticEmailKind
{
    /// <summary>An invitation: its link accepts it.</summary>
    Invitation,

    /// <summary>A confirmation of the email: its link confirms it.</summary>
    ConfirmEmail,

    /// <summary>A password reset: its code (and link, when one is set) resets the password.</summary>
    ResetPassword,

    /// <summary>A sign-up: its token (and link) makes the account, with the password the person chooses then.</summary>
    SignUp,
}

/// <summary>An email the built-in accounts send.</summary>
/// <param name="To">The address.</param>
/// <param name="Kind">What it is about.</param>
/// <param name="Link">The link it sends the person to, or null.</param>
/// <param name="Code">The code it carries, or null.</param>
#pragma warning disable SA1313 // Positional record parameters are properties.
public sealed record DapperMaticEmail(string To, DapperMaticEmailKind Kind, string? Link, string? Code);
#pragma warning restore SA1313

/// <summary>
/// Sends the built-in accounts' emails: the application's own (SMTP, a mail service). Without one, emails are logged,
/// with their links and codes only in Development.
/// </summary>
public interface IDapperMaticEmailSender
{
    /// <summary>
    /// Sends an email.
    /// </summary>
    /// <param name="email">The email.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task SendAsync(DapperMaticEmail email, CancellationToken cancellationToken);
}

/// <summary>
/// The emails of an application that gave no sender: logged, their links and codes (which sign people in) only in
/// Development.
/// </summary>
internal sealed class DapperMaticLoggedEmails(ILogger<DapperMaticLoggedEmails> logger, IHostEnvironment environment) : IDapperMaticEmailSender
{
    /// <inheritdoc />
    public Task SendAsync(DapperMaticEmail email, CancellationToken cancellationToken)
    {
        if (environment.IsDevelopment())
        {
            logger.LogInformation("DapperMatic email ({Kind}) to {To}: {Link} {Code}", email.Kind, email.To, email.Link, email.Code);
        }
        else
        {
            logger.LogWarning("DapperMatic could not send an email ({Kind}) to {To}: register an IDapperMaticEmailSender.", email.Kind, email.To);
        }

        return Task.CompletedTask;
    }
}
