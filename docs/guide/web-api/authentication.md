# Sign-in

DapperMatic's endpoints need to know who is calling before [policies](./security.md) can say what the caller may do. There are three ways to sign callers in, and they can be used together:

| Who | How they sign in | Turned on with |
|-----|------------------|----------------|
| People and programs your application already signs in | Your authentication: Entra ID, Auth0, Keycloak, cookies, … | `SignInWith(...)`, or nothing (your default scheme) |
| Programs | API keys: a service account's, or a person's own | `AddApiKeys()` |
| People, when your application has no sign-in of its own | DapperMatic's built-in accounts (ASP.NET Core Identity) | `AddUserAccounts(...)` |

Every way ends the same: a caller with roles, maybe a tenant, held to the policies.

## Your own sign-in

With nothing configured, DapperMatic's endpoints read the caller from your application's default authentication scheme:

```csharp
builder.Services.AddAuthentication("Bearer").AddJwtBearer("Bearer", o =>
{
    o.Authority = "https://login.example.com";
    o.Audience = "orders-api";
});
builder.Services.AddDapperMatic(dm => dm.Policy(DmPolicy.Allow(DmAction.Read, "authenticated")));
```

To accept several schemes, or schemes that are not the default, list them. They are tried in order, and the first that signs the caller in wins. A caller whose credentials every scheme refuses gets **401**. A caller who sends none is anonymous.

```csharp
builder.Services.AddDapperMatic(dm => dm.SignInWith("Bearer", "Cookies"));
```

Once DapperMatic signs callers in itself (keys or accounts), it registers a scheme named `DapperMatic` (`DapperMaticAuthenticationDefaults.Scheme`) that runs this list: DapperMatic's own methods first, then yours. Name that scheme in your own authorization policies to accept the same callers on your own endpoints; a sign-in that has ended (a key revoked, an account turned off, a membership gone) is refused there too. An API key's limits are DapperMatic's, though: your endpoints see the key's owner's roles, so refuse keys where that matters (`DapperMaticClaims.ByKey(User)`). If your application registers only one scheme, it stays your default.

### Roles

Policies name roles. A caller's roles are their role claims (`ClaimTypes.Role`, or the role type of their identity). Identity providers often put roles elsewhere; tell DapperMatic where:

```csharp
dm.RolesFrom("roles")                                // Entra ID: one claim per role
  .RolesFrom("https://acme.com/roles")               // Auth0: a namespaced claim, values or a JSON array
  .RolesFrom("realm_access", "roles")                // Keycloak: a path into a JSON claim
  .RolesFrom(async (http, user, cancellationToken) =>
  {
      // Anything else, by code: your own tables, a directory, ...
      var directory = http.RequestServices.GetRequiredService<IDirectory>();
      return await directory.RolesOfAsync(user.FindFirstValue("sub"), cancellationToken);
  });
```

A claim's values may each be a role, or one value may hold a JSON array of them. Sources add up. Roles found this way count only in DapperMatic's checks; they are read once, when the caller's session is built.

### Recipes

**Microsoft Entra ID.** App roles arrive in the `roles` claim.

```csharp
builder.Services.AddAuthentication("Bearer").AddJwtBearer("Bearer", o =>
{
    o.Authority = "https://login.microsoftonline.com/{tenant-id}/v2.0";
    o.Audience = "api://orders-api";
});
builder.Services.AddDapperMatic(dm => dm.RolesFrom("roles"));
```

**Auth0.** Add roles to the access token with an Action under a namespaced claim, then read that claim.

```csharp
builder.Services.AddAuthentication("Bearer").AddJwtBearer("Bearer", o =>
{
    o.Authority = "https://acme.eu.auth0.com/";
    o.Audience = "https://orders.acme.com";
});
builder.Services.AddDapperMatic(dm => dm.RolesFrom("https://acme.com/roles"));
```

**Keycloak.** Realm roles are in `realm_access.roles`, client roles in `resource_access.{client}.roles`.

```csharp
builder.Services.AddAuthentication("Bearer").AddJwtBearer("Bearer", o =>
{
    o.Authority = "https://keycloak.example.com/realms/acme";
    o.Audience = "orders-api";
});
builder.Services.AddDapperMatic(dm => dm
    .RolesFrom("realm_access", "roles")
    .RolesFrom("resource_access", "orders-api.roles"));
```

The tenant of a caller signed in by your own authentication comes from your [tenancy sources](./tenancy.md#finding-the-tenant), for example `FromClaim("tenant_id")`.

## Streams and sockets: tickets

A browser cannot set an `Authorization` header on an `EventSource` or a WebSocket. Instead of putting a token in a URL, where logs keep it, ask for a ticket:

```http
POST /api/dm/auth/ticket
Authorization: Bearer …
Content-Type: application/json

{ "path": "/api/dm/d/main/t/orders/subscribe" }
```

```json
{ "ticket": "q1Xo…", "expiresIn": 30 }
```

Then open the stream with `?ticket=…`:

```js
const { ticket } = await (await fetch("/api/dm/auth/ticket", { method: "POST", headers, body: JSON.stringify({ path }) })).json();
const events = new EventSource(`${path}?ticket=${ticket}`);
```

A ticket opens one stream once: the path it was made for (without the query), within 30 seconds (`options.Tickets.Lifetime`). It signs the stream in as the caller who asked for it, in the tenant they were in, until their own sign-in would have ended. Realtime subscriptions and GraphQL sockets accept tickets; nothing else does. Tickets are kept where API keys are, so every instance of the application can redeem them.

## API keys

```csharp
builder.Services.AddDapperMatic(dm => dm.AddApiKeys());
```

A key is sent as `Authorization: Bearer dm_…` or in the `X-Api-Key` header, never in a URL. It looks like `dm_{id}_{secret}`: the prefix lets secret scanners recognise a leaked key, the id names it in lists and audit events, and only a hash of the secret is kept. A key is shown once, when it is made.

There are two kinds, as with OpenAI's and Anthropic's keys:

- **Service account keys** sign in as a service account: an account for a program (an import job, a partner's system), with its own roles and maybe a tenant. They outlive the people who made them.
- **Personal keys** sign in as the person who made them, as that person is now: if their roles change, so does what the key may do, and when they lose their membership of the key's tenant, or their account is turned off, the key stops.

A key may also have **limits**, which only take away: the shortcuts (read only, some datasources, schemas or tables), or [policies](./security.md) of their own (without presets), not both. A key never does more than its owner may, and a key confined to some datasources, schemas or tables manages no access.

### Service accounts

In code:

```csharp
dm.ServiceAccount(new DapperMaticServiceAccount { Name = "nightly-import", Tenant = "42", Roles = { "importer" } });
```

Or through the API, by those who manage access:

```http
PUT /api/dm/access/service-accounts/nightly-import
{ "tenant": "42", "roles": ["importer"], "description": "The nightly price import" }

POST /api/dm/access/service-accounts/nightly-import/keys
{ "name": "prod", "expiresAt": "2027-10-01T00:00:00Z", "tables": ["prices"] }
```

```json
{ "result": { "key": "dm_k3…", "info": { "id": "k3…", "name": "prod", "limits": [ … ], … } } }
```

`GET /access/keys` lists keys (never their secrets), with when each was last used. `DELETE /access/keys/{id}` revokes one: at once on the server that revokes it, and on the others within `AccessRefreshInterval` (30 seconds). A [tenant administrator](./security.md#tenant-administrators) manages only their tenant's service accounts and keys, and gives only roles they hold themselves, or roles only their tenant's policies name (a role no policy names may mean something to your own code).

### Personal keys

People make their own:

```http
POST /api/dm/auth/keys
{ "name": "laptop", "readOnly": true }
```

`GET /auth/keys` lists them, `DELETE /auth/keys/{id}` revokes one and `DELETE /auth/keys` all. A key cannot make keys, change its owner's account, invite anyone or give grants.

Personal keys need to find their owner as they are now. DapperMatic's built-in accounts do that themselves. With your own sign-in, give `ResolveOwner`:

```csharp
dm.AddApiKeys(k =>
{
    k.ResolveOwner = async (ownerId, tenant, services, cancellationToken) =>
    {
        var users = services.GetRequiredService<IUserDirectory>();
        return await users.PrincipalOfAsync(ownerId, tenant, cancellationToken); // null when gone
    };
    k.MaxPersonalKeyLifetime = TimeSpan.FromDays(90);   // optional
    k.PersonalKeyRoles.Clear();                         // or: nobody makes their own
});
```

| Setting | Default | |
|---------|---------|--|
| `Prefix` | `dm` | What keys begin with |
| `HeaderName` | `X-Api-Key` | The header a key may be sent in besides `Authorization` |
| `MaxPersonalKeyLifetime`, `MaxServiceKeyLifetime` | none | The longest a key may last; none means expiry is optional |
| `PersonalKeyRoles` | `authenticated` | Who may make their own keys |

Keys are kept where DapperMatic keeps policies and tenants: in its database (`dm_api_keys`), beside its file, or in memory. `dm.UseKeyRepository<T>()` keeps them in your own store.

## Built-in accounts

For applications without a sign-in of their own, DapperMatic keeps accounts itself, on ASP.NET Core Identity (part of ASP.NET Core, no extra package):

```csharp
builder.Services.AddDapperMatic(dm => dm
    .AddUserAccounts(a => a.UseDatasource("main"))
    .Policy(DmPolicy.Allow(DmAction.All, "admin"))
    .Policy(DmPolicy.Allow(DmAction.ReadWrite, "member")));
```

The tables are made at startup (`dm_users`, `dm_user_roles`, `dm_memberships`, `dm_invitations`, `dm_user_sessions`, `dm_account_settings`, `dm_data_protection_keys`) in the datasource named, or the database given with `UseDatabase(provider, connectionString)`, or else where DapperMatic keeps policies (its database repository). Kept in a datasource the API serves, they are still served by no endpoint, whatever the policies grant: see [Tables no policy reaches](./security#tables-no-policy-reaches). An application that registers ASP.NET Core Identity itself is refused at startup: use one or the other.

### The first administrator

Name them in code or configuration, and they get an invitation when the application starts:

```json
{ "DapperMatic": { "Accounts": { "FirstAdmin": { "Email": "ops@example.com" } } } }
```

Without one, while there is no account, DapperMatic writes a one-time setup code to the log. Use it once:

```http
POST /api/dm/auth/setup
{ "code": "9F2C…", "email": "ops@example.com", "password": "…" }
```

The first administrator gets the platform role `admin` (`FirstAdmin.Roles`); policies say what that role may do.

### Tenants and places

An account can belong to several tenants, with roles in each (a **membership**), and can have **platform roles** of its own, for the platform outside any tenant. A sign-in is in one place at a time: one tenant, with that membership's roles, or the platform, with the platform roles. Never both, so an operator's platform `admin` role does nothing inside a customer's tenant.

```http
POST /api/dm/auth/login
{ "email": "ann@example.com", "password": "…", "tenant": "42" }
```

```json
{ "tokenType": "Bearer", "accessToken": "CfDJ8…", "expiresIn": 3600, "refreshToken": "CfDJ8…" }
```

Without `tenant`, an account with no platform roles and one membership signs in to that tenant; any other signs in to the platform. `POST /auth/switch { "tenant": "43" }` moves a sign-in to another of the account's tenants. `GET /auth/me` describes the sign-in: its tenant, roles and memberships. `POST /auth/refresh { "refreshToken": … }` gives new tokens in the same place.

Add `?useCookies=true` to `login` for a cookie instead of tokens (`&useSessionCookies=true` for one that ends with the browser).

### Invitations and sign-up

Accounts are made by invitation:

```http
POST /api/dm/access/invitations
{ "email": "bo@example.com", "tenant": "42", "roles": ["member"] }
```

The invitation is emailed (see [Email](#email)). For those who manage access to everything, the response carries its token too, so that they can deliver it their own way; a tenant administrator's invitation reaches the email only, since accepting it makes an account of that address. `POST /auth/invitations/accept { "token": …, "password": … }` makes the account; an existing account accepts while signed in. Tenant administrators invite to their own tenant only, giving only roles they may give.

To let people make their own accounts:

```csharp
a.SignUp.Open = true;                       // anyone, or:
a.SignUp.EmailDomains.Add("example.com");   // these domains only
a.SignUp.Check = (email, http, ct) => …;   // and any check of your own
```

`POST /auth/register { "email": … }` signs an address up: it sends that address an email (`DapperMaticEmailKind.SignUp`, linking to `a.Links.SignUp`) whose token, accepted with a password like an invitation's, makes the account. So nobody chooses the password of an address they do not read. Signing up never tells whether an address already has an account, and sends nothing when it has one.

### Grants: support access

A platform operator who needs to act inside a customer's tenant gets a **grant**: a membership of that tenant that ends, with a reason, made by those who manage access to everything:

```http
POST /api/dm/access/grants
{ "userId": "…", "tenant": "42", "roles": ["member"], "expiresAt": "2026-10-23T18:00:00Z", "reason": "Ticket 1234" }
```

A grant lasts 8 hours at most (`MaxGrantDuration`). Signed in through a grant, the operator has the grant's roles only, and every audit event names the grant (`dm_grant`) and who gave it. Tenant administrators can list and revoke grants to their tenant. A grant and a membership of the same tenant are kept apart: a sign-in uses the membership.

### Sign-ins that end

Each request checks the account (kept for `AccessRefreshInterval`: a change made on another server is seen within it). A sign-in ends, with 401, when:

- its account is turned off or locked out;
- it signed out (`POST /auth/logout`), or the account signed out everywhere (`POST /auth/manage/sign-out-everywhere`);
- its password was reset (by email, which also revokes the account's personal keys), or changed elsewhere;
- it was locked out: wrong passwords count, at sign-in and when changing the password;
- its membership or grant is gone or expired;
- its roles in its place changed: a refresh gives the new ones.

Changing your own password or two-factor sign-in (`POST /auth/manage/info`, `/auth/manage/2fa`) keeps your current sign-in and ends the others. Signing out everywhere also revokes the account's personal keys. A cookie whose account or place is gone is refused by your own endpoints too, not renewed.

### Managing accounts

| Endpoint | What it does | Who |
|----------|--------------|-----|
| `GET /access/accounts?tenant=` | Lists accounts | Tenant administrators: their tenant's members |
| `PUT /access/accounts/{id}/roles` | Sets platform roles | App-wide managers only |
| `POST /access/accounts/{id}/disable`, `/enable` | Turns an account off or on | App-wide managers only |
| `PUT`, `DELETE /access/memberships/{tenant}/{userId}` | Adds or changes, or removes, a membership | Tenant administrators: their tenant |
| `GET`, `POST /access/grants`, `DELETE /access/grants/{id}` | Grants | Making them: app-wide managers only |
| `GET`, `POST /access/invitations`, `DELETE /access/invitations/{id}` | Invitations | Tenant administrators: their tenant |

All need `ManageAccess`; none can be used with an API key.

### Email

Invitations, confirmations and password resets are sent through `IDapperMaticEmailSender`. Register your own:

```csharp
builder.Services.AddSingleton<IDapperMaticEmailSender, SmtpEmails>();

public sealed class SmtpEmails(SmtpClient smtp) : IDapperMaticEmailSender
{
    public Task SendAsync(DapperMaticEmail email, CancellationToken cancellationToken) =>
        smtp.SendMailAsync(new MailMessage("no-reply@example.com", email.To, email.Kind.ToString(), email.Link ?? email.Code), cancellationToken);
}
```

Without one, emails are written to the log, with their links only in Development. Links go to DapperMatic's endpoints unless `a.Links` names your own pages (`a.Links.Invitation = "https://app.example.com/join?token={token}"`).

### Passwords, lockout and two-factor sign-in

Identity's rules apply: change them with `a.Identity = o => { o.Password.RequiredLength = 12; o.Lockout.MaxFailedAccessAttempts = 5; }`. `POST /auth/forgot-password` and `/auth/reset-password` reset a password by an emailed code, which ends every sign-in. `POST /auth/manage/2fa` sets up an authenticator app; once on, `login` needs `twoFactorCode` (or `twoFactorRecoveryCode`).

### Data Protection keys

Tokens, cookies and tickets are protected by ASP.NET Core Data Protection. So that every instance of your application reads the others' tokens, DapperMatic keeps the key ring in the accounts' database, encrypted with `ConnectionStringEncryptionKey` (startup fails without one). These are your application's keys too: anything else it protects uses them. Set `a.KeepDataProtectionKeys = false` if you keep them yourself.

## JWT access tokens

The built-in accounts' access tokens are opaque by default: only DapperMatic reads them. To let your other services check them too, issue JWTs:

```csharp
dm.AddUserAccounts(a => a.UseJwt(j =>
{
    j.Issuer = "https://app.example.com/api/dm/auth";
    j.Audience = "orders-api";
    j.AddKey("2026-10", File.ReadAllText("/run/secrets/jwt-key.pem"));
}));
```

A JWT is signed with RS256 (an RSA key of 2,048 bits or more) or ES256 (a P-256 key); there is no shared-secret option, so a service that checks tokens cannot make them. It lasts 15 minutes (`AccessTokenLifetime`); the refresh token gets another. It carries `iss`, `aud`, `sub` (the account's id), `name`, `email`, `role` (an array), `sid` (the sign-in), `dm_tenant`, `dm_membership` and, through a grant, `dm_grant` and `dm_granted_by`.

The public keys are published as a JSON Web Key Set at `/api/dm/auth/jwks.json`. When the issuer is an absolute URL, a discovery document is published too, at `/api/dm/auth/.well-known/openid-configuration`; it gives `{issuer}/jwks.json` as the keys' address, never one built from the request's host name. So set the issuer to the public URL of DapperMatic's `auth` path, and another ASP.NET Core service checks the tokens like any other:

```csharp
builder.Services.AddAuthentication("Bearer").AddJwtBearer("Bearer", o =>
{
    o.Authority = "https://app.example.com/api/dm/auth";
    o.Audience = "orders-api";
    o.MapInboundClaims = false;
    o.TokenValidationParameters.RoleClaimType = "role";
});
```

To change keys, add the new key first and keep the old one (its public part is enough) until the tokens it signed have expired; the first key signs, every listed key is accepted and published.

Inside DapperMatic a JWT is checked like any of the accounts' tokens: its signature, issuer and audience, and then, at each request, its sign-in, membership and roles. Another service sees only the token, so a signed-out sign-in's JWT is good there until it expires: keep `AccessTokenLifetime` short.
