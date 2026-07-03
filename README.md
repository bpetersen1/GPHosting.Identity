# GPHosting.Identity

A community-maintained upgrade of [IdentityServer4](https://github.com/IdentityServer/IdentityServer4) targeting **.NET 10 / C# 13**, published as a drop-in NuGet library by [GP Hosting](https://github.com/bpetersen1).

GPHosting.Identity is an [OpenID Connect](http://openid.net/connect/) and [OAuth 2.0](https://tools.ietf.org/html/rfc6749) framework for ASP.NET Core. It is licensed under [Apache 2.0](https://opensource.org/licenses/Apache-2.0).

---

## Security

Security is a first-class concern in GPHosting.Identity. Every default is set to the most secure option, and all known IdentityServer4 vulnerabilities have been addressed.

**CVE-2024-39694 (Open Redirect in end session) — Fixed**

The original IdentityServer4 did not validate `post_logout_redirect_uri` when no `id_token_hint` was present, allowing attackers to redirect users to arbitrary external URLs after logout. GPHosting.Identity rejects `post_logout_redirect_uri` without a valid `id_token_hint` and logs a warning for operator visibility.

**CVE-2022-24306 (Redirect URI bypass) — Fixed**

The original validator compared redirect URIs as plain strings using case-insensitive comparison, which could be exploited with path-encoding or case tricks to bypass the allowlist. GPHosting.Identity parses both URIs and compares components individually: scheme and host are compared case-insensitively (per RFC 3986), while path and query are compared with exact ordinal equality.

**Secure defaults out of the box**

| Setting | Old default | New default | Why |
|---|---|---|---|
| `AccessTokenLifetime` | 3600 s (1 hour) | 900 s (15 min) | Limits exposure window if a token is leaked |
| `AbsoluteRefreshTokenLifetime` | 2592000 s (30 days) | 86400 s (24 hours) | Reduces refresh token theft window |
| `SlidingRefreshTokenLifetime` | 1296000 s (15 days) | 86400 s (24 hours) | Consistent with absolute lifetime |
| `RequirePkce` | `true` | `true` | PKCE enforced for all code flow clients |
| `AllowPlainTextPkce` | `false` | `false` | Plain PKCE method (S256 only) |
| `RefreshTokenUsage` | `OneTimeOnly` | `OneTimeOnly` | Rotation on every use |
| Secret storage | `HashedSharedSecretValidator` (SHA-256/512) | same | Plain-text validator is `[Obsolete]` and not registered |

**Response headers hardened**

- `X-Frame-Options: DENY` on all consent and form-post responses (clickjacking protection)
- `Content-Security-Policy` with script hash on all framework-rendered HTML
- `Referrer-Policy: no-referrer` on authorize responses

To report a security issue in GPHosting.Identity, please open a [GitHub Security Advisory](https://github.com/bpetersen1/GPHosting.Identity/security/advisories/new).

---

## Modern .NET 10 foundations

GPHosting.Identity is built from the ground up for modern .NET — not a shim on top of legacy infrastructure.

**No Newtonsoft.Json dependency**

The original IdentityServer4 pulled in Newtonsoft.Json for all JSON serialization. GPHosting.Identity uses `System.Text.Json` exclusively, which ships as part of .NET 10 itself. This means:

- **Zero extra dependencies** — nothing extra lands in your package graph
- **Better performance** — System.Text.Json is faster and allocates less memory than Newtonsoft
- **AOT & trim compatible** — Newtonsoft.Json is explicitly incompatible with .NET Native AOT; System.Text.Json supports source-generated serialization, enabling fully ahead-of-time compiled deployments (see below)
- **Smaller NuGet footprint** — one less transitive dependency for your consumers

**C# 13 language features throughout**

All source code uses modern C# 13 idioms: file-scoped namespaces, `ArgumentNullException.ThrowIfNull()`, collection expressions, and pattern matching — making the codebase easier to read, contribute to, and maintain.

**Performance & AOT ready**

- `ISystemClock` fully replaced with `TimeProvider` across all library source — no obsolete APIs remain
- Hot paths optimised: claim lookups in token models use `foreach` instead of LINQ chains, scope string parsing uses `ReadOnlySpan<char>` to avoid intermediate array allocations
- `IsTrimmable` and `EnableTrimAnalyzer` enabled on both core packages — publish with `PublishTrimmed=true` safely
- BenchmarkDotNet baseline project included at `src/Benchmarks/` for scope parsing and claim lookup, so future changes have measurable performance regression detection

**Security proven by tests**

Every security fix ships with tests that prove attacks fail — not just that happy paths work:

- PKCE enforcement: public clients are rejected without a code challenge; `plain` method is blocked by default; S256 is the only accepted method
- Redirect URI validation: path-case bypass, path traversal, query injection, scheme downgrade, and wildcard patterns are all verified to fail
- Secret hashing: SHA-256 and SHA-512 stored secrets validate correctly; plain-text stored values are rejected; timing-safe comparison is verified
- Over 1,100 tests across unit, integration, and EF Core test suites — 0 failures

**Modern OAuth 2.0 standards**

GPHosting.Identity implements current-generation OAuth 2.0 specifications that IdentityServer4 never supported:

- **Pushed Authorization Requests (PAR — RFC 9126)**: Clients push authorization parameters to the server before redirecting the user, receiving a `request_uri` token. This prevents parameter tampering in the browser, is required for FAPI 2.0, and is fully wired end-to-end: `POST /connect/par` stores the request, and the authorize endpoint resolves `request_uri` references, enforces one-time use, validates expiry, and rejects client_id mismatches. Per-client `RequirePushedAuthorization` flag enforces PAR for all requests from that client
- **DPoP (RFC 9449)**: Demonstration of Proof-of-Possession is fully wired end-to-end. When a client sends a `DPoP` header on a token request, the proof JWT is validated (header type, embedded public key, `htm`/`htu` binding, `iat` replay window, `jti` presence, optional `exp`). On success the access token receives a `cnf` claim containing the JWK thumbprint and the response returns `token_type: DPoP` instead of `Bearer`, binding the token to the client's key pair
- **Rich Authorization Requests groundwork (RFC 9396)**: Clients can declare allowed `authorization_details` types for fine-grained resource authorization
- **JARM response modes (not yet enforced)**: `query.jwt`, `fragment.jwt`, and `form_post.jwt` response mode constants exist for JWT-secured authorization responses, but `AuthorizeRequestValidator` doesn't accept them yet — requesting one currently fails with `unsupported_response_type`. Groundwork only.
- **FAPI 2.0 profile flag (not yet enforced)**: Per-client `RequireFapi2` is a persisted column (including in every EF migration), but nothing in the validation pipeline reads it yet. Setting it has no effect today.

**Observability**

GPHosting.Identity emits OpenTelemetry-compatible tracing and metrics, using only BCL types (`System.Diagnostics.Activity` / `System.Diagnostics.Metrics`) — the core library takes **no dependency on the OpenTelemetry SDK**. Everything described here is **opt-in**: a host that does nothing gets no behavior change, no new packages, and no `/health` endpoints. There is no single `UseTelemetry()`/`AddObservability()` switch — it's three independent steps you wire yourself.

- **Distributed tracing**: `GPHosting.Identity.Telemetry.IdentityServerActivitySource` (source name `GPHosting.Identity`) emits spans for the token endpoint and authorize endpoint, tagged with `identityserver.client_id`, `identityserver.grant_type`, and `identityserver.error`
- **Metrics**: `GPHosting.Identity.Telemetry.IdentityServerMetrics` (meter name `GPHosting.Identity`) exposes `identityserver.tokens.issued`, `identityserver.token_requests.errors`, and `identityserver.authorize_requests` counters, tagged by client, grant type, and outcome
- **Health checks**: `builder.AddIdentityServerHealthChecks()` registers a signing-credential availability check. The EF Core package adds `builder.AddConfigurationStoreHealthCheck()` and `builder.AddOperationalStoreHealthCheck()` for database connectivity, tagged `ready` so they can be filtered separately from liveness checks

To actually enable tracing/metrics export and expose health check endpoints, a host wires them up like this (see `src/GPHosting.Identity/host/Startup.cs` for the full working example, including console and OTLP exporters):

```csharp
// 1. Register health checks (on the IIdentityServerBuilder returned by AddIdentityServer)
builder.AddIdentityServerHealthChecks();
// if using EF Core storage:
// builder.AddConfigurationStoreHealthCheck();
// builder.AddOperationalStoreHealthCheck();

// 2. Wire the ActivitySource/Meter into an OpenTelemetry pipeline (requires the OpenTelemetry SDK packages)
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(IdentityServerActivitySource.Name)
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(IdentityServerMetrics.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());

// 3. Map the health check endpoints (in Configure/UseEndpoints — plain ASP.NET Core, not library-provided)
endpoints.MapHealthChecks("/health/live");
endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

---

## NuGet Packages

| Package | Description |
|---|---|
| `GPHosting.Identity` | Core OpenID Connect / OAuth 2.0 framework |
| `GPHosting.Identity.Storage` | Storage interfaces and models |
| `GPHosting.Identity.EntityFramework.Storage` | EF Core persistence layer |
| `GPHosting.Identity.EntityFramework` | EF Core integration |
| `GPHosting.Identity.AspNetIdentity` | ASP.NET Core Identity integration |

Install via NuGet:

```shell
dotnet add package GPHosting.Identity
```

---

## Requirements

- .NET 10 SDK or later
- ASP.NET Core 10

---

## Database Providers

GPHosting.Identity ships migration projects for all major databases.

| Provider | Package | Migration project |
|---|---|---|
| SQL Server | `Microsoft.EntityFrameworkCore.SqlServer` | `migrations/SqlServer` |
| PostgreSQL | `Npgsql.EntityFrameworkCore.PostgreSQL` | `migrations/PostgreSQL` |
| SQLite | `Microsoft.EntityFrameworkCore.Sqlite` | `migrations/SQLite` |
| MySQL | `MySql.EntityFrameworkCore` (Oracle) | `migrations/MySQL` |

### Applying migrations

Each project under `src/EntityFramework.Storage/migrations/<Provider>/` is a standalone EF Core migration host. Edit its `appsettings.json` to point at your database, then run:

```shell
dotnet ef database update --project src/EntityFramework.Storage/migrations/<Provider>/<Provider>.csproj --context ConfigurationDbContext
dotnet ef database update --project src/EntityFramework.Storage/migrations/<Provider>/<Provider>.csproj --context PersistedGrantDbContext
```

### Wiring up your app

Pass a `ConfigureDbContext` action when registering GPHosting.Identity. The method name differs per provider:

**SQL Server**
```csharp
builder.Services.AddIdentityServer()
    .AddConfigurationStore(o => o.ConfigureDbContext = b =>
        b.UseSqlServer(connectionString))
    .AddOperationalStore(o => o.ConfigureDbContext = b =>
        b.UseSqlServer(connectionString));
```

**PostgreSQL**
```csharp
// dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
builder.Services.AddIdentityServer()
    .AddConfigurationStore(o => o.ConfigureDbContext = b =>
        b.UseNpgsql(connectionString))
    .AddOperationalStore(o => o.ConfigureDbContext = b =>
        b.UseNpgsql(connectionString));
```

**SQLite**
```csharp
// dotnet add package Microsoft.EntityFrameworkCore.Sqlite
builder.Services.AddIdentityServer()
    .AddConfigurationStore(o => o.ConfigureDbContext = b =>
        b.UseSqlite(connectionString))
    .AddOperationalStore(o => o.ConfigureDbContext = b =>
        b.UseSqlite(connectionString));
```

**MySQL**
```csharp
// dotnet add package MySql.EntityFrameworkCore
// Note: the method is UseMySQL (capital SQL) — this is Oracle's driver, not Pomelo.
builder.Services.AddIdentityServer()
    .AddConfigurationStore(o => o.ConfigureDbContext = b =>
        b.UseMySQL(connectionString))
    .AddOperationalStore(o => o.ConfigureDbContext = b =>
        b.UseMySQL(connectionString));
```

### MySQL — Pomelo vs Oracle driver

**If you have used `Pomelo.EntityFrameworkCore.MySql` before**, note that the method name is different: Oracle uses `UseMySQL` (capital SQL), while Pomelo uses `UseMySql`. Do **not** install Pomelo alongside this library — Pomelo 9.x targets EF Core 9 and will cause a version conflict with GPHosting.Identity's EF Core 10 dependency. Once `Pomelo.EntityFrameworkCore.MySql` 10.x is released, you can switch by:

1. Replacing `MySql.EntityFrameworkCore` with `Pomelo.EntityFrameworkCore.MySql` in your project.
2. Updating the registration to use Pomelo's API:
   ```csharp
   b.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
   ```
3. Regenerating migrations from `src/EntityFramework.Storage/migrations/MySQL/` (update the driver reference there too).

### SQLite — production warning

> The `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 transitive dependency has a known native-library CVE with no available upgrade at this time. SQLite is not recommended for production identity server deployments — use it for development and testing only.

---

## How to Build

```shell
git clone https://github.com/bpetersen1/GPHosting.Identity.git
cd GPHosting.Identity

# Build
dotnet build src/GPHosting.Identity/test/IdentityServer.UnitTests -c Release

# Test
dotnet test src/GPHosting.Identity/test/IdentityServer.UnitTests -c Release
dotnet test src/GPHosting.Identity/test/IdentityServer.IntegrationTests -c Release
dotnet test src/EntityFramework.Storage/test/UnitTests -c Release
dotnet test src/EntityFramework.Storage/test/IntegrationTests -c Release
dotnet test src/EntityFramework/test/GPHosting.Identity.EntityFramework.Tests -c Release
```

---

## CI / CD

| Trigger | Action |
|---|---|
| Push to `main` or pull request | Build + test all projects |
| Push tag `v*.*.*` | Build + test + pack + publish to NuGet.org |

---

## Acknowledgements

GPHosting.Identity is built on the foundation of IdentityServer4, originally created by [Dominick Baier](https://twitter.com/leastprivilege) and [Brock Allen](https://twitter.com/brocklallen).

This upgrade uses the following open source projects:

- [ASP.NET Core](https://github.com/dotnet/aspnetcore)
- [Entity Framework Core](https://github.com/dotnet/efcore)
- [IdentityModel](https://github.com/IdentityModel/IdentityModel)
- [XUnit](https://xunit.net/)
- [Fluent Assertions](https://fluentassertions.com/)
- [AutoMapper](https://automapper.org/)
