// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityModel;
using IdentityServer.UnitTests.Validation.Setup;
using GPHosting.Identity.Models;
using GPHosting.Identity.Stores;
using Xunit;

namespace IdentityServer.UnitTests.Security
{
    public class PushedAuthorizationRequestTests
    {
        private const string Category = "Security - Pushed Authorization Requests";
        private const string ParPrefix = "urn:ietf:params:oauth:request_uri:";

        private static readonly Client _codeClient = new Client
        {
            ClientId = "codeclient",
            RequireClientSecret = false,
            RequirePkce = true,
            AllowedGrantTypes = GrantTypes.Code,
            AllowedScopes = { "openid", "api1" },
            RedirectUris = { "https://client.example.com/callback" },
        };

        private static readonly Client _parRequiredClient = new Client
        {
            ClientId = "par.required",
            RequireClientSecret = false,
            RequirePkce = true,
            RequirePushedAuthorization = true,
            AllowedGrantTypes = GrantTypes.Code,
            AllowedScopes = { "openid", "api1" },
            RedirectUris = { "https://client.example.com/callback" },
        };

        private static NameValueCollection BuildValidParams(string clientId = "codeclient") =>
            new NameValueCollection
            {
                { OidcConstants.AuthorizeRequest.ClientId, clientId },
                { OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code },
                { OidcConstants.AuthorizeRequest.Scope, "openid" },
                { OidcConstants.AuthorizeRequest.RedirectUri, "https://client.example.com/callback" },
                { OidcConstants.AuthorizeRequest.CodeChallenge, "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM" },
                { OidcConstants.AuthorizeRequest.CodeChallengeMethod, OidcConstants.CodeChallengeMethods.Sha256 },
            };

        private static string SerializeParams(NameValueCollection nvc)
        {
            var dict = new Dictionary<string, string>();
            foreach (string key in nvc.AllKeys)
                dict[key!] = nvc[key] ?? string.Empty;
            return JsonSerializer.Serialize(dict);
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Valid_par_request_uri_is_resolved_and_request_succeeds()
        {
            var parStore = new InMemoryPushedAuthorizationRequestStore();
            var handle = "valid-handle-001";
            var storedParams = BuildValidParams();

            await parStore.StoreAsync(new PushedAuthorizationRequest
            {
                ReferenceValue = handle,
                ExpiresAt = DateTime.UtcNow.AddSeconds(60),
                Parameters = SerializeParams(storedParams),
            });

            var clients = new InMemoryClientStore(new List<Client> { _codeClient });
            var validator = Factory.CreateAuthorizeRequestValidator(clients: clients, parStore: parStore);

            var request = new NameValueCollection
            {
                { OidcConstants.AuthorizeRequest.ClientId, "codeclient" },
                { OidcConstants.AuthorizeRequest.RequestUri, ParPrefix + handle },
            };

            var result = await validator.ValidateAsync(request);

            result.IsError.Should().BeFalse("a valid PAR request_uri should resolve and pass validation");
            result.ValidatedRequest.IsPushedAuthorization.Should().BeTrue();
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Expired_par_request_uri_is_rejected()
        {
            var parStore = new InMemoryPushedAuthorizationRequestStore();
            var handle = "expired-handle-001";
            var storedParams = BuildValidParams();

            await parStore.StoreAsync(new PushedAuthorizationRequest
            {
                ReferenceValue = handle,
                ExpiresAt = DateTime.UtcNow.AddSeconds(-1), // already expired
                Parameters = SerializeParams(storedParams),
            });

            var clients = new InMemoryClientStore(new List<Client> { _codeClient });
            var validator = Factory.CreateAuthorizeRequestValidator(clients: clients, parStore: parStore);

            var request = new NameValueCollection
            {
                { OidcConstants.AuthorizeRequest.ClientId, "codeclient" },
                { OidcConstants.AuthorizeRequest.RequestUri, ParPrefix + handle },
            };

            var result = await validator.ValidateAsync(request);

            result.IsError.Should().BeTrue("an expired PAR request_uri must be rejected");
            result.Error.Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Unknown_par_request_uri_is_rejected()
        {
            var parStore = new InMemoryPushedAuthorizationRequestStore();
            var clients = new InMemoryClientStore(new List<Client> { _codeClient });
            var validator = Factory.CreateAuthorizeRequestValidator(clients: clients, parStore: parStore);

            var request = new NameValueCollection
            {
                { OidcConstants.AuthorizeRequest.ClientId, "codeclient" },
                { OidcConstants.AuthorizeRequest.RequestUri, ParPrefix + "nonexistent-handle" },
            };

            var result = await validator.ValidateAsync(request);

            result.IsError.Should().BeTrue("an unknown PAR request_uri must be rejected");
            result.Error.Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Par_request_uri_is_single_use_second_use_is_rejected()
        {
            var parStore = new InMemoryPushedAuthorizationRequestStore();
            var handle = "one-time-handle-001";
            var storedParams = BuildValidParams();

            await parStore.StoreAsync(new PushedAuthorizationRequest
            {
                ReferenceValue = handle,
                ExpiresAt = DateTime.UtcNow.AddSeconds(60),
                Parameters = SerializeParams(storedParams),
            });

            var clients = new InMemoryClientStore(new List<Client> { _codeClient });
            var validator = Factory.CreateAuthorizeRequestValidator(clients: clients, parStore: parStore);

            var request = new NameValueCollection
            {
                { OidcConstants.AuthorizeRequest.ClientId, "codeclient" },
                { OidcConstants.AuthorizeRequest.RequestUri, ParPrefix + handle },
            };

            var first = await validator.ValidateAsync(request);
            first.IsError.Should().BeFalse("first use should succeed");

            var second = await validator.ValidateAsync(request);
            second.IsError.Should().BeTrue("second use of the same request_uri must be rejected (one-time use)");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Client_id_mismatch_between_outer_request_and_par_params_is_rejected()
        {
            var parStore = new InMemoryPushedAuthorizationRequestStore();
            var handle = "mismatch-handle-001";

            // stored params belong to "codeclient"
            var storedParams = BuildValidParams("codeclient");
            await parStore.StoreAsync(new PushedAuthorizationRequest
            {
                ReferenceValue = handle,
                ExpiresAt = DateTime.UtcNow.AddSeconds(60),
                Parameters = SerializeParams(storedParams),
            });

            var clients = new InMemoryClientStore(new List<Client> { _codeClient });
            var validator = Factory.CreateAuthorizeRequestValidator(clients: clients, parStore: parStore);

            // outer request claims to be a different client
            var request = new NameValueCollection
            {
                { OidcConstants.AuthorizeRequest.ClientId, "attacker" },
                { OidcConstants.AuthorizeRequest.RequestUri, ParPrefix + handle },
            };

            var result = await validator.ValidateAsync(request);

            result.IsError.Should().BeTrue("client_id mismatch must be rejected (prevents request hijacking)");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Client_requiring_par_rejects_direct_authorize_request()
        {
            var parStore = new InMemoryPushedAuthorizationRequestStore();
            var clients = new InMemoryClientStore(new List<Client> { _parRequiredClient });
            var validator = Factory.CreateAuthorizeRequestValidator(clients: clients, parStore: parStore);

            // direct authorize request without a PAR request_uri
            var request = BuildValidParams("par.required");

            var result = await validator.ValidateAsync(request);

            result.IsError.Should().BeTrue("a client with RequirePushedAuthorization=true must reject direct authorize requests");
            result.Error.Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Client_requiring_par_accepts_valid_par_request()
        {
            var parStore = new InMemoryPushedAuthorizationRequestStore();
            var handle = "par-required-handle-001";
            var storedParams = BuildValidParams("par.required");

            await parStore.StoreAsync(new PushedAuthorizationRequest
            {
                ReferenceValue = handle,
                ExpiresAt = DateTime.UtcNow.AddSeconds(60),
                Parameters = SerializeParams(storedParams),
            });

            var clients = new InMemoryClientStore(new List<Client> { _parRequiredClient });
            var validator = Factory.CreateAuthorizeRequestValidator(clients: clients, parStore: parStore);

            var request = new NameValueCollection
            {
                { OidcConstants.AuthorizeRequest.ClientId, "par.required" },
                { OidcConstants.AuthorizeRequest.RequestUri, ParPrefix + handle },
            };

            var result = await validator.ValidateAsync(request);

            result.IsError.Should().BeFalse("a valid PAR request must be accepted even for clients that require PAR");
            result.ValidatedRequest.IsPushedAuthorization.Should().BeTrue();
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Non_par_request_uri_is_not_treated_as_par()
        {
            // A standard JWT request_uri (https://) must not be mistaken for a PAR reference
            var parStore = new InMemoryPushedAuthorizationRequestStore();
            var clients = new InMemoryClientStore(new List<Client> { _codeClient });
            var validator = Factory.CreateAuthorizeRequestValidator(clients: clients, parStore: parStore);

            var request = BuildValidParams("codeclient");
            // Add a non-PAR request_uri (JAR-style) — should fall through to JAR handling, not PAR
            request[OidcConstants.AuthorizeRequest.RequestUri] = "https://client.example.com/request-object";
            request.Remove(OidcConstants.AuthorizeRequest.CodeChallenge);
            request.Remove(OidcConstants.AuthorizeRequest.CodeChallengeMethod);

            // This will fail for other reasons (JAR not enabled or fetch failure), but NOT as a PAR error
            var result = await validator.ValidateAsync(request);

            // We only assert IsPushedAuthorization is false — the error path is expected
            result.ValidatedRequest?.IsPushedAuthorization.Should().BeFalse("a https:// request_uri is JAR, not PAR");
        }
    }
}
