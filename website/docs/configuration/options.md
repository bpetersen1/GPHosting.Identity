---
sidebar_position: 1
---

# IdentityServerOptions reference

Server-wide behavior is configured via the options delegate on `AddIdentityServer()`:

```csharp
builder.Services.AddIdentityServer(options =>
{
    options.IssuerUri = "https://identity.example.com";
    options.Events.RaiseSuccessEvents = true;
    options.Events.RaiseFailureEvents = true;
    options.Events.RaiseErrorEvents = true;
});
```

This page covers the options you're most likely to need. Everything here lives on
`IdentityServerOptions`, grouped into nested option classes by concern.

## Top-level options

| Option | Default | Purpose |
|---|---|---|
| `IssuerUri` | inferred from request | The `iss` claim value and discovery document issuer. Set explicitly in production — inferring it from the incoming request works for a single instance behind no proxy, but breaks the moment you're behind a load balancer or the request host varies. |
| `LowerCaseIssuerUri` | `true` | Normalizes the inferred/configured issuer to lowercase. |
| `AccessTokenJwtType` | `"at+jwt"` | The `typ` header on access tokens. `at+jwt` follows RFC 9068 (JWT access tokens) — only change this if you have a specific interop reason to. |
| `EmitStaticAudienceClaim` | `false` | Adds an `aud` claim shaped `issuer/resources` for older validation middleware that expects it. Modern access token validation doesn't need this. |
| `EmitScopesAsSpaceDelimitedStringInJwt` | `false` | Emits `scope` as a single space-delimited string instead of a JSON array — for clients whose JWT parsing expects the older string format. |

## `Events`

Controls which categories of events IdentityServer raises through its eventing system (consumed
via `IEventSink` — the default sink just logs, but you can register your own to forward events to
a SIEM, audit log, or telemetry pipeline):

```csharp
options.Events.RaiseSuccessEvents = true;
options.Events.RaiseFailureEvents = true;
options.Events.RaiseInformationEvents = true;
options.Events.RaiseErrorEvents = true;
```

All four default to `false`. Turn on at least `RaiseFailureEvents` and `RaiseErrorEvents` in
production — they're your primary signal for things like repeated failed client authentication or
invalid grant attempts. See also [Observability](/docs/observability/opentelemetry) for the
separate `ActivitySource`/`Meter`-based tracing and metrics, which are independent of this eventing
system.

## `Authentication`

Governs the cookie IdentityServer issues after a user signs in through your login UI:

```csharp
options.Authentication.CookieLifetime = TimeSpan.FromHours(10);
options.Authentication.CookieSlidingExpiration = false;
options.Authentication.CookieSameSiteMode = SameSiteMode.None;
options.Authentication.RequireCspFrameSrcForSignout = true;
```

`CookieSameSiteMode` defaults to `None`, which is required for the front-channel/back-channel
logout iframe patterns to work across sites — if you tighten this, verify your logout flows still
work, since `SameSite=Strict`/`Lax` cookies won't be sent in the cross-site iframe requests logout
relies on.

## `UserInteraction`

Points IdentityServer at your login, logout, consent, and error UI routes — these are relative
URLs on your own application, since GPHosting.Identity doesn't ship a UI:

```csharp
options.UserInteraction.LoginUrl = "/Account/Login";
options.UserInteraction.LoginReturnUrlParameter = "ReturnUrl";
options.UserInteraction.LogoutUrl = "/Account/Logout";
options.UserInteraction.ConsentUrl = "/Consent";
options.UserInteraction.ErrorUrl = "/Home/Error";
```

If you're using one of the quickstart UI samples, these already match its routes — you only need
to set these if your login/consent pages live somewhere else.

## `InputLengthRestrictions`

Caps the length of every user-supplied input field IdentityServer parses (`ClientId`, `Scope`,
`RedirectUri`, `UserName`, `Jwt`, etc.), defaulting to conservative values (e.g. 300 characters for
`Scope`, 51,200 for `Jwt`). This exists specifically to bound the cost of parsing/validating
attacker-supplied input — you generally shouldn't need to raise these, but if a legitimate request
is being rejected for exceeding one (check logs for a length-restriction validation error), you can
override the specific field:

```csharp
options.InputLengthRestrictions.Scope = 500;
```

## `Cors` / `Csp`

`Cors` controls which origins can call IdentityServer's endpoints cross-origin (relevant for
SPAs using the authorization code + PKCE flow directly from the browser). `Csp` controls the
Content-Security-Policy header IdentityServer sets on its own UI-adjacent responses (consent,
error pages) — both default to reasonably strict settings; loosen only with a specific,
understood reason.

## `DeviceFlow`

```csharp
options.DeviceFlow.DefaultUserCodeType = IdentityServerConstants.UserCodeTypes.Numeric; // vs. Alphabetic
options.DeviceFlow.Interval = 5; // seconds between allowed polling attempts
```

`DefaultUserCodeType` controls the shape of the user-facing code in the
[device flow](/docs/fundamentals/flows#device-flow-granttypesdeviceflow) — numeric codes are
easier to type on a remote control, alphabetic codes have a larger keyspace for the same length.

## `MutualTls`

```csharp
options.MutualTls.DomainName = "mtls";
```

Enables mutual-TLS client authentication and certificate-bound access tokens (RFC 8705) — set
`DomainName` to the subdomain your mTLS endpoint listens on (e.g. requests to
`mtls.identity.example.com` are treated as the mTLS-authenticated variant of each endpoint).
Leave unset unless you specifically need certificate-bound tokens.

## Next steps

- [Observability](/docs/observability/opentelemetry) — the separate tracing/metrics/health-check
  configuration, not part of `IdentityServerOptions`
- [Stores](/docs/fundamentals/stores) — configuring `ConfigurationStoreOptions` /
  `OperationalStoreOptions`, which are separate from the options covered here
