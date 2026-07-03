---
sidebar_position: 3
---

# Flows

`AllowedGrantTypes` on a client determines which of these it can use. Picking the right one is
mostly about answering one question: **is a real user involved, and can the client keep a secret?**

## Authorization code (`GrantTypes.Code`)

The flow for any client where a user signs in through a browser — server-rendered web apps, SPAs,
mobile apps. Covered step-by-step in [Adding your first client](/docs/getting-started/first-client).

```csharp
new Client
{
    ClientId = "web-app",
    AllowedGrantTypes = GrantTypes.Code,
    RequirePkce = true, // enforced regardless; see below
    RedirectUris = { "https://localhost:7001/signin-oidc" },
    AllowedScopes = { "openid", "profile", "orders.read" }
}
```

**PKCE is mandatory for every authorization code client**, whether or not you set
`RequirePkce = true` — GPHosting.Identity rejects code flow requests without a valid
`code_challenge`. This closes the authorization code interception attack that made PKCE mandatory
in OAuth 2.1: without it, a malicious app on the same device (or a network attacker on a
non-HTTPS redirect) that intercepts the authorization code could redeem it for tokens itself.

Confidential clients (server-rendered apps that can hold a `ClientSecret` server-side) get PKCE
*and* client authentication at the token exchange. Public clients (SPAs, mobile apps — anything
where the "secret" would ship in client-side code or an APK) rely on PKCE alone; don't configure a
`ClientSecret` for these, since a secret embedded in a public client isn't actually secret.

## Client credentials (`GrantTypes.ClientCredentials`)

Machine-to-machine — no user, no browser. A service authenticates as itself and gets an access
token scoped to whatever API it needs to call. Used in the [Quickstart](/docs/getting-started/quickstart).

```csharp
new Client
{
    ClientId = "worker-service",
    ClientSecrets = { new Secret("secret".Sha256()) },
    AllowedGrantTypes = GrantTypes.ClientCredentials,
    AllowedScopes = { "orders.write" }
}
```

Never mix this with `GrantTypes.Code` on the same client — a client that can both authenticate as
itself *and* on behalf of a user is harder to reason about and rarely what you actually want.
`GrantTypes.CodeAndClientCredentials` exists for the (uncommon) case where one client genuinely
needs both.

## Device flow (`GrantTypes.DeviceFlow`)

For input-constrained devices — smart TVs, CLI tools, IoT — where there's no convenient browser
on the device itself. The device displays a code and a URL; the user opens that URL on their
phone or laptop, enters the code, and approves. The device polls `/connect/token` until the user
finishes.

```csharp
new Client
{
    ClientId = "cli-tool",
    AllowedGrantTypes = GrantTypes.DeviceFlow,
    AllowedScopes = { "openid", "profile", "orders.read" }
}
```

Requires the operational store (see [Stores](./stores)) to track the device code while the user
completes the flow out-of-band.

## Refresh tokens

Not a grant type on its own — an add-on to authorization code and device flow clients that need
long-lived access without keeping the user signed in indefinitely:

```csharp
new Client
{
    AllowedGrantTypes = GrantTypes.Code,
    AllowOfflineAccess = true, // enables the "offline_access" scope → issues a refresh token
    RefreshTokenUsage = TokenUsage.OneTimeOnly, // rotate on every use (recommended)
    RefreshTokenExpiration = TokenExpiration.Sliding,
    SlidingRefreshTokenLifetime = 1_296_000 // 15 days
}
```

`TokenUsage.OneTimeOnly` rotates the refresh token on every use — the old one is invalidated and a
new one issued. This means a stolen (but not-yet-used) refresh token becomes detectable: if both
the legitimate client and an attacker try to use the same token, the second one to arrive fails,
which is a strong signal something is wrong. Prefer this over `ReUse` unless you have a specific
reason not to.

## What's not covered here

Implicit and hybrid grants (`GrantTypes.Implicit`, `GrantTypes.Hybrid`) still exist for backward
compatibility with older clients but are not recommended for anything new — OAuth 2.1 drops
implicit entirely in favor of authorization code + PKCE, which works for SPAs too via CORS and is
strictly safer (tokens never appear in a URL fragment).

For the advanced OAuth extensions GPHosting.Identity adds on top of these core flows — Pushed
Authorization Requests and DPoP sender-constrained tokens — see
[PAR & DPoP](/docs/protocols/par-and-dpop); these layer on top of the flows above rather than
replacing them.

## Next steps

- [Stores](./stores) — persisting the grants these flows create
- [PKCE](/docs/security/pkce) — why it's non-optional and how enforcement actually works
