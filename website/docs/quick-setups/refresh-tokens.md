---
sidebar_position: 4
---

# 5 min setup: Refresh tokens

Not a grant type on its own — an add-on to [authorization code](./authorization-code) or
[device flow](./device-flow) clients that need long-lived access without keeping the user signed
in indefinitely. Conceptually covered in [Flows](/docs/fundamentals/flows#refresh-tokens).

## Server side

```csharp
new Client
{
    ClientId = "web-app",
    ClientSecrets = { new Secret("secret".Sha256()) },
    AllowedGrantTypes = GrantTypes.Code,
    RedirectUris = { "https://localhost:7001/signin-oidc" },
    AllowedScopes = { "openid", "profile", "orders.read", "offline_access" },

    AllowOfflineAccess = true,                       // enables issuing refresh tokens
    RefreshTokenUsage = TokenUsage.OneTimeOnly,       // rotate on every use — see Security note below
    RefreshTokenExpiration = TokenExpiration.Sliding,
    SlidingRefreshTokenLifetime = 1_296_000,          // 15 days, in seconds
    AbsoluteRefreshTokenLifetime = 2_592_000          // 30 days hard cap, in seconds
}
```

Requires `AllowedScopes` to include `offline_access` — the standard OIDC scope that signals "I
want a refresh token," in addition to `AllowOfflineAccess = true` on the client. Both are needed;
either one alone won't issue a refresh token.

Also requires a persisted grant store in anything beyond local single-instance testing — refresh
tokens are stored server-side and looked up on every use (see [Stores](/docs/fundamentals/stores)).

## Client side (ASP.NET Core MVC, using `SaveTokens`)

```csharp
.AddOpenIdConnect("oidc", options =>
{
    options.Authority = "https://localhost:5001";
    options.ClientId = "web-app";
    options.ClientSecret = "secret";
    options.ResponseType = "code";
    options.Scope.Add("offline_access"); // request it explicitly
    options.SaveTokens = true;           // persists access + refresh token in the auth cookie
});
```

Actually using the refresh token to get a new access token once the old one expires is a separate
concern from obtaining it — most setups reach for a package like
`Duende.AccessTokenManagement` or hand-roll a background renewal using
`IdentityModel.Client`'s `RequestRefreshTokenAsync`:

```csharp
using IdentityModel.Client;

var tokenResponse = await client.RequestRefreshTokenAsync(new RefreshTokenRequest
{
    Address = disco.TokenEndpoint,
    ClientId = "web-app",
    ClientSecret = "secret",
    RefreshToken = savedRefreshToken
});

// tokenResponse.RefreshToken is the *new* token if RefreshTokenUsage = OneTimeOnly — store it,
// the old one is now invalid.
```

## Why `OneTimeOnly` over `ReUse`

With `TokenUsage.OneTimeOnly`, every refresh rotates the token — the previous one stops working
immediately. This makes a stolen-but-unused refresh token detectable: if both the legitimate
client and an attacker try to redeem the same token, only the first one to arrive succeeds, and
the second failing is a strong signal something is compromised. `TokenUsage.ReUse` (the same
refresh token works indefinitely until expiry) is simpler but gives up that detection — prefer
`OneTimeOnly` unless you have a specific reason not to.

## Checklist if it's not working

- `offline_access` missing from either `AllowedScopes` (server) or the requested `Scope` (client)
  — both are required, and the failure mode is just "no refresh token in the response," not an
  error.
- If refreshing suddenly starts failing for a previously-working client, check
  `AbsoluteRefreshTokenLifetime` hasn't been exceeded — sliding expiration extends the token on
  each use, but the absolute cap is a hard stop regardless.
- With `OneTimeOnly`, make sure your client actually persists the *new* refresh token from each
  response — reusing a stale one after rotation will fail by design.

## Next steps

- [Stores](/docs/fundamentals/stores) — where refresh tokens are persisted
- [Flows](/docs/fundamentals/flows) — how this fits into authorization code and device flow overall
