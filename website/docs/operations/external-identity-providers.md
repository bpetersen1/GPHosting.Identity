---
sidebar_position: 2
---

# External identity providers

GPHosting.Identity can act as a middleman: instead of validating credentials itself, it delegates
login to Google, Azure AD, ADFS, or any other OpenID Connect / OAuth provider, then issues its own
tokens to your clients. This is standard ASP.NET Core external authentication — GPHosting.Identity
adds two constants that make the handshake between the external cookie and IdentityServer's own
session work correctly.

## Wiring one up

```csharp
using GPHosting.Identity;

builder.Services.AddAuthentication()
    .AddOpenIdConnect("google", "Google", options =>
    {
        options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;

        options.Authority = "https://accounts.google.com/";
        options.ClientId = "<your-google-client-id>";
        options.CallbackPath = "/signin-google";
        options.Scope.Add("email");
    });
```

`SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme` is the important part —
it tells the external OIDC handler to sign the user into a *temporary* external cookie rather than
directly establishing a local session. GPHosting.Identity's login page reads that external cookie,
maps the claims to a local user (creating one on first login, if that's your policy), and only
then establishes the real session. Skipping this and using the default sign-in scheme bypasses
that mapping step entirely.

For providers that need to trigger single sign-out, also set `SignOutScheme`:

```csharp
.AddOpenIdConnect("aad", "Azure AD", options =>
{
    options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
    options.SignOutScheme = IdentityServerConstants.SignoutScheme;

    options.Authority = "https://login.microsoftonline.com/<tenant-id>/v2.0";
    options.ClientId = "<your-app-registration-client-id>";
    options.ResponseType = "code";
    options.CallbackPath = "/signin-aad";
    options.SignedOutCallbackPath = "/signout-callback-aad";
})
```

## Persisting the `state` parameter across load-balanced instances

If you're running more than one instance behind a load balancer (see
[Deployment](./deployment)), the OIDC `state` parameter — used to correlate the callback with the
original request — needs to be readable by whichever instance handles the callback, not just the
one that started the request:

```csharp
services.AddOidcStateDataFormatterCache("google", "aad"); // scheme names you've registered
```

This stores `state` in `IDistributedCache` instead of in-process memory. Requires a distributed
cache configured (Redis, SQL Server, etc.) — without it, external logins will intermittently fail
with an invalid `state` error whenever the callback lands on a different instance than the one
that issued the challenge.

## Mapping external claims to your user store

The actual "look up or create a local user for this external identity" logic isn't provided by
GPHosting.Identity — it happens in your login page, reading from the external cookie
(`HttpContext.AuthenticateAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme)`) and
writing to whatever user store you're using (see [Stores](/docs/fundamentals/stores) if that's a
database, or [`GPHosting.Identity.AspNetIdentity`](/docs/getting-started/installation) if you're
using ASP.NET Core Identity). The samples under `samples/Quickstarts/` include a working example
of this mapping logic in their login page implementation.

## Checklist if it's not working

- Confirm `SignInScheme` is `ExternalCookieAuthenticationScheme`, not the default — this is the
  single most common misconfiguration, and it fails silently (login "succeeds" but the user never
  actually gets signed into GPHosting.Identity's session).
- If callbacks intermittently fail behind a load balancer, check whether you need
  `AddOidcStateDataFormatterCache` (see above).
- `CallbackPath` must be registered as a valid redirect URI with the *external* provider (Google,
  Azure AD, etc.) — this is a different registration from anything in GPHosting.Identity's own
  `Client` configuration.

## Next steps

- [Login and consent UI](/docs/getting-started/login-and-consent) — where the external-cookie
  mapping logic actually lives
- [Deployment](./deployment) — running behind a load balancer, relevant to the `state` caching
  above
