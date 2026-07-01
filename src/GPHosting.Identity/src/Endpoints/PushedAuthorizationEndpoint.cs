// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using IdentityModel;
using GPHosting.Identity.Endpoints.Results;
using GPHosting.Identity.Extensions;
using GPHosting.Identity.Hosting;
using GPHosting.Identity.Models;
using GPHosting.Identity.Stores;
using GPHosting.Identity.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace GPHosting.Identity.Endpoints;

internal class PushedAuthorizationEndpoint : IEndpointHandler
{
    private const int ExpiresInSeconds = 60;
    private const string RequestUriPrefix = "urn:ietf:params:oauth:request_uri:";

    private readonly IClientSecretValidator _clientValidator;
    private readonly IPushedAuthorizationRequestStore _parStore;
    private readonly ILogger _logger;

    public PushedAuthorizationEndpoint(
        IClientSecretValidator clientValidator,
        IPushedAuthorizationRequestStore parStore,
        ILogger<PushedAuthorizationEndpoint> logger)
    {
        _clientValidator = clientValidator;
        _parStore = parStore;
        _logger = logger;
    }

    public async Task<IEndpointResult> ProcessAsync(HttpContext context)
    {
        _logger.LogTrace("Processing pushed authorization request.");

        if (!HttpMethods.IsPost(context.Request.Method))
        {
            _logger.LogWarning("Pushed authorization endpoint only supports POST requests");
            return new StatusCodeResult(HttpStatusCode.MethodNotAllowed);
        }

        if (!context.Request.HasApplicationFormContentType())
        {
            _logger.LogWarning("Invalid media type for pushed authorization endpoint");
            return new StatusCodeResult(HttpStatusCode.UnsupportedMediaType);
        }

        return await ProcessPushedAuthorizationRequestAsync(context);
    }

    private async Task<IEndpointResult> ProcessPushedAuthorizationRequestAsync(HttpContext context)
    {
        _logger.LogDebug("Starting pushed authorization request.");

        var clientResult = await _clientValidator.ValidateAsync(context);
        if (clientResult.Client == null)
        {
            _logger.LogWarning("Client authentication failed for pushed authorization request.");
            return Error(OidcConstants.TokenErrors.InvalidClient, "Client authentication failed");
        }

        var form = (await context.Request.ReadFormAsync()).AsNameValueCollection();

        var clientId = form[OidcConstants.AuthorizeRequest.ClientId];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("Pushed authorization request missing client_id parameter.");
            return Error(OidcConstants.AuthorizeErrors.InvalidRequest, "client_id is required");
        }

        if (!string.Equals(clientId, clientResult.Client.ClientId, StringComparison.Ordinal))
        {
            _logger.LogWarning("client_id in form does not match authenticated client.");
            return Error(OidcConstants.TokenErrors.InvalidClient, "client_id mismatch");
        }

        var handle = CryptoRandom.CreateUniqueId(16, CryptoRandom.OutputFormat.Hex);
        var requestUri = RequestUriPrefix + handle;

        var parameters = new Dictionary<string, string>();
        foreach (var key in form.AllKeys)
        {
            if (key != null)
                parameters[key] = form[key] ?? string.Empty;
        }

        var parRequest = new PushedAuthorizationRequest
        {
            ReferenceValue = handle,
            ExpiresAt = DateTime.UtcNow.AddSeconds(ExpiresInSeconds),
            Parameters = JsonSerializer.Serialize(parameters)
        };

        await _parStore.StoreAsync(parRequest);

        _logger.LogDebug("Pushed authorization request stored. request_uri: {requestUri}", requestUri);

        return new PushedAuthorizationSuccessResult(requestUri, ExpiresInSeconds);
    }

    private static BadRequestResult Error(string error, string errorDescription = null)
        => new BadRequestResult(error, errorDescription);

    private sealed class PushedAuthorizationSuccessResult : IEndpointResult
    {
        private readonly string _requestUri;
        private readonly int _expiresIn;

        public PushedAuthorizationSuccessResult(string requestUri, int expiresIn)
        {
            _requestUri = requestUri;
            _expiresIn = expiresIn;
        }

        public async Task ExecuteAsync(HttpContext context)
        {
            context.Response.StatusCode = 201;
            context.Response.SetNoCache();

            var dto = new ResultDto
            {
                request_uri = _requestUri,
                expires_in = _expiresIn
            };

            await context.Response.WriteJsonAsync(dto);
        }

        private sealed class ResultDto
        {
            public string request_uri { get; set; } = default!;
            public int expires_in { get; set; }
        }
    }
}
