// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System.Threading.Tasks;
using GPHosting.Identity.Models;
using GPHosting.Identity.Extensions;
using GPHosting.Identity.Hosting;
using IdentityModel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using GPHosting.Identity.Services;
using GPHosting.Identity.Configuration;
using GPHosting.Identity.Stores;
using GPHosting.Identity.ResponseHandling;
using Microsoft.AspNetCore.Authentication;
using System.Text.Encodings.Web;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;

namespace GPHosting.Identity.Endpoints.Results;
internal class AuthorizeResult : IEndpointResult
{
    private const int JarmResponseLifetimeSeconds = 60;

    public AuthorizeResponse Response { get; }

    public AuthorizeResult(AuthorizeResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        Response = response;
    }

    internal AuthorizeResult(
        AuthorizeResponse response,
        IdentityServerOptions options,
        IUserSession userSession,
        IMessageStore<ErrorMessage> errorMessageStore,
        TimeProvider clock)
        : this(response)
    {
        _options = options;
        _userSession = userSession;
        _errorMessageStore = errorMessageStore;
        _clock = clock;
    }

    private IdentityServerOptions _options;
    private IUserSession _userSession;
    private IMessageStore<ErrorMessage> _errorMessageStore;
    private TimeProvider _clock;

    private void Init(HttpContext context)
    {
        _options = _options ?? context.RequestServices.GetRequiredService<IdentityServerOptions>();
        _userSession = _userSession ?? context.RequestServices.GetRequiredService<IUserSession>();
        _errorMessageStore = _errorMessageStore ?? context.RequestServices.GetRequiredService<IMessageStore<ErrorMessage>>();
        _clock = _clock ?? context.RequestServices.GetRequiredService<TimeProvider>();
    }

    public async Task ExecuteAsync(HttpContext context)
    {
        Init(context);

        if (Response.IsError)
        {
            await ProcessErrorAsync(context);
        }
        else
        {
            await ProcessResponseAsync(context);
        }
    }

    private async Task ProcessErrorAsync(HttpContext context)
    {
        // these are the conditions where we can send a response 
        // back directly to the client, otherwise we're only showing the error UI
        var isSafeError =
            Response.Error == OidcConstants.AuthorizeErrors.AccessDenied ||
            Response.Error == OidcConstants.AuthorizeErrors.AccountSelectionRequired ||
            Response.Error == OidcConstants.AuthorizeErrors.LoginRequired ||
            Response.Error == OidcConstants.AuthorizeErrors.ConsentRequired ||
            Response.Error == OidcConstants.AuthorizeErrors.InteractionRequired;

        if (isSafeError)
        {
            // this scenario we can return back to the client
            await ProcessResponseAsync(context);
        }
        else
        {
            // we now know we must show error page
            await RedirectToErrorPageAsync(context);
        }
    }

    protected async Task ProcessResponseAsync(HttpContext context)
    {
        if (!Response.IsError)
        {
            // success response -- track client authorization for sign-out
            //_logger.LogDebug("Adding client {0} to client list cookie for subject {1}", request.ClientId, request.Subject.GetSubjectId());
            await _userSession.AddClientIdAsync(Response.Request.ClientId);
        }

        await RenderAuthorizeResponseAsync(context);
    }

    private async Task RenderAuthorizeResponseAsync(HttpContext context)
    {
        var responseMode = Response.Request.ResponseMode;
        NameValueCollection nvc;
        string deliveryMode;

        if (Constants.JarmResponseModes.All.Contains(responseMode))
        {
            var jwt = await CreateJarmResponseAsync(context);
            nvc = new NameValueCollection { { "response", jwt } };
            deliveryMode = ResolveJarmDeliveryMode(responseMode);
        }
        else
        {
            nvc = Response.ToNameValueCollection();
            deliveryMode = responseMode;
        }

        if (deliveryMode == OidcConstants.ResponseModes.Query ||
            deliveryMode == OidcConstants.ResponseModes.Fragment)
        {
            context.Response.SetNoCache();
            context.Response.Redirect(BuildRedirectUri(nvc, deliveryMode));
        }
        else if (deliveryMode == OidcConstants.ResponseModes.FormPost)
        {
            context.Response.SetNoCache();
            AddSecurityHeaders(context);
            await context.Response.WriteHtmlAsync(GetFormPostHtml(nvc));
        }
        else
        {
            //_logger.LogError("Unsupported response mode.");
            throw new InvalidOperationException("Unsupported response mode");
        }
    }

    /// <summary>
    /// The response mode a bare "jwt" resolves to per response_type, when the client didn't pick
    /// one of the explicit query.jwt/fragment.jwt/form_post.jwt variants (JARM).
    /// </summary>
    private string ResolveJarmDeliveryMode(string responseMode)
    {
        if (responseMode == Constants.JarmResponseModes.QueryJwt) return OidcConstants.ResponseModes.Query;
        if (responseMode == Constants.JarmResponseModes.FragmentJwt) return OidcConstants.ResponseModes.Fragment;
        if (responseMode == Constants.JarmResponseModes.FormPostJwt) return OidcConstants.ResponseModes.FormPost;

        // bare "jwt": code flow defaults to query, anything carrying a token/id_token in the
        // response (implicit, hybrid) defaults to fragment, matching the plain (non-JARM) defaults.
        return Response.Request.ResponseType == OidcConstants.ResponseTypes.Code
            ? OidcConstants.ResponseModes.Query
            : OidcConstants.ResponseModes.Fragment;
    }

    /// <summary>
    /// Wraps the authorize response parameters in a signed JWT (JARM — JWT Secured Authorization
    /// Response Mode), so the client can verify the response actually came from this server and
    /// wasn't tampered with or injected in transit.
    /// </summary>
    private async Task<string> CreateJarmResponseAsync(HttpContext context)
    {
        var keys = context.RequestServices.GetRequiredService<IKeyMaterialService>();
        var credential = await keys.GetSigningCredentialsAsync();
        if (credential == null)
        {
            throw new InvalidOperationException("No signing credential is configured. Can't create a JARM response.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var claims = new List<Claim>
        {
            new Claim(JwtClaimTypes.Issuer, context.GetIdentityServerIssuerUri()),
            new Claim(JwtClaimTypes.Audience, Response.Request.ClientId)
        };

        var responseParameters = Response.ToNameValueCollection();
        foreach (var key in responseParameters.AllKeys)
        {
            if (key != null)
            {
                claims.Add(new Claim(key, responseParameters[key]));
            }
        }

        var jwt = new JwtSecurityToken(
            signingCredentials: credential,
            claims: claims,
            notBefore: now,
            expires: now.AddSeconds(JarmResponseLifetimeSeconds));

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private void AddSecurityHeaders(HttpContext context)
    {
        context.Response.AddScriptCspHeaders(_options.Csp, "sha256-orD0/VhH8hLqrLxKHD/HUEMdwqX6/0ve7c5hspX5VJ8=");

        if (!context.Response.Headers.ContainsKey("Referrer-Policy"))
        {
            context.Response.Headers.Append("Referrer-Policy", "no-referrer");
        }

        if (!context.Response.Headers.ContainsKey("X-Frame-Options"))
        {
            context.Response.Headers.Append("X-Frame-Options", "DENY");
        }
    }

    private string BuildRedirectUri() => BuildRedirectUri(Response.ToNameValueCollection(), Response.Request.ResponseMode);

    private string BuildRedirectUri(NameValueCollection nvc, string deliveryMode)
    {
        var uri = Response.RedirectUri;
        var query = nvc.ToQueryString();

        if (deliveryMode == OidcConstants.ResponseModes.Query)
        {
            uri = uri.AddQueryString(query);
        }
        else
        {
            uri = uri.AddHashFragment(query);
        }

        if (Response.IsError && !uri.Contains("#"))
        {
            // https://tools.ietf.org/html/draft-bradley-oauth-open-redirector-00
            uri += "#_=_";
        }

        return uri;
    }

    private const string FormPostHtml = "<html><head><meta http-equiv='X-UA-Compatible' content='IE=edge' /><base target='_self'/></head><body><form method='post' action='{uri}'>{body}<noscript><button>Click to continue</button></noscript></form><script>window.addEventListener('load', function(){document.forms[0].submit();});</script></body></html>";

    private string GetFormPostHtml(NameValueCollection nvc)
    {
        var html = FormPostHtml;

        var url = Response.Request.RedirectUri;
        url = HtmlEncoder.Default.Encode(url);
        html = html.Replace("{uri}", url);
        html = html.Replace("{body}", nvc.ToFormPost());

        return html;
    }

    private async Task RedirectToErrorPageAsync(HttpContext context)
    {
        var errorModel = new ErrorMessage
        {
            RequestId = context.TraceIdentifier,
            Error = Response.Error,
            ErrorDescription = Response.ErrorDescription,
            UiLocales = Response.Request?.UiLocales,
            DisplayMode = Response.Request?.DisplayMode,
            ClientId = Response.Request?.ClientId
        };

        if (Response.RedirectUri != null && Response.Request?.ResponseMode != null)
        {
            // if we have a valid redirect uri, then include it to the error page
            errorModel.RedirectUri = BuildRedirectUri();
            errorModel.ResponseMode = Response.Request.ResponseMode;
        }

        var message = new Message<ErrorMessage>(errorModel, _clock.GetUtcNow().UtcDateTime);
        var id = await _errorMessageStore.WriteAsync(message);

        var errorUrl = _options.UserInteraction.ErrorUrl;

        var url = errorUrl.AddQueryString(_options.UserInteraction.ErrorIdParameter, id);
        context.Response.RedirectToAbsoluteUrl(url);
    }
}
