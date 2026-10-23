// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.Dml;

namespace MJCZone.DapperMatic.AspNetCore.Accounts;

/// <summary>
/// ASP.NET Core Identity's store of the built-in accounts, in DapperMatic's tables (its own DML): passwords, emails,
/// security stamps, lockout, two-factor sign-in, authenticator keys and recovery codes. Identity does the security;
/// this keeps what it decides. Roles are kept by DapperMatic itself, per place.
/// </summary>
internal sealed class DapperMaticUserStore :
    IUserPasswordStore<DapperMaticUser>,
    IUserEmailStore<DapperMaticUser>,
    IUserSecurityStampStore<DapperMaticUser>,
    IUserLockoutStore<DapperMaticUser>,
    IUserTwoFactorStore<DapperMaticUser>,
    IUserAuthenticatorKeyStore<DapperMaticUser>,
    IUserTwoFactorRecoveryCodeStore<DapperMaticUser>,
    IUserPhoneNumberStore<DapperMaticUser>
{
    private readonly DapperMaticAccountDb _db;
    private readonly IdentityOptions _identity;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticUserStore"/> class.
    /// </summary>
    /// <param name="db">The account tables' database.</param>
    /// <param name="identity">Identity's options: its lockout.</param>
    public DapperMaticUserStore(DapperMaticAccountDb db, IOptions<IdentityOptions> identity)
    {
        _db = db;
        _identity = identity.Value;
    }

    /// <inheritdoc />
    public async Task<IdentityResult> CreateAsync(DapperMaticUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _db.Scope(db).InsertAsync(user, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (DmDatabaseErrors.Classify(ex) == DmDatabaseErrorKind.UniqueViolation)
        {
            return IdentityResult.Failed(new IdentityError { Code = "DuplicateUserName", Description = "An account has this email." });
        }

        return IdentityResult.Success;
    }

    /// <inheritdoc />
    public async Task<IdentityResult> UpdateAsync(DapperMaticUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        // Changed only as it was read: another change since fails this one, as Identity expects.
        var read = user.ConcurrencyStamp;
        user.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        var changed = await _db.Scope(db)
            .UpdateAsync<DapperMaticUser>(
                u => new DapperMaticUser
                {
                    UserName = user.UserName,
                    NormalizedUserName = user.NormalizedUserName,
                    Email = user.Email,
                    NormalizedEmail = user.NormalizedEmail,
                    EmailConfirmed = user.EmailConfirmed,
                    PasswordHash = user.PasswordHash,
                    SecurityStamp = user.SecurityStamp,
                    ConcurrencyStamp = user.ConcurrencyStamp,
                    PhoneNumber = user.PhoneNumber,
                    PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                    TwoFactorEnabled = user.TwoFactorEnabled,
                    LockoutEnd = user.LockoutEnd,
                    LockoutEnabled = user.LockoutEnabled,
                    AccessFailedCount = user.AccessFailedCount,
                    AuthenticatorKey = user.AuthenticatorKey,
                    RecoveryCodes = user.RecoveryCodes,
                    Disabled = user.Disabled,
                },
                u => u.Id == user.Id && u.ConcurrencyStamp == read,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
        if (changed == 0)
        {
            user.ConcurrencyStamp = read;
            return IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure());
        }

        return IdentityResult.Success;
    }

    /// <inheritdoc />
    public async Task<IdentityResult> DeleteAsync(DapperMaticUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await _db.Scope(db).DeleteByIdAsync<DapperMaticUser>(user.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        return IdentityResult.Success;
    }

    /// <inheritdoc />
    public async Task<DapperMaticUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await _db.Scope(db).SingleByIdAsync<DapperMaticUser>(userId, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DapperMaticUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await _db.Scope(db).SingleAsync<DapperMaticUser>(u => u.NormalizedUserName == normalizedUserName, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DapperMaticUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await _db.Scope(db).SingleAsync<DapperMaticUser>(u => u.NormalizedEmail == normalizedEmail, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<string> GetUserIdAsync(DapperMaticUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id);

    /// <inheritdoc />
    public Task<string?> GetUserNameAsync(DapperMaticUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);

    /// <inheritdoc />
    public Task SetUserNameAsync(DapperMaticUser user, string? userName, CancellationToken cancellationToken) => Set(() => user.UserName = userName);

    /// <inheritdoc />
    public Task<string?> GetNormalizedUserNameAsync(DapperMaticUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);

    /// <inheritdoc />
    public Task SetNormalizedUserNameAsync(DapperMaticUser user, string? normalizedName, CancellationToken cancellationToken) => Set(() => user.NormalizedUserName = normalizedName);

    /// <inheritdoc />
    public Task SetPasswordHashAsync(DapperMaticUser user, string? passwordHash, CancellationToken cancellationToken) => Set(() => user.PasswordHash = passwordHash);

    /// <inheritdoc />
    public Task<string?> GetPasswordHashAsync(DapperMaticUser user, CancellationToken cancellationToken) => Task.FromResult(user.PasswordHash);

    /// <inheritdoc />
    public Task<bool> HasPasswordAsync(DapperMaticUser user, CancellationToken cancellationToken) => Task.FromResult(user.PasswordHash is not null);

    /// <inheritdoc />
    public Task SetEmailAsync(DapperMaticUser user, string? email, CancellationToken cancellationToken) => Set(() => user.Email = email);

    /// <inheritdoc />
    public Task<string?> GetEmailAsync(DapperMaticUser user, CancellationToken cancellationToken) => Task.FromResult(user.Email);

    /// <inheritdoc />
    public Task<bool> GetEmailConfirmedAsync(DapperMaticUser user, CancellationToken cancellationToken) => Task.FromResult(user.EmailConfirmed);

    /// <inheritdoc />
    public Task SetEmailConfirmedAsync(DapperMaticUser user, bool confirmed, CancellationToken cancellationToken) => Set(() => user.EmailConfirmed = confirmed);

    /// <inheritdoc />
    public Task<string?> GetNormalizedEmailAsync(DapperMaticUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedEmail);

    /// <inheritdoc />
    public Task SetNormalizedEmailAsync(DapperMaticUser user, string? normalizedEmail, CancellationToken cancellationToken) => Set(() => user.NormalizedEmail = normalizedEmail);

    /// <inheritdoc />
    public Task SetSecurityStampAsync(DapperMaticUser user, string stamp, CancellationToken cancellationToken) => Set(() => user.SecurityStamp = stamp);

    /// <inheritdoc />
    public Task<string?> GetSecurityStampAsync(DapperMaticUser user, CancellationToken cancellationToken) => Task.FromResult(user.SecurityStamp);

    /// <inheritdoc />
    public Task<DateTimeOffset?> GetLockoutEndDateAsync(DapperMaticUser user, CancellationToken cancellationToken) => Task.FromResult(user.LockoutEnd);

    /// <inheritdoc />
    public Task SetLockoutEndDateAsync(DapperMaticUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken) => Set(() => user.LockoutEnd = lockoutEnd);

    /// <inheritdoc />
    /// <remarks>
    /// Counted in the database, not on the row as read, so that failed sign-ins at the same time each count; the row's
    /// concurrency stamp changes, so that none writes an older count back, and the lockout is kept here when the count
    /// reaches it, whatever happens to the write after.
    /// </remarks>
    public async Task<int> IncrementAccessFailedCountAsync(DapperMaticUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        var scope = _db.Scope(db);
        var id = user.Id;
        var stamp = Guid.NewGuid().ToString("N");
        await scope.UpdateAsync<DapperMaticUser>(u => new DapperMaticUser { AccessFailedCount = u.AccessFailedCount + 1, ConcurrencyStamp = stamp }, u => u.Id == id, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        // The row as it is now (another request may have locked the account since this one read it), with the stamp this
        // write set: the row written back after is never older than the database's, and a row changed since is not
        // written back at all.
        if (await scope.SingleByIdAsync<DapperMaticUser>(id, cancellationToken: cancellationToken).ConfigureAwait(false) is { } now)
        {
            Reload(user, now);
        }
        else
        {
            user.AccessFailedCount++;
        }

        user.ConcurrencyStamp = stamp;
        var lockout = _identity.Lockout;
        if (user.LockoutEnabled && user.AccessFailedCount >= lockout.MaxFailedAccessAttempts)
        {
            DateTimeOffset? end = DateTimeOffset.UtcNow + lockout.DefaultLockoutTimeSpan;
            var locked = Guid.NewGuid().ToString("N");
            await scope.UpdateAsync<DapperMaticUser>(u => new DapperMaticUser { LockoutEnd = end, ConcurrencyStamp = locked }, u => u.Id == id, cancellationToken: cancellationToken).ConfigureAwait(false);
            (user.LockoutEnd, user.ConcurrencyStamp) = (end, locked);
        }

        return user.AccessFailedCount;
    }

    /// <inheritdoc />
    public Task ResetAccessFailedCountAsync(DapperMaticUser user, CancellationToken cancellationToken) => Set(() => user.AccessFailedCount = 0);

    /// <inheritdoc />
    public Task<int> GetAccessFailedCountAsync(DapperMaticUser user, CancellationToken cancellationToken) => Task.FromResult(user.AccessFailedCount);

    /// <inheritdoc />
    public Task<bool> GetLockoutEnabledAsync(DapperMaticUser user, CancellationToken cancellationToken) => Task.FromResult(user.LockoutEnabled);

    /// <inheritdoc />
    public Task SetLockoutEnabledAsync(DapperMaticUser user, bool enabled, CancellationToken cancellationToken) => Set(() => user.LockoutEnabled = enabled);

    /// <inheritdoc />
    public Task SetTwoFactorEnabledAsync(DapperMaticUser user, bool enabled, CancellationToken cancellationToken) => Set(() => user.TwoFactorEnabled = enabled);

    /// <inheritdoc />
    public Task<bool> GetTwoFactorEnabledAsync(DapperMaticUser user, CancellationToken cancellationToken) => Task.FromResult(user.TwoFactorEnabled);

    /// <inheritdoc />
    public Task SetAuthenticatorKeyAsync(DapperMaticUser user, string key, CancellationToken cancellationToken) => Set(() => user.AuthenticatorKey = key);

    /// <inheritdoc />
    public Task<string?> GetAuthenticatorKeyAsync(DapperMaticUser user, CancellationToken cancellationToken) => Task.FromResult(user.AuthenticatorKey);

    /// <inheritdoc />
    public Task ReplaceCodesAsync(DapperMaticUser user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken) =>
        Set(() => user.RecoveryCodes = string.Join(';', recoveryCodes));

    /// <inheritdoc />
    public Task<bool> RedeemCodeAsync(DapperMaticUser user, string code, CancellationToken cancellationToken)
    {
        var codes = (user.RecoveryCodes ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (!codes.Remove(code))
        {
            return Task.FromResult(false);
        }

        user.RecoveryCodes = string.Join(';', codes);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<int> CountCodesAsync(DapperMaticUser user, CancellationToken cancellationToken) =>
        Task.FromResult((user.RecoveryCodes ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries).Length);

    /// <inheritdoc />
    public Task SetPhoneNumberAsync(DapperMaticUser user, string? phoneNumber, CancellationToken cancellationToken) => Set(() => user.PhoneNumber = phoneNumber);

    /// <inheritdoc />
    public Task<string?> GetPhoneNumberAsync(DapperMaticUser user, CancellationToken cancellationToken) => Task.FromResult(user.PhoneNumber);

    /// <inheritdoc />
    public Task<bool> GetPhoneNumberConfirmedAsync(DapperMaticUser user, CancellationToken cancellationToken) => Task.FromResult(user.PhoneNumberConfirmed);

    /// <inheritdoc />
    public Task SetPhoneNumberConfirmedAsync(DapperMaticUser user, bool confirmed, CancellationToken cancellationToken) => Set(() => user.PhoneNumberConfirmed = confirmed);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    // A change Identity makes to the user in memory; kept by its UpdateAsync.
    private static Task Set(Action change)
    {
        change();
        return Task.CompletedTask;
    }

    // An account's row as read, into the one Identity holds (its concurrency stamp aside).
    private static void Reload(DapperMaticUser user, DapperMaticUser now)
    {
        user.UserName = now.UserName;
        user.NormalizedUserName = now.NormalizedUserName;
        user.Email = now.Email;
        user.NormalizedEmail = now.NormalizedEmail;
        user.EmailConfirmed = now.EmailConfirmed;
        user.PasswordHash = now.PasswordHash;
        user.SecurityStamp = now.SecurityStamp;
        user.PhoneNumber = now.PhoneNumber;
        user.PhoneNumberConfirmed = now.PhoneNumberConfirmed;
        user.TwoFactorEnabled = now.TwoFactorEnabled;
        user.LockoutEnd = now.LockoutEnd;
        user.LockoutEnabled = now.LockoutEnabled;
        user.AccessFailedCount = now.AccessFailedCount;
        user.AuthenticatorKey = now.AuthenticatorKey;
        user.RecoveryCodes = now.RecoveryCodes;
        user.Disabled = now.Disabled;
    }
}
