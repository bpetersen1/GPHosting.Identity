---
sidebar_position: 1
---

# Samples

The repo ships complete, runnable sample projects rather than isolated snippets — clone the repo
and run any of these directly to see a full client/server pair working together.

## Quickstarts (`samples/Quickstarts/`)

Numbered end-to-end walkthroughs, each building on the last:

| Sample | Demonstrates |
|---|---|
| `1_ClientCredentials` | Machine-to-machine, no user — the simplest possible setup |
| `2_InteractiveAspNetCore` | A full interactive login: IdentityServer + an MVC client + a protected API, authorization code + PKCE |
| `3_AspNetCoreAndApis` | Calling multiple protected APIs from an interactive client |
| `4_JavaScriptClient` | A browser-based (JS) client using the code flow |
| `5_EntityFramework` | Switching client/resource configuration to the EF Core stores (see [Stores](/docs/fundamentals/stores)) |
| `6_AspNetIdentity` | Using ASP.NET Core Identity as the user store instead of the in-memory test users |

`2_InteractiveAspNetCore` is the one referenced from
[Adding your first client](/docs/getting-started/first-client) — it's the fastest way to see a
`GrantTypes.Code` + PKCE client working end-to-end without wiring one up from scratch.

## Individual client samples (`samples/Clients/src/`)

Smaller, single-purpose client projects covering specific grant types and authentication methods:
console apps for client credentials, device flow, resource owner password, mTLS, private key JWT,
parameterized scopes; an MVC app (`MvcCode`) for the standard interactive code+PKCE flow; and
`MvcAutomaticTokenManagement` for automatic access/refresh token handling.

## Key management (`samples/KeyManagement/`)

Demonstrates loading signing keys from the filesystem vs. a database-backed key store — a more
detailed companion to [Signing Keys](/docs/fundamentals/signing-keys) if you're building your own
key rotation/storage strategy rather than using a certificate file directly.

## Running a sample

Each sample is a standalone solution — `cd` into its directory and `dotnet run` the IdentityServer
project first, then the client/API projects. Check each sample's own `README` (where present) or
`Program.cs`/`Startup.cs` for the exact ports and client configuration it expects.

## Next steps

- [Adding your first client](/docs/getting-started/first-client) — the concepts these samples
  demonstrate in practice
- [Flows](/docs/fundamentals/flows) — which grant type to reach for outside of these samples
