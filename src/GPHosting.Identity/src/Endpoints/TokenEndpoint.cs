// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using IdentityModel;
using GPHosting.Identity.Endpoints.Results;
using GPHosting.Identity.Events;
using GPHosting.Identity.Extensions;
using GPHosting.Identity.Hosting;
using GPHosting.Identity.ResponseHandling;
using GPHosting.Identity.Services;
using GPHosting.Identity.Telemetry;
using GPHosting.Identity.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using GPHosting.Identity.Configuration;

namespace GPHosting.Identity.Endpoints;
/// <summary>
/// The token endpoint
/// </summary>
/// <seealso cref="GPHosting.Identity.Hosting.IEndpointHandler" />
internal class TokenEndpoint : IEndpointHandler
{
    private readonly IClientSecretValidator _clientValidator;
    private readonly ITokenRequestValidator _requestValidator;
    private readonly ITokenResponseGenerator _responseGenerator;
    private readonly IEventService _events;
    private readonly IDPoPProofValidator _dPoPValidator;
    private readonly ILogger _logger;

    public TokenEndpoint(
        IClientSecretValidator clientValidator,
        ITokenRequestValidator requestValidator,
        ITokenResponseGenerator responseGenerator,
        IEventService events,
        IDPoPProofValidator dPoPValidator,
        ILogger<TokenEndpoint> logger)
    {
        _clientValidator = clientValidator;
        _requestValidator = requestValidator;
        _responseGenerator = responseGenerator;
        _events = events;
        _dPoPValidator = dPoPValidator;
        _logger = logger;
    }

    /// <summary>
    /// Processes the request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns></returns>
    public async Task<IEndpointResult> ProcessAsync(HttpContext context)
    {
        _logger.LogTrace("Processing token request.");

        // validate HTTP
        if (!HttpMethods.IsPost(context.Request.Method) || !context.Request.HasApplicationFormContentType())
        {
            _logger.LogWarning("Invalid HTTP request for token endpoint");
            return Error(OidcConstants.TokenErrors.InvalidRequest);
        }

        return await ProcessTokenRequestAsync(context);
    }

    private async Task<IEndpointResult> ProcessTokenRequestAsync(HttpContext context)
    {
        using var activity = IdentityServerActivitySource.Source.StartActivity("TokenEndpoint.ProcessRequest", ActivityKind.Server);

        _logger.LogDebug("Start token request.");

        // validate client
        var clientResult = await _clientValidator.ValidateAsync(context);

        if (clientResult.Client == null)
        {
            return Error(OidcConstants.TokenErrors.InvalidClient, activity: activity);
        }

        activity?.SetTag("identityserver.client_id", clientResult.Client.ClientId);

        // validate request
        var form = (await context.Request.ReadFormAsync()).AsNameValueCollection();
        _logger.LogTrace("Calling into token request validator: {type}", _requestValidator.GetType().FullName);
        var requestResult = await _requestValidator.ValidateRequestAsync(form, clientResult);

        activity?.SetTag("identityserver.grant_type", requestResult.ValidatedRequest?.GrantType);

        if (requestResult.IsError)
        {
            await _events.RaiseAsync(new TokenIssuedFailureEvent(requestResult));
            return Error(requestResult.Error, requestResult.ErrorDescription, requestResult.CustomResponse, clientResult.Client.ClientId, requestResult.ValidatedRequest?.GrantType, activity);
        }

        // validate DPoP proof if the client sent one
        var dPopProof = context.Request.Headers[IdentityServerConstants.DPoP.ProofTokenHeader].FirstOrDefault();
        if (dPopProof != null)
        {
            var requestUrl = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}";
            var dPopResult = await _dPoPValidator.ValidateAsync(new DPoPProofValidationContext
            {
                ProofToken = dPopProof,
                HttpMethod = context.Request.Method,
                HttpUrl = requestUrl
            });

            if (dPopResult.IsError)
            {
                _logger.LogWarning("DPoP proof validation failed: {error} — {description}", dPopResult.Error, dPopResult.ErrorDescription);
                return Error(dPopResult.Error, dPopResult.ErrorDescription, clientId: clientResult.Client.ClientId, grantType: requestResult.ValidatedRequest?.GrantType, activity: activity);
            }

            requestResult.ValidatedRequest.DPoPConfirmation = dPopResult.Confirmation;
            _logger.LogDebug("DPoP proof validated. cnf={confirmation}", dPopResult.Confirmation);
        }

        // create response
        _logger.LogTrace("Calling into token request response generator: {type}", _responseGenerator.GetType().FullName);
        var response = await _responseGenerator.ProcessAsync(requestResult);

        await _events.RaiseAsync(new TokenIssuedSuccessEvent(response, requestResult));
        LogTokens(response, requestResult);
        RecordTokensIssued(response, requestResult);

        // return result
        _logger.LogDebug("Token request success.");
        activity?.SetStatus(ActivityStatusCode.Ok);
        return new TokenResult(response);
    }

    private TokenErrorResult Error(string error, string errorDescription = null, Dictionary<string, object> custom = null, string clientId = null, string grantType = null, Activity activity = null)
    {
        var response = new TokenErrorResponse
        {
            Error = error,
            ErrorDescription = errorDescription,
            Custom = custom
        };

        activity?.SetStatus(ActivityStatusCode.Error, error);
        activity?.SetTag("identityserver.error", error);

        var tags = new TagList
        {
            { "error", error },
            { "client_id", clientId ?? "unknown" },
            { "grant_type", grantType ?? "unknown" }
        };
        IdentityServerMetrics.TokenRequestErrors.Add(1, tags);

        return new TokenErrorResult(response);
    }

    private static void RecordTokensIssued(TokenResponse response, TokenRequestValidationResult requestResult)
    {
        var clientId = requestResult.ValidatedRequest.Client?.ClientId ?? "unknown";
        var grantType = requestResult.ValidatedRequest.GrantType ?? "unknown";

        if (response.IdentityToken != null)
        {
            IdentityServerMetrics.TokensIssued.Add(1, new TagList { { "client_id", clientId }, { "grant_type", grantType }, { "token_type", "identity_token" } });
        }
        if (response.RefreshToken != null)
        {
            IdentityServerMetrics.TokensIssued.Add(1, new TagList { { "client_id", clientId }, { "grant_type", grantType }, { "token_type", "refresh_token" } });
        }
        if (response.AccessToken != null)
        {
            IdentityServerMetrics.TokensIssued.Add(1, new TagList { { "client_id", clientId }, { "grant_type", grantType }, { "token_type", "access_token" } });
        }
    }

    private void LogTokens(TokenResponse response, TokenRequestValidationResult requestResult)
    {
        var clientId = $"{requestResult.ValidatedRequest.Client.ClientId} ({requestResult.ValidatedRequest.Client?.ClientName ?? "no name set"})";
        var subjectId = requestResult.ValidatedRequest.Subject?.GetSubjectId() ?? "no subject";

        if (response.IdentityToken != null)
        {
            _logger.LogTrace("Identity token issued for {clientId} / {subjectId}: {token}", clientId, subjectId, response.IdentityToken);
        }
        if (response.RefreshToken != null)
        {
            _logger.LogTrace("Refresh token issued for {clientId} / {subjectId}: {token}", clientId, subjectId, response.RefreshToken);
        }
        if (response.AccessToken != null)
        {
            _logger.LogTrace("Access token issued for {clientId} / {subjectId}: {token}", clientId, subjectId, response.AccessToken);
        }
    }
}
