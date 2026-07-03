---
sidebar_position: 4
---

# JARM (JWT Secured Authorization Response Mode)

A normal authorize response comes back as plain query parameters or a URL fragment —
`?code=abc123&state=xyz`. Nothing about that response is authenticated: if an attacker can get a
client to process a forged redirect (a mix-up attack between two authorization servers, a
response injected by a malicious app on the same device), the client has no way to verify the
response actually came from the identity server it thinks it's talking to. JARM wraps the whole
response in a JWT, signed with the server's own key, so the client can verify it cryptographically
before trusting anything in it.

## Requesting a JARM response

Set `response_mode` to one of the JWT-wrapped variants:

| `response_mode` | Delivery |
|---|---|
| `query.jwt` | JWT in the query string: `?response=<jwt>` |
| `fragment.jwt` | JWT in the URL fragment: `#response=<jwt>` |
| `form_post.jwt` | JWT in an auto-submitting form POST body |
| `jwt` | Delivery follows the plain-mode default for the response_type — query for code flow, fragment otherwise |

```
https://localhost:5001/connect/authorize?client_id=web-app&response_type=code&response_mode=query.jwt&redirect_uri=https://localhost:7001/signin-oidc&scope=openid&state=abc123
```

The redirect back to the client contains just `response=<jwt>` — none of the usual `code`,
`state`, etc. appear as separate parameters; they're all inside the signed JWT instead.

## What's inside the JWT

```json
{
  "iss": "https://localhost:5001",
  "aud": "web-app",
  "exp": 1735682400,
  "code": "abc123...",
  "state": "abc123"
}
```

Every parameter that would normally be a query/fragment value — `code`, `state`,
`session_state`, or `error`/`error_description` for an error response — becomes a claim in the
JWT instead. `iss` and `aud` are added specifically for JARM: they let the client confirm both
*who* issued the response and *that it was actually intended for this client*, closing the
mix-up-attack gap plain response modes leave open.

The JWT is short-lived (60 seconds) — it's meant to be verified and consumed immediately by the
client's redirect handler, not stored or replayed later.

## Verifying it on the client side

The signing key is the same one published at your server's JWKS endpoint
(`/.well-known/openid-configuration/jwks`) — the same key used for ID tokens. Most OIDC client
libraries with JARM support handle this automatically once you configure `response_mode`; if
you're verifying by hand:

```csharp
var jwks = await httpClient.GetFromJsonAsync<JsonWebKeySet>(
    "https://localhost:5001/.well-known/openid-configuration/jwks");

var handler = new JwtSecurityTokenHandler();
handler.ValidateToken(responseJwt, new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidIssuer = "https://localhost:5001",
    ValidateAudience = true,
    ValidAudience = "web-app",
    IssuerSigningKeys = jwks.GetSigningKeys()
}, out var validatedToken);
```

Validate `iss` and `aud` explicitly — that's the actual security benefit over a plain response
mode. Skipping those checks and just parsing the claims out of the JWT gets you JSON parsing with
extra steps, not the mix-up-attack protection JARM is for.

## Next steps

- [PAR & DPoP](./par-and-dpop) — the other enforced protocol extensions on top of the core flows
- [RAR](./rar) — fine-grained `authorization_details` on the authorize request itself
- [Signing Keys](/docs/fundamentals/signing-keys) — the key used to sign JARM responses is the
  same one used for ID tokens
