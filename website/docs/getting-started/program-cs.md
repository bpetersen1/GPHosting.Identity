---
sidebar_position: 3
---

# Program.cs walkthrough

This walks through the [Quickstart](./quickstart)'s `Program.cs`, line by line, and calls out
the options you'll actually need to know about early.

```csharp
using GPHosting.Identity.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdentityServer()
    .AddDeveloperSigningCredential()
    .AddInMemoryApiScopes([new ApiScope("api1", "My API")])
    .AddInMemoryClients([ /* ... */ ]);

var app = builder.Build();

app.UseIdentityServer();

app.Run();
```

## `AddIdentityServer()`

Registers all of GPHosting.Identity's core services — validators, response generators,
endpoint handlers, default in-memory persisted grant store — and returns an
`IIdentityServerBuilder` you chain further configuration off of. Every `.Add*()` call after it
is a builder extension method that layers in one more piece of configuration.

You can pass an options delegate to configure server-wide behavior:

```csharp
builder.Services.AddIdentityServer(options =>
{
    options.Events.RaiseSuccessEvents = true;
    options.Events.RaiseFailureEvents = true;
    options.Events.RaiseErrorEvents = true;

    options.EmitScopesAsSpaceDelimitedStringInJwt = true;
});
```

See [IdentityServerOptions reference](/docs/configuration/options) — *coming soon* — for the
full list of options.

## `AddDeveloperSigningCredential()`

Generates (and caches to disk, by default in `tempkey.jwk` next to your project) a throwaway
RSA signing key at startup, so tokens are actually signed and validated correctly during local
development. **This is not suitable for production** — the key isn't shared across instances
and isn't backed by any secure storage. Before deploying, replace it with a real certificate or
key management service — see [Signing Keys](/docs/fundamentals/signing-keys).

## `AddInMemoryApiScopes(...)` / `AddInMemoryClients(...)`

Registers a fixed, in-memory list of API scopes and clients — no database involved. This is
great for getting started and for tests, but a static list baked into `Program.cs` doesn't
scale to managing real clients in production. When you're ready, swap these for
`AddConfigurationStore()` from the `GPHosting.Identity.EntityFramework` package, which reads
clients/scopes from a database instead — see [Stores](/docs/fundamentals/stores) —
*coming soon*.

## `app.UseIdentityServer()`

Adds the OpenID Connect / OAuth 2.0 middleware to the pipeline — this is what actually serves
`/connect/token`, `/connect/authorize`, `/.well-known/openid-configuration`, and the rest of the
protocol endpoints. If you're also using `UseRouting()` / `UseAuthorization()` / MVC endpoints
(for a login UI, for example), the ordering that works is:

```csharp
app.UseRouting();
app.UseIdentityServer();
app.UseAuthorization();

app.MapDefaultControllerRoute(); // or MapControllers(), etc.
```

`UseIdentityServer()` already includes CORS and routing internally for its own endpoints, so a
minimal API-only server (like the quickstart above) doesn't need `UseRouting()` at all.

## Next step

[Adding your first client](./first-client) walks through configuring an interactive
(browser-based) client instead of the machine-to-machine `client_credentials` example above.
