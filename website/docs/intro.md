---
sidebar_position: 1
slug: /
---

# What is GPHosting.Identity?

**GPHosting.Identity** is an OpenID Connect and OAuth 2.0 framework for ASP.NET Core, built on
.NET 10 and C# 13. It's a community-maintained upgrade of the archived
[IdentityServer4](https://github.com/IdentityServer/IdentityServer4) project — same proven
protocol implementation, modernized for the current .NET platform and actively maintained.

Use it to add a full OpenID Connect / OAuth 2.0 token server to your own application:
login, consent, token issuance, and everything needed to secure APIs and SPAs behind
industry-standard flows.

## Why GPHosting.Identity?

- **.NET 10 / C# 13 native** — no legacy shims, no `netstandard2.0` compatibility baggage.
  Trim-and-AOT-ready, `System.Text.Json` throughout (no Newtonsoft.Json dependency).
- **Modern OAuth standards** — Pushed Authorization Requests (RFC 9126) and DPoP (RFC 9449),
  fully enforced end-to-end — protocol features IdentityServer4 never shipped. (JARM response
  modes and a FAPI 2.0 profile flag exist in the data model as groundwork for future work, but
  aren't enforced yet — see [PAR & DPoP](/docs/protocols/par-and-dpop).)
- **Security-tested, not just security-reviewed** — PKCE enforcement, redirect URI validation,
  and secret hashing all ship with tests that prove the attack actually fails, not just that
  the happy path works.
- **Observable by default** — built-in `System.Diagnostics.Activity` tracing and
  `System.Diagnostics.Metrics` counters, with zero required dependency on any specific
  telemetry vendor. See [Observability](/docs/observability/opentelemetry).
- **Free and open source** — Apache 2.0 licensed, no commercial tier, no revenue threshold.

## Where to start

- New to the project? Head to [Installation](/docs/getting-started/installation) and
  [Quickstart](/docs/getting-started/quickstart) — a working token server in about 15 lines.
- Migrating from IdentityServer4? See the
  [migration guide](/docs/migration/from-identityserver4).
- Looking for the raw API surface? See the [API Reference](/docs/api).

## Packages

:::note
GPHosting.Identity has not been published to nuget.org yet — packaging and release is in
progress. Once published, install with `dotnet add package <name>` as shown below. Until then,
reference the project directly or watch the
[GitHub repo](https://github.com/bpetersen1/GPHosting.Identity) for release announcements.
:::

| Package | Purpose |
|---|---|
| `GPHosting.Identity` | Core OpenID Connect / OAuth 2.0 framework |
| `GPHosting.Identity.Storage` | Storage interfaces and models |
| `GPHosting.Identity.EntityFramework.Storage` | EF Core persistence layer |
| `GPHosting.Identity.EntityFramework` | EF Core integration (client/resource/grant stores) |
| `GPHosting.Identity.AspNetIdentity` | ASP.NET Core Identity integration |

## License

GPHosting.Identity is licensed under [Apache 2.0](https://github.com/bpetersen1/GPHosting.Identity/blob/main/LICENSE).
Original copyright headers from IdentityServer4 are retained per the license terms; new and
modified code carries GP Hosting copyright.
