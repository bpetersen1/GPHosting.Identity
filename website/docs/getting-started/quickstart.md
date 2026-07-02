---
sidebar_position: 2
---

# Quickstart

This gets a minimal token server running locally with an in-memory client and API scope — no
database required. It's meant for exploring the library, not production (see
[Signing Keys](/docs/fundamentals/signing-keys) — *coming soon* — and
[Stores](/docs/fundamentals/stores) — *coming soon* — before deploying anywhere real).

## 1. Create a new ASP.NET Core project

```shell
dotnet new web -n MyIdentityServer
cd MyIdentityServer
dotnet add package GPHosting.Identity
```

## 2. Wire it up in `Program.cs`

```csharp
using GPHosting.Identity.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdentityServer()
    .AddDeveloperSigningCredential()
    .AddInMemoryApiScopes([new ApiScope("api1", "My API")])
    .AddInMemoryClients([
        new Client
        {
            ClientId = "client",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = { "api1" }
        }
    ]);

var app = builder.Build();

app.UseIdentityServer();

app.Run();
```

That's it — 15 lines of actual configuration. `AddDeveloperSigningCredential()` generates a
throwaway RSA key on startup, which is fine for local exploration but must be replaced with a
persisted key before you deploy anywhere (see [Signing Keys](/docs/fundamentals/signing-keys) —
*coming soon*).

## 3. Run it

```shell
dotnet run
```

By default this listens on `https://localhost:5001` (check your console output for the exact
port). The discovery document is now live at:

```
https://localhost:5001/.well-known/openid-configuration
```

## 4. Request a token

With the server running, request an access token using the `client_credentials` grant:

```shell
curl -X POST https://localhost:5001/connect/token \
  -d "grant_type=client_credentials" \
  -d "client_id=client" \
  -d "client_secret=secret" \
  -d "scope=api1"
```

You should get back a JSON response containing an `access_token` — a signed JWT you can decode
at [jwt.io](https://jwt.io) to see the claims GPHosting.Identity issued.

## Next steps

- [Program.cs walkthrough](./program-cs) — what each line above actually does, and the options
  you'll want to know about early
- [Adding your first client](./first-client) — configuring a real interactive (browser-based)
  client instead of machine-to-machine client credentials
