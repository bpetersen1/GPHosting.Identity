---
sidebar_position: 5
---

# Login and consent UI

GPHosting.Identity deliberately ships **no** login or consent pages — `UseIdentityServer()` only
serves the protocol endpoints (`/connect/token`, `/connect/authorize`, discovery, JWKS). The
actual "enter your username and password" and "this app wants access to..." pages are regular
MVC/Razor Pages controllers and views in *your* host application, wired together through one
interface: `IIdentityServerInteractionService`.

Why isn't this built in? Login UX varies enormously between deployments — 2FA, external providers,
custom branding, password-less flows — baking in an opinionated UI would mean fighting it in every
real deployment. Instead, the interaction service gives you everything you need to build exactly
the UI your product requires.

## The login page

```csharp
public class AccountController : Controller
{
    private readonly IIdentityServerInteractionService _interaction;

    [HttpGet]
    public async Task<IActionResult> Login(string returnUrl)
    {
        // returnUrl came from the authorize request that redirected here — pass it through
        // your login form as a hidden field so POST can use it too.
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginInputModel model)
    {
        // Confirms returnUrl is a legitimate, still-valid authorize request — don't skip this,
        // it's what prevents an attacker crafting a fake returnUrl to redirect a login elsewhere.
        var context = await _interaction.GetAuthorizationContextAsync(model.ReturnUrl);

        if (!IsValidCredentials(model.Username, model.Password))
        {
            ModelState.AddModelError("", "Invalid username or password");
            return View(model);
        }

        var isuser = new IdentityServerUser(subjectId: LookupUserId(model.Username))
        {
            DisplayName = model.Username
        };

        await HttpContext.SignInAsync(isuser, new AuthenticationProperties());

        return Redirect(model.ReturnUrl); // safe — validated by GetAuthorizationContextAsync above
    }
}
```

The two calls that matter: `GetAuthorizationContextAsync(returnUrl)` validates that `returnUrl`
actually corresponds to a pending authorize request (not just any URL — this is your open-redirect
guard on the login page itself), and `HttpContext.SignInAsync(isuser, ...)` establishes the actual
IdentityServer session using the standard ASP.NET Core authentication API, just with
`IdentityServerUser` as the principal wrapper.

## The consent page

Only reached for clients that don't have `RequireConsent = false` set, and only for scopes the
user hasn't already granted:

```csharp
public class ConsentController : Controller
{
    private readonly IIdentityServerInteractionService _interaction;

    [HttpGet]
    public async Task<IActionResult> Index(string returnUrl)
    {
        var request = await _interaction.GetAuthorizationContextAsync(returnUrl);
        // build a view model from request.ValidatedResources.RawScopeValues, request.Client, etc.
        return View(BuildConsentViewModel(request));
    }

    [HttpPost]
    public async Task<IActionResult> Index(ConsentInputModel model)
    {
        var request = await _interaction.GetAuthorizationContextAsync(model.ReturnUrl);

        var grantedConsent = model.Button == "yes"
            ? new ConsentResponse { ScopesValuesConsented = model.ScopesConsented.ToArray() }
            : new ConsentResponse { Error = AuthorizationError.AccessDenied };

        await _interaction.GrantConsentAsync(request, grantedConsent);

        return Redirect(model.ReturnUrl);
    }
}
```

`GrantConsentAsync` persists the user's decision so the authorize flow can resume and actually
issue the code/tokens for the originally requested scopes.

## Don't write this from scratch

The exact controllers, view models, and Razor views above (full versions, with 2FA hooks, remember-me,
external provider buttons, and all the edge cases) live in
`src/GPHosting.Identity/host/Quickstart/Account/` and
`src/GPHosting.Identity/host/Quickstart/Consent/` in this repo — copy them into your own host
project as a starting point and adapt the branding/business logic, rather than reimplementing the
interaction service calls yourself. This mirrors the original IdentityServer4 "quickstart UI"
pattern.

## Next steps

- [Adding your first client](./first-client) — the client-side configuration that redirects here
- [External identity providers](/docs/operations/external-identity-providers) — routing login
  through Google/Azure AD/ADFS instead of (or alongside) local username/password
