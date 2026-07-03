---
sidebar_position: 2
---

# 5 min setup: Authorization code + PKCE

The flow for any client where a real user signs in through a browser. Covered conceptually in
[Adding your first client](/docs/getting-started/first-client) — this page is the minimal
copy-paste reference, with the confidential (server-rendered web app) and public (SPA) variants
side by side.

## Server side

```csharp
using GPHosting.Identity.Models;

builder.Services.AddIdentityServer()
    .AddDeveloperSigningCredential()
    .AddInMemoryIdentityResources([new IdentityResources.OpenId(), new IdentityResources.Profile()])
    .AddInMemoryApiScopes([new ApiScope("orders.read", "Read orders")])
    .AddInMemoryClients([
        // confidential — server-rendered app, can hold a secret
        new Client
        {
            ClientId = "web-app",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.Code,
            RedirectUris = { "https://localhost:7001/signin-oidc" },
            PostLogoutRedirectUris = { "https://localhost:7001/signout-callback-oidc" },
            AllowedScopes = { "openid", "profile", "orders.read" },
            AllowOfflineAccess = true // only if you also want refresh tokens — see that page
        },
        // public — SPA, no secret, PKCE-only (see Security > PKCE for why this is safe)
        new Client
        {
            ClientId = "spa-app",
            RequireClientSecret = false,
            AllowedGrantTypes = GrantTypes.Code,
            RedirectUris = { "https://localhost:4200/callback" },
            PostLogoutRedirectUris = { "https://localhost:4200" },
            AllowedCorsOrigins = { "https://localhost:4200" },
            AllowedScopes = { "openid", "profile", "orders.read" }
        }
    ])
    .AddTestUsers(TestUsers.Users); // or your real user store — see Fundamentals > Stores
```

Note what's *absent* on the SPA client: no `ClientSecrets`. A secret shipped in browser-delivered
JavaScript isn't secret — PKCE (enforced unconditionally for clients with
`RequireClientSecret = false`, see [PKCE](/docs/security/pkce)) is what actually protects this
client, not a shared secret.

## Client side — confidential (ASP.NET Core MVC)

```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies";
    options.DefaultChallengeScheme = "oidc";
})
.AddCookie("Cookies")
.AddOpenIdConnect("oidc", options =>
{
    options.Authority = "https://localhost:5001";
    options.ClientId = "web-app";
    options.ClientSecret = "secret";
    options.ResponseType = "code"; // authorization code flow
    options.Scope.Add("profile");
    options.Scope.Add("orders.read");
    options.SaveTokens = true;
});
```

## Client side — public (SPA, e.g. `oidc-client-ts`)

```javascript
import { UserManager } from "oidc-client-ts";

const userManager = new UserManager({
  authority: "https://localhost:5001",
  client_id: "spa-app",
  redirect_uri: "https://localhost:4200/callback",
  post_logout_redirect_uri: "https://localhost:4200",
  response_type: "code", // PKCE is handled automatically by the library
  scope: "openid profile orders.read",
});

await userManager.signinRedirect();
```

Any standards-compliant OIDC library handles PKCE generation/verification transparently — you
don't write PKCE code by hand on either the server or client side, it's a library-level detail on
top of the code flow.

## Checklist if it's not working

- `RedirectUris` must **exactly** match the client's configured redirect URI — no trailing slash
  differences, no path differences (see [Redirect URIs](/docs/security/redirect-uris)).
- SPA clients need `AllowedCorsOrigins` set to the origin actually making the browser request, or
  the authorize/token endpoints will reject the cross-origin call.
- If login fails silently with no error page, check `UserInteraction.LoginUrl` in
  [`IdentityServerOptions`](/docs/configuration/options) points at a login page your app
  actually serves — GPHosting.Identity has no built-in login UI (see
  [Login and consent UI](/docs/getting-started/login-and-consent)).

## Next steps

- [Refresh tokens](./refresh-tokens) — keeping the confidential client's session alive without
  re-prompting login
- [Login and consent UI](/docs/getting-started/login-and-consent) — the UI pieces this flow
  depends on that GPHosting.Identity doesn't ship
