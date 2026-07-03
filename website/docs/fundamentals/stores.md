---
sidebar_position: 4
---

# Stores

The [Quickstart](/docs/getting-started/quickstart) used `AddInMemoryClients()` and
`AddInMemoryApiScopes()` — a fixed list baked into `Program.cs`. That's fine for exploring the
library, but it means adding a client requires a redeploy, and it doesn't scale past a handful of
entries. For any real deployment you'll want clients, resources, and issued tokens backed by a
database instead.

`GPHosting.Identity.EntityFramework` provides two independent EF Core-backed stores:

- **Configuration store** — clients, identity resources, API resources, and API scopes.
- **Operational store** — persisted grants (refresh tokens, authorization codes, device codes,
  consent) and device flow codes.

They're independent on purpose: some deployments want clients in a database but are fine with an
in-memory operational store (e.g. single-instance dev/test setups), or vice versa. Most production
deployments enable both.

## Installing

```shell
dotnet add package GPHosting.Identity.EntityFramework
```

This pulls in `GPHosting.Identity.EntityFramework.Storage` transitively — you don't need to
reference it directly unless you're building a custom store on top of its `DbContext`s.

You'll also need an EF Core database provider for whichever database you're targeting:

| Database | Package |
|---|---|
| SQLite | `Microsoft.EntityFrameworkCore.Sqlite` |
| PostgreSQL | `Npgsql.EntityFrameworkCore.PostgreSQL` |
| MySQL | `MySql.EntityFrameworkCore` |
| SQL Server | `Microsoft.EntityFrameworkCore.SqlServer` |

SQLite is the easiest to get running locally (no server to install); Postgres, MySQL, and SQL
Server are all supported for production with first-class migrations shipped in the repo (see
[Migrations](#migrations) below).

## Wiring it up

```csharp
using System.Reflection;

var migrationsAssembly = typeof(Program).Assembly.GetName().Name;
var connectionString = builder.Configuration.GetConnectionString("IdentityServer");

builder.Services.AddIdentityServer()
    .AddDeveloperSigningCredential() // replace before deploying — see Signing Keys
    .AddConfigurationStore(options =>
    {
        options.ConfigureDbContext = b => b.UseNpgsql(connectionString,
            sql => sql.MigrationsAssembly(migrationsAssembly));
    })
    .AddOperationalStore(options =>
    {
        options.ConfigureDbContext = b => b.UseNpgsql(connectionString,
            sql => sql.MigrationsAssembly(migrationsAssembly));
        options.EnableTokenCleanup = true;
    });
```

Swap `UseNpgsql` for `UseSqlite`, `UseMySQL`, or `UseSqlServer` depending on your provider — the
rest of the shape stays the same. `MigrationsAssembly(...)` tells EF Core to look for migrations
in your own project rather than the `GPHosting.Identity.EntityFramework.Storage` assembly, since
you can't run migrations against a referenced library's assembly directly.

Once wired up, replace `AddInMemoryClients()` / `AddInMemoryApiScopes()` from the quickstart —
clients and scopes now come from the database via `IClientStore` / `IResourceStore`, seeded
however you like (a startup seeding routine, an admin UI, direct SQL — GPHosting.Identity doesn't
prescribe one).

## `EnableTokenCleanup`

Persisted grants (refresh tokens, used authorization codes, expired device codes) accumulate in
the operational store over time. `EnableTokenCleanup = true` registers a hosted background service
(`TokenCleanupHost`) that periodically deletes expired entries:

```csharp
options.EnableTokenCleanup = true;
options.TokenCleanupInterval = 3600;   // seconds between sweeps (default: 1 hour)
options.TokenCleanupBatchSize = 100;   // rows deleted per batch (default: 100)
```

Leave this on for any production deployment — without it, the persisted grants table grows
unbounded.

## Migrations

Each supported provider has its own pre-built migrations project under
`src/EntityFramework.Storage/migrations/` (for the configuration + operational stores) and
`src/EntityFramework/migrations/` (for the same, packaged alongside the higher-level stores) —
one folder per provider (`SQLite/`, `PostgreSQL/`, `MySQL/`, `SqlServer/`), each a small EF Core
design-time project referencing the actual storage assembly. This mirrors the original
IdentityServer4 layout: migrations are provider-specific because the SQL EF Core generates differs
enough between providers (data types, index syntax) that a single migration set can't target all
of them.

To apply migrations against your own database, either:

- **Reference the pre-built migrations project's output** if you're not customizing the schema, or
- **Generate your own migrations** against your `DbContext` once you've called `ConfigureDbContext`
  with your provider and connection string:

```shell
dotnet ef migrations add InitialCreate \
  --context ConfigurationDbContext \
  --project YourProject.csproj

dotnet ef migrations add InitialCreate \
  --context PersistedGrantDbContext \
  --project YourProject.csproj
```

Run migrations at startup for local dev, or via a separate migration step in your deployment
pipeline for production (running `context.Database.Migrate()` on every instance's startup is
usually undesirable once you have more than one instance, since they'd race to apply the same
migration):

```csharp
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>().Database.Migrate();
    scope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.Migrate();
}
```

## Table names and schema

Every table name is configurable via `ConfigurationStoreOptions` / `OperationalStoreOptions`, and
both support an optional `DefaultSchema`:

```csharp
options.DefaultSchema = "identity";
options.Client = new TableConfiguration("Clients"); // override individual table names if needed
```

Useful if you're sharing a database with other applications and want GPHosting.Identity's tables
namespaced or renamed to fit an existing convention.

## Caching

Configuration store reads (client lookups, scope lookups) happen on the hot path of every token
request. `AddConfigurationStoreCache()` adds an in-memory caching decorator in front of
`IClientStore`, `IResourceStore`, and `ICorsPolicyService`:

```csharp
builder.Services.AddIdentityServer()
    .AddConfigurationStore(...)
    .AddConfigurationStoreCache();
```

This is a simple in-memory cache with no built-in invalidation on write — if you change a client
through an admin UI while the server is running, the cache won't reflect it until the entry
expires. Fine for configuration that changes rarely; skip it if clients/scopes change frequently
at runtime.

## Next steps

- [Architecture](/docs/fundamentals/architecture) — how clients, resources, and grants relate to
  each other conceptually
- [Signing Keys](/docs/fundamentals/signing-keys) — the other piece of moving from local dev to
  production
- [Observability](/docs/observability/opentelemetry) — health checks for both stores
  (`AddConfigurationStoreHealthCheck()` / `AddOperationalStoreHealthCheck()`)
