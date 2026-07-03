---
sidebar_position: 7
---

# Extensibility

GPHosting.Identity's default behavior covers standard OIDC/OAuth 2.0, but real deployments
usually need to hook in custom logic somewhere: pulling extra claims from your own user table,
supporting a non-standard grant type, or validating extra business rules on a token request.
These are the four extension points you'll reach for most often.

## `IProfileService` — controlling what claims end up in tokens

Called whenever claims about the user are needed — during token issuance and via the `/connect/userinfo`
endpoint. This is the most commonly implemented extension point; without it, `AddTestUsers()`'s
default profile service is the only thing supplying claims.

```csharp
public class MyProfileService : IProfileService
{
    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var user = await _users.FindByIdAsync(context.Subject.GetSubjectId());

        context.IssuedClaims.AddRange(
            context.RequestedClaimTypes
                .Where(claimType => user.HasClaim(claimType))
                .Select(claimType => new Claim(claimType, user.GetClaimValue(claimType))));
    }

    public async Task IsActiveAsync(IsActiveContext context)
    {
        var user = await _users.FindByIdAsync(context.Subject.GetSubjectId());
        context.IsActive = user is { IsDisabled: false };
    }
}
```

```csharp
builder.Services.AddIdentityServer()
    .AddProfileService<MyProfileService>();
```

`GetProfileDataAsync` only needs to add claims present in `context.RequestedClaimTypes` — adding
claims outside that list wastes token space without the client ever asking for them.
`IsActiveAsync` is checked on every token request, not just login — this is where a deactivated
account gets locked out immediately rather than just at next login, so keep it fast (this runs on
the hot path).

## `IExtensionGrantValidator` — custom grant types

For a grant type outside the OAuth standard set — a legacy SSO token exchange, a proprietary
mobile SDK's auth mechanism, anything that doesn't fit authorization code or client credentials:

```csharp
public class MyCustomGrantValidator : IExtensionGrantValidator
{
    public string GrantType => "my_custom_grant";

    public Task ValidateAsync(ExtensionGrantValidationContext context)
    {
        var credential = context.Request.Raw.Get("custom_credential");

        context.Result = IsValid(credential)
            ? new GrantValidationResult(subject: LookupSubjectId(credential), authenticationMethod: GrantType)
            : new GrantValidationResult(TokenRequestErrors.InvalidGrant, "invalid custom credential");

        return Task.CompletedTask;
    }
}
```

```csharp
builder.Services.AddIdentityServer()
    .AddExtensionGrantValidator<MyCustomGrantValidator>();
```

The client requesting this grant needs `grant_type: "my_custom_grant"` in its
`AllowedGrantTypes` — a raw string, since it's outside `GrantTypes`'s standard set:

```csharp
new Client { AllowedGrantTypes = { "my_custom_grant" } }
```

## `ICustomTokenRequestValidator` — extra validation on every token request

Runs after standard token request validation, across *every* grant type — the place for
business rules that don't fit neatly into a grant validator (rate limiting per-client, checking
an external allowlist, enforcing tenant-specific policy):

```csharp
public class MyTokenRequestValidator : ICustomTokenRequestValidator
{
    public Task ValidateAsync(CustomTokenRequestValidationContext context)
    {
        if (IsRateLimited(context.Result.ValidatedRequest.Client.ClientId))
        {
            context.Result.IsError = true;
            context.Result.Error = TokenRequestErrors.InvalidGrant;
        }

        return Task.CompletedTask;
    }
}
```

```csharp
builder.Services.AddIdentityServer()
    .AddCustomTokenRequestValidator<MyTokenRequestValidator>();
```

## `IScopeParser` — non-standard scope syntax

Only needed if you're using parameterized scopes (e.g. `transaction:{id}` where the id varies per
request) — the default parser expects a flat list of scope strings and won't understand a custom
syntax on top of that:

```csharp
public class MyScopeParser : IScopeParser
{
    public ParsedScopesResult ParseScopeValues(IEnumerable<string> scopeValues)
    {
        // split "transaction:123" into a recognized scope name + a parameter, rather than
        // treating it as an unrecognized literal scope
    }
}
```

```csharp
builder.Services.AddIdentityServer()
    .AddScopeParser<MyScopeParser>();
```

Most deployments never need this — reach for it only if a fixed scope list genuinely can't
express what you need (dynamic per-resource permissions being the common case).

## Next steps

- [Stores](/docs/fundamentals/stores) — the other major extension point (custom `IClientStore`/
  `IResourceStore` implementations), covered separately since it's less "extra logic" and more
  "where configuration lives"
- [Resources](./resources) — how requested scopes normally map to claims, which `IProfileService`
  and `IScopeParser` both interact with
