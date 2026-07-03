---
sidebar_position: 2
---

# Rich Authorization Requests (RAR, RFC 9396)

Plain OAuth scopes are flat strings — `orders.read` either is or isn't granted, with no room to
express "read order #12345 only" or "transfer up to $500." RAR replaces (or supplements) scopes
with structured JSON objects, `authorization_details`, that describe exactly what's being
authorized — the type of access, and arbitrary type-specific fields alongside it.

## Requesting fine-grained authorization

```shell
curl -G https://localhost:5001/connect/authorize \
  --data-urlencode "client_id=web-app" \
  --data-urlencode "response_type=code" \
  --data-urlencode "redirect_uri=https://localhost:7001/signin-oidc" \
  --data-urlencode "scope=openid" \
  --data-urlencode 'authorization_details=[{"type":"payment_initiation","actions":["initiate"],"instructedAmount":{"currency":"USD","amount":"500.00"}}]'
```

`authorization_details` is a JSON array — a single request can carry multiple entries, each
describing a different piece of access being requested. The only field GPHosting.Identity
requires structurally is `type`; everything else in each object (`actions`, `instructedAmount`,
or whatever your resource type needs) is opaque to the validator and passed through as-is for
your own application logic to interpret.

## Allowlisting types per client

Every `type` requested must be explicitly allowed on the client — there's no default-allow:

```csharp
new Client
{
    ClientId = "web-app",
    AllowedAuthorizationDetailsTypes = { "payment_initiation", "account_information" },
    // ...
}
```

A client with an empty (or unset) `AllowedAuthorizationDetailsTypes` can't use RAR at all — any
`authorization_details` on its requests gets rejected outright, not silently ignored. And it's
all-or-nothing per request: if one entry in the array names a type the client isn't allowed to
use, the *whole* authorize request fails rather than honoring just the allowed entries. This
mirrors how scopes and redirect URIs are allowlisted elsewhere in GPHosting.Identity — an
allowlist that fails closed is much harder to misconfigure into something exploitable than one
that silently drops what it doesn't recognize.

## What gets validated

Beyond the type allowlist, GPHosting.Identity requires:

- `authorization_details` must parse as a JSON array (not a bare object, not a string)
- every entry must be a JSON object
- every entry must have a non-empty `type` field

Malformed input — invalid JSON, a bare object instead of an array, an entry missing `type` — is
rejected with `invalid_authorization_details`, the error code RFC 9396 defines for this case.

## Where the value ends up

The validated `authorization_details` survives the authorization code round-trip and is echoed
back on the token response:

```json
{
  "access_token": "eyJ...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "authorization_details": [
    {"type": "payment_initiation", "actions": ["initiate"], "instructedAmount": {"currency": "USD", "amount": "500.00"}}
  ]
}
```

Your API (or the code issuing the access token, via a custom `IProfileService` — see
[Extensibility](/docs/fundamentals/extensibility)) is responsible for actually enforcing what
the `authorization_details` describes; GPHosting.Identity's job ends at validating the request
shape and the client's allowlist, not interpreting what a `payment_initiation` type means to your
business.

## Scope

RAR is currently wired for the **authorization code flow only** — not client credentials or
device flow. RFC 9396 is framed primarily around interactive flows where a user is consenting to
specific, fine-grained access, which is the scenario this targets.

## Next steps

- [PAR & DPoP](./par-and-dpop) — the other enforced protocol extensions on top of the core flows
- [JARM](./jarm) — verifying the authorize response itself came from this server
- [Resources](/docs/fundamentals/resources) — how RAR relates to the coarser scope model
