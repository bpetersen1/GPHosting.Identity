---
sidebar_position: 1
---

# Architecture

GPHosting.Identity's data model has four core concepts. Understanding how they relate is the
fastest way to reason about any given deployment.

## Clients

A **client** is an application that wants tokens — a web app, a SPA, a mobile app, a backend
service. It's not a person; it's the thing requesting access on a person's (or its own) behalf.
Every request to `/connect/token` or `/connect/authorize` identifies a client via `client_id`
(and usually a secret or PKCE proof).

A client declares, up front, what it's allowed to do:

- **`AllowedGrantTypes`** — which OAuth grant(s) it can use (see [Flows](./flows))
- **`AllowedScopes`** — which scopes it can request tokens for
- **`RedirectUris`** — where it's allowed to receive authorization responses (interactive clients
  only — see [Redirect URIs](/docs/security/redirect-uris))
- **`ClientSecrets`** — how it authenticates itself as a confidential client (machine-to-machine
  clients; public clients like SPAs typically have none and rely on PKCE instead)

Nothing about a client is optional-by-omission — if a grant type, scope, or redirect URI isn't
explicitly allowed, the request is rejected. This is deliberate: an allowlist model is much harder
to misconfigure into something exploitable than a denylist.

## Resources: identity vs. API

GPHosting.Identity issues two kinds of tokens, and resources map to which one populates what:

- **Identity resources** describe claims about the *user* — `openid` (subject id), `profile`
  (name, picture, etc.), `email`. These end up in the **ID token**, and answer "who is this
  person?"
- **API resources** describe an API you're protecting, and group one or more **API scopes** —
  the specific permissions a token can be issued for against that API. These end up in the
  **access token**, and answer "what is this token allowed to do?"

```csharp
new ApiResource("orders-api", "Orders API")
{
    Scopes = { "orders.read", "orders.write" }
}
```

A client's `AllowedScopes` can mix identity scopes (`openid`, `profile`) and API scopes
(`orders.read`) freely — a single token request for an interactive login typically asks for both,
so the client gets an ID token describing the user *and* an access token it can call the API with.

See [Resources](./resources) for the full breakdown of identity resources, API resources, and API
scopes, and how claims flow into each token type.

## Grants

A **persisted grant** is anything GPHosting.Identity needs to remember between requests:
an issued refresh token, an authorization code waiting to be redeemed, a device flow code waiting
for user approval, or a user's consent decision. Grants are transient by nature — they expire, get
consumed, or get revoked — which is why they live in a separate store from client/resource
configuration (see [Stores](./stores)): the two have very different read/write patterns and
retention needs.

## How a request flows through the pieces

A typical interactive login:

1. The **client** redirects the user's browser to `/connect/authorize` with its `client_id`,
   requested scopes, and `redirect_uri`.
2. GPHosting.Identity checks the client's `AllowedScopes` and `RedirectUris` — if either doesn't
   match what was requested, the request is rejected before the user sees anything.
3. The user authenticates (however your login UI is built — GPHosting.Identity doesn't ship one)
   and consents to the requested scopes.
4. GPHosting.Identity creates a **grant** (an authorization code) and redirects back to the
   client's `redirect_uri`.
5. The client exchanges the code at `/connect/token` for an ID token (from the requested
   **identity resources**) and an access token (scoped to the requested **API scopes**), consuming
   the grant in the process.
6. The client calls the API with the access token; the API validates it independently (it doesn't
   need to call back to GPHosting.Identity for every request — that's the point of a signed JWT).

## Next steps

- [Resources](./resources) — identity resources, API resources, and API scopes in depth
- [Flows](./flows) — authorization code, client credentials, device flow, and refresh tokens
- [Stores](./stores) — where clients, resources, and grants actually live
