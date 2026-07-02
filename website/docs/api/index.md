---
sidebar_position: 1
---

# API Reference

The full C# API reference — every public class, interface, method, and property across all
five GPHosting.Identity packages — is generated automatically from XML doc comments in the
source code using [DocFX](https://dotnet.github.io/docfx/).

**[Browse the API Reference →](/api-reference/)**

This reference is kept in sync with the source on every release, so it always reflects what's
actually shipped in the published NuGet packages — not what the guides say it *should* do.

## Where to find things

- **`GPHosting.Identity`** — core namespace: `IIdentityServerBuilder`, client/resource models,
  validators, and response generators
- **`GPHosting.Identity.Telemetry`** — `IdentityServerActivitySource`, `IdentityServerMetrics`
- **`GPHosting.Identity.EntityFramework`** — `AddConfigurationStore()`, `AddOperationalStore()`,
  and the EF Core store implementations
- **`GPHosting.Identity.AspNetIdentity`** — ASP.NET Core Identity integration

If you're looking for narrative guides and tutorials instead of raw API surface, start at
[Getting Started](/docs/getting-started/installation) instead.
