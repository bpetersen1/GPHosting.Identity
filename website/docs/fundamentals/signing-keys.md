---
sidebar_position: 1
---

# Signing Keys

Every token GPHosting.Identity issues is a signed JWT. The signing key is what makes that
signature meaningful — anyone validating a token needs access to the corresponding public key,
and anyone who gets hold of the *private* key can forge tokens that your APIs will accept as
genuine. Getting key management right is one of the few things in an OpenID Connect deployment
that's genuinely hard to fix after the fact.

## Development: `AddDeveloperSigningCredential()`

```csharp
builder.Services.AddIdentityServer()
    .AddDeveloperSigningCredential();
```

Generates an RSA key on first run and persists it to `tempkey.jwk` in the current working
directory (see [Program.cs walkthrough](/docs/getting-started/program-cs) for the exact
mechanics). This is fine for local development — the key is silently reused across restarts, so
tokens issued before a restart still validate — but it is **not appropriate for production**:

- The key lives unprotected on disk, in the deployment's working directory.
- If you run multiple instances (which you should, for any real deployment), each one generates
  its *own* key unless they share a working directory, meaning tokens issued by one instance
  won't validate against another.
- There's no rotation story — the key never changes unless you delete the file.

## Production: a real certificate

```csharp
using System.Security.Cryptography.X509Certificates;

var cert = new X509Certificate2("path/to/cert.pfx", "certificate-password");

builder.Services.AddIdentityServer()
    .AddSigningCredential(cert, "RS256");
```

`AddSigningCredential` also accepts a raw `RsaSecurityKey`, `ECDsaSecurityKey`, or
`SigningCredentials` directly, if you're loading key material from a secrets manager (Azure Key
Vault, AWS Secrets Manager, HashiCorp Vault) rather than a certificate file on disk. However you
load it, the requirements are the same: an **asymmetric** key (RSA or ECDsa) — GPHosting.Identity
explicitly rejects symmetric keys for token signing, since a symmetric key would mean every
service capable of *validating* a token could also *forge* one.

Supported algorithms: `RS256`/`RS384`/`RS512`, `PS256`/`PS384`/`PS512` (RSA), and
`ES256`/`ES384`/`ES512` (ECDsa). RS256 is the most broadly compatible with OIDC client
libraries; ES256 produces smaller tokens if you control both ends of the integration.

## Key rotation

You can register multiple signing credentials at once — GPHosting.Identity uses the first one
registered to actually sign new tokens, but will *validate* tokens against all of them:

```csharp
builder.Services.AddIdentityServer()
    .AddSigningCredential(currentCert, "RS256")
    .AddValidationKey(new RsaSecurityKey(previousKey.GetRSAPublicKey()));
```

This is the pattern for rotating a signing key without invalidating tokens that were issued
just before the rotation: introduce the new key as a **validation** key first (so it appears in
the discovery document's JWKS alongside the current signing key), deploy, wait out your longest
token lifetime, then promote it to the signing credential and drop the old key from validation.

## Next steps

- [Discovery](/docs/fundamentals/discovery) — *coming soon* — how signing keys are published via
  the discovery document's JWKS endpoint
- [Stores](/docs/fundamentals/stores) — *coming soon* — in-memory vs. database-backed
  configuration, the other piece of moving from local dev to production
