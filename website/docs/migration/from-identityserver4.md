---
sidebar_position: 1
---

# Migrating from IdentityServer4

GPHosting.Identity is a rename-and-modernize fork of the archived
[IdentityServer4](https://github.com/IdentityServer/IdentityServer4) project (the `archive`
branch, last active on .NET Core 3.1) — same protocol implementation and model shapes, upgraded to
.NET 10 / C# 13. If you have an existing IdentityServer4 deployment, most of your configuration
code carries over with a namespace and package rename; the sections below cover what doesn't.

## Package names

| IdentityServer4 | GPHosting.Identity |
|---|---|
| `IdentityServer4` | `GPHosting.Identity` |
| `IdentityServer4.Storage` | `GPHosting.Identity.Storage` |
| `IdentityServer4.EntityFramework` | `GPHosting.Identity.EntityFramework` |
| `IdentityServer4.EntityFramework.Storage` | `GPHosting.Identity.EntityFramework.Storage` |
| `IdentityServer4.AspNetIdentity` | `GPHosting.Identity.AspNetIdentity` |

## Namespaces

Every `IdentityServer4.*` namespace becomes `GPHosting.Identity.*` — `IdentityServer4.Models`
becomes `GPHosting.Identity.Models`, `IdentityServer4.Stores` becomes `GPHosting.Identity.Stores`,
and so on. A global find-and-replace of `IdentityServer4` → `GPHosting.Identity` across your
`using` statements handles the overwhelming majority of this; the model classes (`Client`,
`ApiResource`, `ApiScope`, `IdentityResource`, `Secret`, etc.) and their properties are otherwise
unchanged.

```diff
- using IdentityServer4.Models;
+ using GPHosting.Identity.Models;
```

## Target framework

GPHosting.Identity targets **.NET 10** only — there's no `netstandard2.0` or multi-targeting for
older runtimes. If you're on .NET Core 3.1 (where IdentityServer4 was archived) or an intermediate
LTS (.NET 6/8), you'll need to upgrade your host application to .NET 10 as part of this migration,
not just swap the package reference. There is no supported side-by-side or gradual path — plan for
a single coordinated upgrade of your identity server host.

## `AddDeveloperSigningCredential()` / `AddSigningCredential()`

Unchanged in shape and behavior — see [Signing Keys](/docs/fundamentals/signing-keys) if you want
to revisit key management while you're already touching this code, but no migration action is
required here.

## `Newtonsoft.Json` removed

IdentityServer4 depended on `Newtonsoft.Json` in places; GPHosting.Identity uses
`System.Text.Json` throughout and has no `Newtonsoft.Json` dependency at all. If your host
application has custom serialization hooks, profile services, or extension grant validators that
assumed `Newtonsoft.Json` types (e.g. `JObject` claims manipulation), those need to be ported to
`System.Text.Json` (`JsonNode`/`JsonDocument`) equivalents.

## `ISystemClock` replaced with `TimeProvider`

Any custom code that took a dependency on ASP.NET Core's `ISystemClock` (common in tests that
needed to control "now") should switch to the standard .NET `TimeProvider` abstraction instead —
GPHosting.Identity's own services were migrated to `TimeProvider` internally, and `ISystemClock` is
no longer wired into the container.

## New since IdentityServer4 (not required, worth knowing about)

These didn't exist in IdentityServer4 and require no migration action, but are worth being aware
of since they may replace custom code you built to work around their absence:

- **Pushed Authorization Requests (RFC 9126), DPoP (RFC 9449), JARM** — modern OAuth extensions
  IdentityServer4 never shipped.
- **Built-in observability** — `System.Diagnostics` tracing/metrics and ASP.NET Core health
  checks, see [Observability](/docs/observability/opentelemetry). If you built your own logging
  around token issuance for monitoring purposes, this may now cover it natively.
- **Multi-database migrations** — PostgreSQL and MySQL migrations ship alongside the SQL Server
  and SQLite ones IdentityServer4 had, see [Stores](/docs/fundamentals/stores).

## What to test after migrating

- Existing persisted grants (refresh tokens, device codes) in your database are read through the
  same EF Core model shapes and table names by default — a straight package swap should read your
  existing operational store without a data migration, but verify against a copy of production
  data before cutting over, especially if you've customized table names via
  `ConfigurationStoreOptions`/`OperationalStoreOptions`.
- Any custom `IProfileService`, `IExtensionGrantValidator`, or custom validators you've written
  against IdentityServer4 interfaces — the interfaces are unchanged in shape, but recompiling
  against the new namespaces will surface anything relying on `Newtonsoft.Json` or `ISystemClock`
  as described above.
- Signing key file locations and formats are unchanged — `tempkey.jwk` behavior, certificate
  loading, etc. all carry over as-is.

## Next steps

- [Installation](/docs/getting-started/installation) — package references for a fresh setup
- [Stores](/docs/fundamentals/stores) — if you're also taking the opportunity to move databases
