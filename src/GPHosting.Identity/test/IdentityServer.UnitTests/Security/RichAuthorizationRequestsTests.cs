// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityModel;
using IdentityServer.UnitTests.Validation.Setup;
using GPHosting.Identity.Models;
using GPHosting.Identity.Stores;
using Xunit;

namespace IdentityServer.UnitTests.Security
{
    /// <summary>
    /// Verifies that authorization_details (RFC 9396 — Rich Authorization Requests) are only
    /// accepted when every requested type is present in the client's AllowedAuthorizationDetailsTypes
    /// allowlist — a client with no configured types cannot use RAR at all, and a request naming an
    /// unlisted type must be rejected rather than silently ignored or partially honored.
    /// </summary>
    public class RichAuthorizationRequestsTests
    {
        private const string Category = "Security - Rich Authorization Requests";

        private static IClientStore BuildClientStore(params Client[] clients)
        {
            return new InMemoryClientStore(new List<Client>(clients));
        }

        private static NameValueCollection BaseAuthorizeParameters(string clientId, string authorizationDetails = null)
        {
            var parameters = new NameValueCollection
            {
                { OidcConstants.AuthorizeRequest.ClientId, clientId },
                { OidcConstants.AuthorizeRequest.Scope, "openid" },
                { OidcConstants.AuthorizeRequest.RedirectUri, "https://app/callback" },
                { OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code },
                { OidcConstants.AuthorizeRequest.CodeChallenge, "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM" },
                { OidcConstants.AuthorizeRequest.CodeChallengeMethod, OidcConstants.CodeChallengeMethods.Sha256 }
            };

            if (authorizationDetails != null)
            {
                parameters.Add("authorization_details", authorizationDetails);
            }

            return parameters;
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Request_with_no_authorization_details_should_be_unaffected()
        {
            var client = new Client
            {
                ClientId = "rar.none",
                RequireClientSecret = false,
                AllowedGrantTypes = GrantTypes.Code,
                AllowedScopes = { "openid" },
                RedirectUris = { "https://app/callback" },
                RequireConsent = false
                // AllowedAuthorizationDetailsTypes intentionally left empty
            };

            var validator = Factory.CreateAuthorizeRequestValidator(clients: BuildClientStore(client));
            var result = await validator.ValidateAsync(BaseAuthorizeParameters("rar.none"));

            result.IsError.Should().BeFalse("a request that doesn't use RAR must not be affected by RAR validation");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Allowed_type_should_be_accepted()
        {
            var client = new Client
            {
                ClientId = "rar.allowed",
                RequireClientSecret = false,
                AllowedGrantTypes = GrantTypes.Code,
                AllowedScopes = { "openid" },
                RedirectUris = { "https://app/callback" },
                RequireConsent = false,
                AllowedAuthorizationDetailsTypes = { "payment_initiation" }
            };

            var parameters = BaseAuthorizeParameters(
                "rar.allowed",
                """[{"type":"payment_initiation","actions":["initiate"]}]""");

            var validator = Factory.CreateAuthorizeRequestValidator(clients: BuildClientStore(client));
            var result = await validator.ValidateAsync(parameters);

            result.IsError.Should().BeFalse("a type present in the client's allowlist must be accepted");
            result.ValidatedRequest.RawAuthorizationDetails.Should().Contain("payment_initiation");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Type_not_in_client_allowlist_should_be_rejected()
        {
            var client = new Client
            {
                ClientId = "rar.mismatch",
                RequireClientSecret = false,
                AllowedGrantTypes = GrantTypes.Code,
                AllowedScopes = { "openid" },
                RedirectUris = { "https://app/callback" },
                RequireConsent = false,
                AllowedAuthorizationDetailsTypes = { "payment_initiation" }
            };

            var parameters = BaseAuthorizeParameters(
                "rar.mismatch",
                """[{"type":"account_information"}]""");

            var validator = Factory.CreateAuthorizeRequestValidator(clients: BuildClientStore(client));
            var result = await validator.ValidateAsync(parameters);

            result.IsError.Should().BeTrue("a type outside the client's allowlist must be rejected");
            result.Error.Should().Be("invalid_authorization_details");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Client_with_empty_allowlist_should_reject_any_authorization_details()
        {
            var client = new Client
            {
                ClientId = "rar.empty-allowlist",
                RequireClientSecret = false,
                AllowedGrantTypes = GrantTypes.Code,
                AllowedScopes = { "openid" },
                RedirectUris = { "https://app/callback" },
                RequireConsent = false
                // no AllowedAuthorizationDetailsTypes configured at all
            };

            var parameters = BaseAuthorizeParameters(
                "rar.empty-allowlist",
                """[{"type":"payment_initiation"}]""");

            var validator = Factory.CreateAuthorizeRequestValidator(clients: BuildClientStore(client));
            var result = await validator.ValidateAsync(parameters);

            result.IsError.Should().BeTrue("a client with no allowed types must not be able to use RAR at all");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Malformed_json_should_be_rejected()
        {
            var client = new Client
            {
                ClientId = "rar.malformed",
                RequireClientSecret = false,
                AllowedGrantTypes = GrantTypes.Code,
                AllowedScopes = { "openid" },
                RedirectUris = { "https://app/callback" },
                RequireConsent = false,
                AllowedAuthorizationDetailsTypes = { "payment_initiation" }
            };

            var parameters = BaseAuthorizeParameters("rar.malformed", "not-json-at-all");

            var validator = Factory.CreateAuthorizeRequestValidator(clients: BuildClientStore(client));
            var result = await validator.ValidateAsync(parameters);

            result.IsError.Should().BeTrue("malformed authorization_details must be rejected, not ignored");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Object_instead_of_array_should_be_rejected()
        {
            var client = new Client
            {
                ClientId = "rar.not-array",
                RequireClientSecret = false,
                AllowedGrantTypes = GrantTypes.Code,
                AllowedScopes = { "openid" },
                RedirectUris = { "https://app/callback" },
                RequireConsent = false,
                AllowedAuthorizationDetailsTypes = { "payment_initiation" }
            };

            var parameters = BaseAuthorizeParameters(
                "rar.not-array",
                """{"type":"payment_initiation"}""");

            var validator = Factory.CreateAuthorizeRequestValidator(clients: BuildClientStore(client));
            var result = await validator.ValidateAsync(parameters);

            result.IsError.Should().BeTrue("authorization_details must be a JSON array per RFC 9396, not a bare object");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Entry_missing_type_should_be_rejected()
        {
            var client = new Client
            {
                ClientId = "rar.missing-type",
                RequireClientSecret = false,
                AllowedGrantTypes = GrantTypes.Code,
                AllowedScopes = { "openid" },
                RedirectUris = { "https://app/callback" },
                RequireConsent = false,
                AllowedAuthorizationDetailsTypes = { "payment_initiation" }
            };

            var parameters = BaseAuthorizeParameters(
                "rar.missing-type",
                """[{"actions":["initiate"]}]""");

            var validator = Factory.CreateAuthorizeRequestValidator(clients: BuildClientStore(client));
            var result = await validator.ValidateAsync(parameters);

            result.IsError.Should().BeTrue("an authorization_details entry without a type must be rejected");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task One_disallowed_type_among_several_should_reject_the_whole_request()
        {
            var client = new Client
            {
                ClientId = "rar.partial",
                RequireClientSecret = false,
                AllowedGrantTypes = GrantTypes.Code,
                AllowedScopes = { "openid" },
                RedirectUris = { "https://app/callback" },
                RequireConsent = false,
                AllowedAuthorizationDetailsTypes = { "payment_initiation" }
            };

            var parameters = BaseAuthorizeParameters(
                "rar.partial",
                """[{"type":"payment_initiation"},{"type":"account_information"}]""");

            var validator = Factory.CreateAuthorizeRequestValidator(clients: BuildClientStore(client));
            var result = await validator.ValidateAsync(parameters);

            result.IsError.Should().BeTrue(
                "the whole request must fail if any single entry names a disallowed type — no partial honoring");
        }
    }
}
