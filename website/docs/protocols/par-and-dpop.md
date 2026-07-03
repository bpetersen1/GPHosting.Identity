---
sidebar_position: 1
---

# PAR & DPoP

These are the two OAuth 2.0 extensions IdentityServer4 never shipped that are actually enforced
end-to-end in GPHosting.Identity today — verified against the validators and endpoint handlers
directly, not just declared in a spec list. (Two related features, JARM response modes and a
FAPI 2.0 profile flag, exist in the data model as groundwork but aren't enforced yet — see the
note at the bottom.)

## Pushed Authorization Requests (PAR, RFC 9126)

Normally, authorization request parameters (`scope`, `redirect_uri`, `response_type`, etc.) travel
in the browser's URL when redirecting to `/connect/authorize` — visible in browser history,
referrer headers, and to anything watching the redirect. PAR moves them server-to-server instead:
the client POSTs the parameters directly to `/connect/par`, gets back a short-lived `request_uri`
handle, and only *that handle* appears in the browser redirect.

### Making a pushed authorization request

```shell
curl -X POST https://localhost:5001/connect/par \
  -u "web-app:secret" \
  -d "response_type=code" \
  -d "client_id=web-app" \
  -d "redirect_uri=https://localhost:7001/signin-oidc" \
  -d "scope=openid profile orders.read" \
  -d "code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM" \
  -d "code_challenge_method=S256"
```

```json
{ "request_uri": "urn:ietf:params:oauth:request_uri:a1b2c3d4e5f6...", "expires_in": 60 }
```

Then redirect the browser using just the handle instead of the full parameter set:

```
https://localhost:5001/connect/authorize?client_id=web-app&request_uri=urn:ietf:params:oauth:request_uri:a1b2c3d4e5f6...
```

The authorize endpoint resolves `request_uri` back to the stored parameters, enforces **one-time
use** (a second attempt to redeem the same `request_uri` fails), validates it hasn't expired
(60-second lifetime, matching the spec's recommendation for a narrow window), and rejects the
request if the `client_id` in the follow-up authorize call doesn't match the one that pushed the
original request.

### Requiring PAR for a client

```csharp
new Client
{
    ClientId = "web-app",
    RequirePushedAuthorization = true, // rejects any authorize request that didn't arrive via PAR
    // ...
}
```

With this set, a client attempting the traditional full-parameters-in-the-URL flow gets rejected
outright — useful for clients where you specifically want to guarantee authorization parameters
never transit the browser (financial/health data integrations, or just as defense-in-depth against
parameter tampering and referrer leakage).

## DPoP — Demonstration of Proof-of-Possession (RFC 9449)

A normal (`Bearer`) access token is a bearer credential — whoever holds it can use it, no further
proof needed. If it leaks (logged, intercepted, stolen from a compromised client), it's usable by
the attacker exactly like the legitimate client. DPoP binds a token to a specific key pair the
client controls, so a stolen token is useless without also stealing the private key.

### How it works

The client generates an asymmetric key pair (once, or per-session) and signs a small proof JWT for
each request, sent in a `DPoP` header:

```
DPoP: eyJ0eXAiOiJkcG9wK2p3dCIsImFsZyI6IkVTMjU2IiwiandrIjp7Li4ufQ.eyJodG0iOiJQT1NUIiwiaHR1IjoiaHR0cHM6Ly9sb2NhbGhvc3Q6NTAwMS9jb25uZWN0L3Rva2VuIiwiaWF0IjoxNzA5MzAwODAwLCJqdGkiOiJhYmMxMjMifQ.signature
```

The proof JWT's header carries the **public** key (`jwk`) and `typ: dpop+jwt`; the payload carries
`htm` (HTTP method), `htu` (HTTP URL), `iat`, and a unique `jti`. GPHosting.Identity's
`DefaultDPoPProofValidator` checks, in order:

- the JWT is well-formed and signed correctly against its own embedded public key
- the embedded `jwk` is genuinely public (rejects the proof outright if it contains private key
  material — `d` present)
- `htm` matches the actual HTTP method of the request
- `htu` matches the actual request URL (scheme + host + path, ignoring query string)
- `iat` is within a 5-minute window of the server's clock (bounds replay — an old proof is rejected
  even if otherwise valid)
- `jti` is present (a unique identifier per proof — required by the spec for replay protection)

On success, the access token gets a `cnf` (confirmation) claim containing the JWK thumbprint, and
the token response returns `"token_type": "DPoP"` instead of `"token_type": "Bearer"` — signaling
to the client (and any API validating the token) that this token is sender-constrained and must be
presented alongside a matching DPoP proof, not as a bare bearer token.

### Requesting a DPoP-bound token

No client configuration flag needed — DPoP is opt-in per-request, triggered simply by sending the
`DPoP` header on the token request:

```shell
curl -X POST https://localhost:5001/connect/token \
  -H "DPoP: <proof-jwt-for-this-request>" \
  -d "grant_type=client_credentials" \
  -d "client_id=worker-service" \
  -d "client_secret=secret" \
  -d "scope=orders.write"
```

The resulting access token is now bound to the key pair that signed the proof. Calling a
DPoP-aware API requires generating a **new** proof JWT per API call (with `htm`/`htu` matching
that specific request) — the token alone isn't enough, exactly like the design intends.

## What's not enforced yet

`JarmResponseModes` (`jwt`, `query.jwt`, `fragment.jwt`, `form_post.jwt`) exist as constants, but
`AuthorizeRequestValidator` doesn't currently accept them — requesting one of these `response_mode`
values fails with `unsupported_response_type`. Similarly, `Client.RequireFapi2` is a real,
persisted flag (present in every EF migration across all four database providers) that nothing in
the validation pipeline reads yet — setting it currently has no effect. Both are groundwork for
future work, not features to configure a client against today.

## Next steps

- [Flows](/docs/fundamentals/flows) — how PAR and DPoP layer on top of the core grant types
- [Security](/docs/security/pkce) — PKCE and redirect URI validation, the other enforced
  protections on the authorization code flow
