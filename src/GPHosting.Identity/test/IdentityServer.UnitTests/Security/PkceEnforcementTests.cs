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
    /// Verifies that PKCE (Proof Key for Code Exchange) is enforced as required by
    /// OAuth 2.0 Security Best Current Practice (RFC 9700).
    /// Public clients must always use PKCE regardless of the RequirePkce configuration flag.
    /// </summary>
    public class PkceEnforcementTests
    {
        private const string Category = "Security - PKCE Enforcement";

        private static IClientStore BuildClientStore(params Client[] clients)
        {
            return new InMemoryClientStore(new List<Client>(clients));
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Public_client_without_pkce_should_be_rejected_even_when_RequirePkce_is_false()
        {
            // Per OAuth 2.0 Security BCP, public clients must always send PKCE.
            // The library enforces this unconditionally when RequireClientSecret=false.
            var publicCodeClient = new Client
            {
                ClientId = "public.code",
                RequireClientSecret = false,
                RequirePkce = false, // explicitly turned off — must be ignored for public clients
                AllowedGrantTypes = GrantTypes.Code,
                AllowedScopes = { "openid" },
                RedirectUris = { "https://app/callback" },
                RequireConsent = false
            };

            var parameters = new NameValueCollection();
            parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "public.code");
            parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
            parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://app/callback");
            parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);
            // Deliberately omit code_challenge and code_challenge_method

            var validator = Factory.CreateAuthorizeRequestValidator(
                clients: BuildClientStore(publicCodeClient));
            var result = await validator.ValidateAsync(parameters);

            result.IsError.Should().BeTrue("public clients must always provide PKCE");
            result.ErrorDescription.Should().Contain("code challenge", "the rejection must be due to missing PKCE");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Public_client_with_pkce_should_be_accepted()
        {
            var publicCodeClient = new Client
            {
                ClientId = "public.code",
                RequireClientSecret = false,
                RequirePkce = false,
                AllowedGrantTypes = GrantTypes.Code,
                AllowedScopes = { "openid" },
                RedirectUris = { "https://app/callback" },
                RequireConsent = false
            };

            var parameters = new NameValueCollection();
            parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "public.code");
            parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
            parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://app/callback");
            parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);
            parameters.Add(OidcConstants.AuthorizeRequest.CodeChallenge, "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM");
            parameters.Add(OidcConstants.AuthorizeRequest.CodeChallengeMethod, OidcConstants.CodeChallengeMethods.Sha256);

            var validator = Factory.CreateAuthorizeRequestValidator(
                clients: BuildClientStore(publicCodeClient));
            var result = await validator.ValidateAsync(parameters);

            result.IsError.Should().BeFalse("public clients that supply a valid PKCE challenge must succeed");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Confidential_client_with_RequirePkce_false_and_no_challenge_should_be_accepted()
        {
            // Confidential clients (RequireClientSecret=true) are not forced into PKCE
            // when RequirePkce=false, because they authenticate via their client secret.
            var confidentialCodeClient = new Client
            {
                ClientId = "confidential.code",
                RequireClientSecret = true,
                RequirePkce = false,
                ClientSecrets = { new Secret("secret".Sha256()) },
                AllowedGrantTypes = GrantTypes.Code,
                AllowedScopes = { "openid" },
                RedirectUris = { "https://app/callback" },
                RequireConsent = false
            };

            var parameters = new NameValueCollection();
            parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "confidential.code");
            parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
            parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://app/callback");
            parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);
            // No code_challenge — allowed because client is confidential with RequirePkce=false

            var validator = Factory.CreateAuthorizeRequestValidator(
                clients: BuildClientStore(confidentialCodeClient));
            var result = await validator.ValidateAsync(parameters);

            result.IsError.Should().BeFalse("confidential clients with RequirePkce=false may omit PKCE");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Confidential_client_with_RequirePkce_true_and_no_challenge_should_be_rejected()
        {
            var confidentialCodeClient = new Client
            {
                ClientId = "confidential.code.pkce",
                RequireClientSecret = true,
                RequirePkce = true,
                ClientSecrets = { new Secret("secret".Sha256()) },
                AllowedGrantTypes = GrantTypes.Code,
                AllowedScopes = { "openid" },
                RedirectUris = { "https://app/callback" },
                RequireConsent = false
            };

            var parameters = new NameValueCollection();
            parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "confidential.code.pkce");
            parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
            parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://app/callback");
            parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);

            var validator = Factory.CreateAuthorizeRequestValidator(
                clients: BuildClientStore(confidentialCodeClient));
            var result = await validator.ValidateAsync(parameters);

            result.IsError.Should().BeTrue("confidential clients with RequirePkce=true must provide a code challenge");
            result.ErrorDescription.Should().Contain("code challenge");
        }
    }
}
