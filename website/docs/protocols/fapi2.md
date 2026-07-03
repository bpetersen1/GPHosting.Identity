---
sidebar_position: 4
---

# FAPI 2.0 profile flag

`Client.RequireFapi2` composes [PAR](./par-and-dpop#pushed-authorization-requests-par-rfc-9126),
[DPoP](./par-and-dpop#dpop--demonstration-of-proof-of-possession-rfc-9449), and PKCE into one
enforced bundle for a client — set it once instead of remembering to configure each requirement
individually.

```csharp
new Client
{
    ClientId = "financial-app",
    ClientSecrets = { new Secret("secret".Sha256()) },
    AllowedGrantTypes = GrantTypes.Code,
    RequireFapi2 = true, // implies the requirements below
    // ...
}
```

## What `RequireFapi2 = true` actually enforces today

| Requirement | Enforced at | Effect |
|---|---|---|
| PAR mandatory | Authorize endpoint | Implies `RequirePushedAuthorization` even if that flag isn't separately set — any authorize request that didn't arrive via `/connect/par` is rejected |
| `response_type=code` only | Authorize endpoint | Implicit and hybrid responses are rejected, even for a client whose `AllowedGrantTypes` would otherwise permit them |
| PKCE with S256 mandatory | Authorize endpoint | `code_challenge_method=plain` is rejected even if the client has `AllowPlainTextPkce = true`; PKCE is required even for confidential clients that wouldn't otherwise need it |
| Sender-constrained access tokens | Token endpoint | A token request without a valid `DPoP` header is rejected outright — no plain bearer token is issued to a FAPI 2.0 client |

Each of these composes an already-independently-enforced feature — [PAR](./par-and-dpop),
[DPoP](./par-and-dpop#dpop--demonstration-of-proof-of-possession-rfc-9449), and
[PKCE](/docs/security/pkce) — rather than introducing new validation logic. `RequireFapi2` is a
convenience flag that turns all three on together and closes the gaps a client could otherwise
leave open (plain PKCE, no PAR, bearer-only tokens) even while nominally trying to comply.

## What this is *not*

This is a useful subset of the FAPI 2.0 Security Profile, not full conformance with the spec. The
full profile also covers things this flag does not enforce:

- **Client authentication method restrictions** — FAPI 2.0 requires asymmetric client
  authentication (`private_key_jwt` or mTLS), not `client_secret_basic`/`client_secret_post`.
  `RequireFapi2` doesn't restrict which authentication method a client uses.
- **mTLS as an alternative sender-constraining mechanism** — the spec allows mTLS-bound tokens as
  an alternative to DPoP; this implementation only recognizes DPoP.
- **JARM as a mandatory response mode** — [JARM](./jarm) is available and works, but
  `RequireFapi2` doesn't currently force its use.
- **Request object requirements** — FAPI 2.0 has additional rules around signed request objects
  that aren't checked here.

If you need full FAPI 2.0 certification-level conformance, treat this flag as a strong starting
point, not a compliance guarantee — verify the specific requirements your certification or
regulator cares about against the table above.

## Next steps

- [PAR & DPoP](./par-and-dpop) — the two protocols this flag builds on
- [RAR](./rar) and [JARM](./jarm) — the other advanced protocols, usable independently of FAPI 2.0
- [PKCE](/docs/security/pkce) — why S256 over plain matters
