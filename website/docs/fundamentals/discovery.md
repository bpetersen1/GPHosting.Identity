---
sidebar_position: 6
---

# Discovery document

Every OpenID Connect provider publishes a discovery document — a JSON file describing its
endpoints, capabilities, and public signing keys — so clients can configure themselves from a
single URL instead of hardcoding every endpoint:

```
https://your-server/.well-known/openid-configuration
```

Most OIDC client libraries (`Microsoft.AspNetCore.Authentication.OpenIdConnect`, and equivalents
in other languages) only need the issuer URL — they fetch this document once and cache it.

## What's in it

The document is built from your actual configuration, not maintained separately — it reflects
whatever clients, scopes, and options you've registered:

- `issuer` — from `IdentityServerOptions.IssuerUri` (or inferred from the request if unset — see
  [Deployment](/docs/operations/deployment) for why you should set this explicitly in production)
- `authorization_endpoint`, `token_endpoint`, `userinfo_endpoint`, `end_session_endpoint`, etc.
- `jwks_uri` — points to the JSON Web Key Set (JWKS) endpoint, which publishes the **public** half
  of your signing key(s) so clients and APIs can verify token signatures without ever seeing the
  private key
- `scopes_supported`, `claims_supported`, `response_types_supported`,
  `grant_types_supported` — reflect your registered identity/API resources and enabled grant types
- `token_endpoint_auth_methods_supported` — which client authentication methods are accepted
  (`client_secret_basic`, `client_secret_post`, `private_key_jwt`, etc.)

## Controlling what's published

`DiscoveryOptions` (under `IdentityServerOptions.Discovery`) lets you turn off individual sections
if you don't want them exposed:

```csharp
builder.Services.AddIdentityServer(options =>
{
    options.Discovery.ShowApiScopes = false;       // omit API scopes from the public document
    options.Discovery.ShowExtensionGrantTypes = false;
    options.Discovery.ResponseCacheInterval = 3600; // hint clients to cache for 1 hour
});
```

Turning off a section (e.g. `ShowApiScopes`) doesn't disable the underlying feature — it just
stops advertising it in the discovery document. Useful if you have internal-only scopes you don't
want listed publicly, though remember this is *obscurity*, not access control: a client that
already knows the scope name can still request it.

`CustomEntries` lets you add arbitrary extra fields some ecosystems expect:

```csharp
options.Discovery.CustomEntries.Add("my_custom_property", "value");
```

## Key rotation and the JWKS endpoint

This is where [Signing Keys](/docs/fundamentals/signing-keys)'s key rotation pattern actually
takes effect: every signing *and* validation key you register (via `AddSigningCredential` and
`AddValidationKey`) shows up in the JWKS response, keyed by `kid` (key ID). A client validating a
token looks up the matching `kid` in the JWKS — this is exactly why introducing a new key as a
validation key first, before promoting it to signing, works: the new key is already discoverable
via JWKS before anything is signed with it.

## Next steps

- [Signing Keys](/docs/fundamentals/signing-keys) — how keys get into the JWKS in the first place
- [Deployment](/docs/operations/deployment) — setting `IssuerUri` correctly behind a reverse proxy
  or load balancer, so the discovery document reflects the URL clients actually use
