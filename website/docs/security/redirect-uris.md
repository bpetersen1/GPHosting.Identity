---
sidebar_position: 2
---

# Redirect URI validation

A client's `RedirectUris` is an **allowlist of exact strings**, not a pattern. GPHosting.Identity
uses a `StrictRedirectUriValidator` that requires a byte-for-byte match between the
`redirect_uri` sent in the authorization request and one of the URIs registered on the client.
This is one of the most important anti-phishing protections in the whole protocol — get it wrong
and you've built an open redirect.

## What "strict" actually rejects

All of the following are rejected against a client registered with
`https://app.example.com/callback`, even though a naive prefix or "starts with" check would let
several of them through:

| Incoming `redirect_uri` | Rejected because |
|---|---|
| `https://app.example.com/admin` | Different path |
| `https://evil.example.com/callback` | Different host |
| `http://app.example.com/callback` | Scheme downgrade (https → http) |
| `https://app.example.com/callback?injected=value` | Appended query string |
| (registered as `https://app.example.com/*`) | Wildcards are never interpreted as patterns |
| `null` / empty string | No match possible |

Only an **exact string match** against one of the client's registered `RedirectUris` succeeds — a
client can register multiple URIs (e.g. one for the app, one for silent token renewal), and any
exact match among them is accepted.

## Why this matters more than it looks like it should

The redirect URI is where the authorization code (or, in the implicit/hybrid flows, tokens
directly) gets delivered. If an attacker can get GPHosting.Identity to redirect to a URI they
control — even one that merely *starts with* or *contains* the registered value — they can harvest
the code or token from that redirect. Query string injection is a subtler variant of the same
problem: allowing extra query parameters on an otherwise-correct URI can let an attacker append
their own parameters that a loosely-written client-side handler might misinterpret.

This is exactly why GPHosting.Identity's validator does not support wildcard or prefix
registration, even though it would be more convenient during development (e.g. matching any
`localhost` port). Register each redirect URI you actually use, including local development ports,
explicitly.

## Verifying this yourself

Covered by
`src/GPHosting.Identity/test/IdentityServer.UnitTests/Security/RedirectUriValidationTests.cs` —
each rejection case above (different path, different host, scheme downgrade, query injection,
wildcard, null, empty) has a dedicated test asserting it fails, alongside tests confirming exact
and multi-URI matches succeed.

## Next steps

- [PKCE](./pkce) — the other core protection for the authorization code flow
- [Adding your first client](/docs/getting-started/first-client) — where `RedirectUris` is
  actually configured
