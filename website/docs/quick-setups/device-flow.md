---
sidebar_position: 4
---

# 5 min setup: Device flow

For input-constrained devices (smart TVs, CLI tools, IoT) with no convenient browser on the device
itself. Conceptually covered in [Flows](/docs/fundamentals/flows#device-flow-granttypesdeviceflow).

## Server side

```csharp
using GPHosting.Identity.Models;

builder.Services.AddIdentityServer()
    .AddDeveloperSigningCredential()
    .AddInMemoryIdentityResources([new IdentityResources.OpenId()])
    .AddInMemoryApiScopes([new ApiScope("orders.read", "Read orders")])
    .AddInMemoryClients([
        new Client
        {
            ClientId = "cli-tool",
            AllowedGrantTypes = GrantTypes.DeviceFlow,
            AllowedScopes = { "openid", "orders.read" },
            AllowOfflineAccess = true // usually wanted here — CLI tools shouldn't re-prompt hourly
        }
    ])
    .AddTestUsers(TestUsers.Users)
    .AddOperationalStore(options => // required — device codes live here while pending
    {
        options.ConfigureDbContext = b => b.UseSqlite("DataSource=devicecodes.db");
    });
```

Unlike client credentials or authorization code, device flow **requires an operational store**
(see [Stores](/docs/fundamentals/stores)) even for local testing — there's no in-memory device
code store, since the code needs to survive between the device's initial request and the
out-of-band approval on a second device.

Also make sure `UserInteraction.DeviceVerificationUrl` in
[`IdentityServerOptions`](/docs/configuration/options) points at a page your app serves where the
user enters the code — GPHosting.Identity doesn't ship this UI either.

## Client side

```csharp
using IdentityModel.Client;

var client = new HttpClient();
var disco = await client.GetDiscoveryDocumentAsync("https://localhost:5001");

// 1. Start the flow
var deviceAuthResponse = await client.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
{
    Address = disco.DeviceAuthorizationEndpoint,
    ClientId = "cli-tool",
    Scope = "openid orders.read"
});

Console.WriteLine($"Go to {deviceAuthResponse.VerificationUri} and enter code: {deviceAuthResponse.UserCode}");

// 2. Poll until the user approves (or denies/times out)
TokenResponse tokenResponse;
do
{
    await Task.Delay(TimeSpan.FromSeconds(deviceAuthResponse.Interval));

    tokenResponse = await client.RequestDeviceTokenAsync(new DeviceTokenRequest
    {
        Address = disco.TokenEndpoint,
        ClientId = "cli-tool",
        DeviceCode = deviceAuthResponse.DeviceCode
    });
} while (tokenResponse.IsError && tokenResponse.Error == "authorization_pending");
```

`deviceAuthResponse.Interval` is the minimum polling interval the server expects (see
`IdentityServerOptions.DeviceFlow` in [Configuration](/docs/configuration/options)) — polling
faster than this gets you a `slow_down` error, not a faster response.

## Checklist if it's not working

- Confirm the operational store is actually wired up — without it, the device authorization
  request itself fails since there's nowhere to persist the pending code.
- `VerificationUri` needs a real page behind it — if users hit a 404 when they follow it, that's
  the missing UI piece, not a device flow bug.
- If polling never resolves, check the client is actually approving against the same
  `verification_uri_complete`/user code shown — a mismatch between what's displayed and what's
  submitted is the most common integration bug here.

## Next steps

- [Stores](/docs/fundamentals/stores) — the operational store this flow depends on
- [Refresh tokens](./refresh-tokens) — combine with `AllowOfflineAccess` for long-lived CLI
  sessions
