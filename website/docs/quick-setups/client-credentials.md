---
sidebar_position: 2
---

# 5 min setup: Client credentials

No user, no browser — a service authenticates as itself. This is the same example used in the
[Quickstart](/docs/getting-started/quickstart); this page is the minimal reference version with
nothing else attached.

## Server side

```csharp
using GPHosting.Identity.Models;

builder.Services.AddIdentityServer()
    .AddDeveloperSigningCredential()
    .AddInMemoryApiScopes([new ApiScope("orders.write", "Write orders")])
    .AddInMemoryClients([
        new Client
        {
            ClientId = "worker-service",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = { "orders.write" }
        }
    ]);
```

That's the entire server-side requirement: one API scope, one client with a secret, one grant
type.

## Client side

No library needed — a raw HTTP POST is enough, since there's no browser redirect involved:

```shell
curl -X POST https://localhost:5001/connect/token \
  -d "grant_type=client_credentials" \
  -d "client_id=worker-service" \
  -d "client_secret=secret" \
  -d "scope=orders.write"
```

In an actual service, use `IdentityModel`'s `HttpClient` extensions (or your language's OIDC
client library) instead of hand-rolling the request:

```csharp
using IdentityModel.Client;

var client = new HttpClient();
var disco = await client.GetDiscoveryDocumentAsync("https://localhost:5001");

var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
{
    Address = disco.TokenEndpoint,
    ClientId = "worker-service",
    ClientSecret = "secret",
    Scope = "orders.write"
});
```

## Checklist if it's not working

- Client's `AllowedScopes` must include every scope you're requesting — a partial mismatch fails
  the whole request, not just the unlisted scope.
- `ClientSecrets` on the client must match what you're sending — remember `new Secret("secret".Sha256())`
  hashes the secret; you still send the plaintext `"secret"` from the client.
- `AllowedGrantTypes` must be exactly `GrantTypes.ClientCredentials` (or include it) — this is the
  one flow with zero interactive/browser involvement, so a client also configured for
  `GrantTypes.Code` doesn't automatically get client credentials too.

## Next steps

- [Authorization code](./authorization-code) — the flow for when a real user is involved
- [Flows](/docs/fundamentals/flows#client-credentials-granttypesclientcredentials) — why this
  grant exists and when to reach for it
