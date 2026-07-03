---
sidebar_position: 1
---

# PKCE enforcement

PKCE (Proof Key for Code Exchange, RFC 7636) closes the authorization code interception attack:
without it, anything that can observe the redirect back to a client — a malicious app registered
for the same custom URI scheme on a mobile device, a network observer on a non-HTTPS hop, a
compromised proxy — can steal the authorization code and redeem it for tokens itself, before the
legitimate client does.

## What GPHosting.Identity enforces

Per [OAuth 2.0 Security Best Current Practice](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-security-topics)
(RFC 9700), **public clients must always use PKCE — unconditionally**, regardless of the client's
`RequirePkce` setting:

```csharp
new Client
{
    ClientId = "public-spa",
    RequireClientSecret = false, // this makes it a "public" client
    RequirePkce = false,          // ignored — PKCE is still required
    AllowedGrantTypes = GrantTypes.Code,
    // ...
}
```

An authorization request for this client that omits `code_challenge` is rejected before a login
page is even shown. This is enforced in the validator itself, not left to configuration — you
cannot accidentally turn it off for a public client by setting `RequirePkce = false`.

**Confidential clients** (`RequireClientSecret = true`) are not forced into PKCE unless
`RequirePkce` is explicitly `true` — they already authenticate via their client secret at the
token exchange, which provides an equivalent guarantee that only the legitimate client can redeem
the code. Setting `RequirePkce = true` on a confidential client adds PKCE as defense-in-depth on
top of the secret; this is a reasonable default for new clients even when not strictly required.

| Client type | `RequirePkce` | Result |
|---|---|---|
| Public (`RequireClientSecret = false`) | any value | PKCE required — enforced unconditionally |
| Confidential (`RequireClientSecret = true`) | `false` | PKCE optional |
| Confidential (`RequireClientSecret = true`) | `true` | PKCE required |

## Verifying this yourself

The enforcement is covered by
`src/GPHosting.Identity/test/IdentityServer.UnitTests/Security/PkceEnforcementTests.cs` — the
tests assert the failure case directly (a public client without a challenge is rejected with an
error mentioning "code challenge"), not just that the happy path succeeds. Worth reading if you
want to see the exact validator behavior rather than take this page's word for it.

## Next steps

- [Redirect URIs](./redirect-uris) — the other half of authorization code flow security
- [Flows](/docs/fundamentals/flows) — where PKCE fits into the authorization code flow overall
