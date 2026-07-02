// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityModel;
using GPHosting.Identity.Configuration;
using GPHosting.Identity.Endpoints.Results;
using GPHosting.Identity.Events;
using GPHosting.Identity.Extensions;
using GPHosting.Identity.Hosting;
using GPHosting.Identity.Logging.Models;
using GPHosting.Identity.Models;
using GPHosting.Identity.ResponseHandling;
using GPHosting.Identity.Services;
using GPHosting.Identity.Telemetry;
using GPHosting.Identity.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GPHosting.Identity.Endpoints;
internal abstract class AuthorizeEndpointBase : IEndpointHandler
{
    private readonly IAuthorizeResponseGenerator _authorizeResponseGenerator;

    private readonly IEventService _events;
    private readonly IdentityServerOptions _options;

    private readonly IAuthorizeInteractionResponseGenerator _interactionGenerator;

    private readonly IAuthorizeRequestValidator _validator;

    protected AuthorizeEndpointBase(
        IEventService events,
        ILogger<AuthorizeEndpointBase> logger,
        IdentityServerOptions options,
        IAuthorizeRequestValidator validator,
        IAuthorizeInteractionResponseGenerator interactionGenerator,
        IAuthorizeResponseGenerator authorizeResponseGenerator,
        IUserSession userSession)
    {
        _events = events;
        _options = options;
        Logger = logger;
        _validator = validator;
        _interactionGenerator = interactionGenerator;
        _authorizeResponseGenerator = authorizeResponseGenerator;
        UserSession = userSession;
    }

    protected ILogger Logger { get; private set; }

    protected IUserSession UserSession { get; private set; }

    public abstract Task<IEndpointResult> ProcessAsync(HttpContext context);

    internal async Task<IEndpointResult> ProcessAuthorizeRequestAsync(NameValueCollection parameters, ClaimsPrincipal user, ConsentResponse consent)
    {
        using var activity = IdentityServerActivitySource.Source.StartActivity("AuthorizeEndpoint.ProcessRequest", ActivityKind.Server);

        if (user != null)
        {
            Logger.LogDebug("User in authorize request: {subjectId}", user.GetSubjectId());
        }
        else
        {
            Logger.LogDebug("No user present in authorize request");
        }

        // validate request
        var result = await _validator.ValidateAsync(parameters, user);
        if (result.IsError)
        {
            RecordOutcome("error", result.ValidatedRequest?.ClientId, activity, result.Error);
            return await CreateErrorResultAsync(
                "Request validation failed",
                result.ValidatedRequest,
                result.Error,
                result.ErrorDescription);
        }

        var request = result.ValidatedRequest;
        activity?.SetTag("identityserver.client_id", request.ClientId);
        LogRequest(request);

        // determine user interaction
        var interactionResult = await _interactionGenerator.ProcessInteractionAsync(request, consent);
        if (interactionResult.IsError)
        {
            RecordOutcome("error", request.ClientId, activity, interactionResult.Error);
            return await CreateErrorResultAsync("Interaction generator error", request, interactionResult.Error, interactionResult.ErrorDescription, false);
        }
        if (interactionResult.IsLogin)
        {
            RecordOutcome("login", request.ClientId, activity);
            return new LoginPageResult(request);
        }
        if (interactionResult.IsConsent)
        {
            RecordOutcome("consent", request.ClientId, activity);
            return new ConsentPageResult(request);
        }
        if (interactionResult.IsRedirect)
        {
            RecordOutcome("redirect", request.ClientId, activity);
            return new CustomRedirectResult(request, interactionResult.RedirectUrl);
        }

        var response = await _authorizeResponseGenerator.CreateResponseAsync(request);

        await RaiseResponseEventAsync(response);

        LogResponse(response);
        RecordOutcome(response.IsError ? "error" : "success", request.ClientId, activity, response.Error);

        return new AuthorizeResult(response);
    }

    private static void RecordOutcome(string outcome, string clientId, Activity activity, string error = null)
    {
        if (error != null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, error);
            activity?.SetTag("identityserver.error", error);
        }
        else if (outcome == "success")
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
        }

        IdentityServerMetrics.AuthorizeRequests.Add(1, new TagList { { "outcome", outcome }, { "client_id", clientId ?? "unknown" } });
    }

    protected async Task<IEndpointResult> CreateErrorResultAsync(
        string logMessage,
        ValidatedAuthorizeRequest request = null,
        string error = OidcConstants.AuthorizeErrors.ServerError,
        string errorDescription = null,
        bool logError = true)
    {
        if (logError)
        {
            Logger.LogError(logMessage);
        }

        if (request != null)
        {
            var details = new AuthorizeRequestValidationLog(request, _options.Logging.AuthorizeRequestSensitiveValuesFilter);
            Logger.LogInformation("{@validationDetails}", details);
        }

        // TODO: should we raise a token failure event for all errors to the authorize endpoint?
        await RaiseFailureEventAsync(request, error, errorDescription);

        return new AuthorizeResult(new AuthorizeResponse
        {
            Request = request,
            Error = error,
            ErrorDescription = errorDescription,
            SessionState = request?.GenerateSessionStateValue()
        });
    }

    private void LogRequest(ValidatedAuthorizeRequest request)
    {
        var details = new AuthorizeRequestValidationLog(request, _options.Logging.AuthorizeRequestSensitiveValuesFilter);
        Logger.LogDebug(nameof(ValidatedAuthorizeRequest) + Environment.NewLine + "{@validationDetails}", details);
    }

    private void LogResponse(AuthorizeResponse response)
    {
        var details = new AuthorizeResponseLog(response);
        Logger.LogDebug("Authorize endpoint response" + Environment.NewLine + "{@details}", details);
    }

    private void LogTokens(AuthorizeResponse response)
    {
        var clientId = $"{response.Request.ClientId} ({response.Request.Client.ClientName ?? "no name set"})";
        var subjectId = response.Request.Subject.GetSubjectId();

        if (response.IdentityToken != null)
        {
            Logger.LogTrace("Identity token issued for {clientId} / {subjectId}: {token}", clientId, subjectId, response.IdentityToken);
        }
        if (response.Code != null)
        {
            Logger.LogTrace("Code issued for {clientId} / {subjectId}: {token}", clientId, subjectId, response.Code);
        }
        if (response.AccessToken != null)
        {
            Logger.LogTrace("Access token issued for {clientId} / {subjectId}: {token}", clientId, subjectId, response.AccessToken);
        }
    }

    private Task RaiseFailureEventAsync(ValidatedAuthorizeRequest request, string error, string errorDescription)
    {
        return _events.RaiseAsync(new TokenIssuedFailureEvent(request, error, errorDescription));
    }

    private Task RaiseResponseEventAsync(AuthorizeResponse response)
    {
        if (!response.IsError)
        {
            LogTokens(response);
            return _events.RaiseAsync(new TokenIssuedSuccessEvent(response));
        }

        return RaiseFailureEventAsync(response.Request, response.Error, response.ErrorDescription);
    }
}
