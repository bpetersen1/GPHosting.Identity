---
sidebar_position: 2
---

# Resources

[Architecture](./architecture) introduced the split between identity resources and API
resources/scopes. This page covers the actual model shapes and how claims flow from them into
tokens.

## Identity resources

An identity resource is a named group of claims about the *user*, requested via a scope in the
`openid` connect sense:

```csharp
new IdentityResource("profile", "User profile", new[] { "name", "family_name", "given_name", "picture" })
```

GPHosting.Identity ships the standard OIDC ones out of the box via
`IdentityResources.OpenId()`, `IdentityResources.Profile()`, `IdentityResources.Email()`, etc. —
you rarely need to hand-write these unless you're adding custom claims (e.g. a `tenant_id` your
own application needs in the ID token):

```csharp
.AddInMemoryIdentityResources([
    new IdentityResources.OpenId(),
    new IdentityResources.Profile(),
    new IdentityResource("tenant", "Tenant membership", new[] { "tenant_id" })
])
```

Two flags control consent screen behavior, not token content:

- **`Required`** — if `true`, the user can't opt out of granting this scope during consent (used
  for `openid` itself — you can't log in without it).
- **`Emphasize`** — hints to a consent UI that this scope should be visually called out (e.g.
  "this app will see your email address").

## API resources and API scopes

An API resource represents an API you're protecting. It groups one or more API scopes — the
specific permissions a token can carry:

```csharp
new ApiResource("orders-api", "Orders API")
{
    Scopes = { "orders.read", "orders.write" },
    ApiSecrets = { new Secret("api-secret".Sha256()) }, // only needed for introspection
    AllowedAccessTokenSigningAlgorithms = { "RS256" }    // omit to use the server default
}
```

```csharp
new ApiScope("orders.read", "Read orders"),
new ApiScope("orders.write", "Modify orders")
```

Why the split between resource and scope, rather than just scopes? Because more than one API can
share a scope name (`orders.read` might mean the same thing whether it's your REST API or your
gRPC API), and a single API resource can expose several distinct permission levels. A client's
`AllowedScopes` lists scopes, not resources — the resource grouping mostly matters for API-side
token validation (an API validates that its access token contains a scope it recognizes, and
optionally that the token was actually intended for it via the `aud` claim).

`ApiSecrets` on an `ApiResource` are for **token introspection** (`/connect/introspect`) — used
when an API can't validate JWTs locally (e.g. reference tokens) and needs to ask GPHosting.Identity
whether a token is still valid. If you're using self-contained JWT access tokens (the default),
most APIs validate locally and never need this.

## Which claims end up where

| Resource type | Ends up in | Answers |
|---|---|---|
| Identity resource | ID token | "Who is this person?" |
| API resource / scope | Access token | "What can this token do?" |

A single authorization request typically asks for both — `openid profile orders.read` gets you
an ID token with the user's profile claims and an access token scoped to `orders.read`. The two
tokens serve different audiences: the ID token is for *your client* to read (never forward it to
an API), the access token is for *the API* to validate.

## Next steps

- [Flows](./flows) — how a client actually obtains these tokens
- [Stores](./stores) — moving resource definitions from an in-memory list to a database
