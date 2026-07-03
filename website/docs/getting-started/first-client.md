---
sidebar_position: 4
---

# Adding your first client

The [Quickstart](./quickstart) used a `client_credentials` client — a machine-to-machine
client with no user involved. Most real applications need an **interactive** client instead:
a user logs in through a browser, and the client receives tokens on their behalf via the
Authorization Code flow with PKCE. (Not sure this is the right flow for what you're building?
See [Which flow do I need?](/docs/quick-setups/which-flow).)

## A code + PKCE client

```csharp
using GPHosting.Identity.Models;

new Client
{
    ClientId = "my-web-app",
    ClientSecrets = { new Secret("secret".Sha256()) },

    AllowedGrantTypes = GrantTypes.Code,
    RequirePkce = true,

    RedirectUris = { "https://localhost:7001/signin-oidc" },
    PostLogoutRedirectUris = { "https://localhost:7001/signout-callback-oidc" },

    AllowedScopes =
    {
        "openid",
        "profile",
        "api1"
    }
}
```

Key differences from the client-credentials example:

- **`AllowedGrantTypes = GrantTypes.Code`** — authorization code flow, the flow you want for
  any client where a real user signs in via a browser.
- **`RequirePkce = true`** — enforced by default for all clients regardless of this flag (see
  [PKCE](/docs/security/pkce)), set explicitly here for clarity.
- **`RedirectUris`** — the exact URL(s) GPHosting.Identity is allowed to redirect back to after
  login. This is an allowlist, not a pattern — the client's request must match one of these
  exactly, or the request is rejected. This is a core anti-phishing protection; don't be
  tempted to use wildcards (see [Redirect URIs](/docs/security/redirect-uris)).
- **`AllowedScopes`** includes `openid` and `profile` — these are **identity scopes**, not API
  scopes. They control what claims about the *user* end up in the ID token (name, email,
  etc.), separate from `api1` which controls what the *access token* can be used for. See
  [Resources](/docs/fundamentals/resources).

## Consuming it

On the client side, in an ASP.NET Core MVC app, this pairs with the standard
`Microsoft.AspNetCore.Authentication.OpenIdConnect` handler — GPHosting.Identity is a standards
-compliant OpenID Connect provider, so there's nothing GPHosting.Identity-specific needed on the
client. See the [samples](/docs/samples/overview) for a complete working MVC client.

## Next steps

- [Login and consent UI](./login-and-consent) — building the pages this flow redirects to, since
  GPHosting.Identity doesn't ship them
- [Architecture](/docs/fundamentals/architecture) — how clients, resources, scopes, and grants
  fit together
- [Flows](/docs/fundamentals/flows) — authorization code, client credentials, device flow, and
  refresh tokens in more depth
