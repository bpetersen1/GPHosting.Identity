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

**Security proven by tests**

Every security fix ships with tests that prove attacks fail — not just that happy paths work:

- PKCE enforcement: public clients are rejected without a code challenge; `plain` method is blocked by default; S256 is the only accepted method
- Redirect URI validation: path-case bypass, path traversal, query injection, scheme downgrade, and wildcard patterns are all verified to fail
- Secret hashing: SHA-256 and SHA-512 stored secrets validate correctly; plain-text stored values are rejected; timing-safe comparison is verified
- Over 1,100 tests across unit, integration, and EF Core test suites — 0 failures

**Modern OAuth 2.0 standards**

GPHosting.Identity implements current-generation OAuth 2.0 specifications that IdentityServer4 never supported:

- **Pushed Authorization Requests (PAR — RFC 9126)**: Clients push authorization parameters to the server before redirecting the user, receiving a `request_uri` token. This prevents parameter tampering in the browser, is required for FAPI 2.0, and is fully wired end-to-end: `POST /connect/par` stores the request, and the authorize endpoint resolves `request_uri` references, enforces one-time use, validates expiry, and rejects client_id mismatches. Per-client `RequirePushedAuthorization` flag enforces PAR for all requests from that client
- **DPoP proof validation (RFC 9449)**: Demonstration of Proof-of-Possession binds access tokens to a client's public key, preventing stolen token replay. The `IDPoPProofValidator` verifies the JWT header type, embedded public key, HTTP method/URL binding, and `iat` replay window
- **Rich Authorization Requests groundwork (RFC 9396)**: Clients can declare allowed `authorization_details` types for fine-grained resource authorization
- **JARM response modes**: `query.jwt`, `fragment.jwt`, and `form_post.jwt` response mode constants for JWT-secured authorization responses
- **FAPI 2.0 profile flag**: Per-client `RequireFapi2` and `RequirePushedAuthorization` flags for financial-grade API enforcement

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
