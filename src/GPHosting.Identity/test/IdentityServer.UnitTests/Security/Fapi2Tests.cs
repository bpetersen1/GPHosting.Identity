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
    /// <summary>
    /// Verifies the FAPI 2.0 profile flag (Client.RequireFapi2) actually forces its component
    /// requirements at the authorize endpoint — PAR, code-only response_type, and PKCE with S256 —
    /// rather than just existing as an inert database column.
    /// </summary>
    public class Fapi2Tests
    {
        private const string Category = "Security - FAPI 2.0";
        private const string ParPrefix = "urn:ietf:params:oauth:request_uri:";

        private static IClientStore BuildClientStore(params Client[] clients) => new InMemoryClientStore(new List<Client>(clients));

        private static NameValueCollection AuthorizeParameters(
            string clientId,
            string responseType = "code",
            string codeChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            string codeChallengeMethod = OidcConstants.CodeChallengeMethods.Sha256)
        {
            var parameters = new NameValueCollection
            {
                { OidcConstants.AuthorizeRequest.ClientId, clientId },
                { OidcConstants.AuthorizeRequest.Scope, "openid" },
                { OidcConstants.AuthorizeRequest.RedirectUri, "https://app/callback" },
                { OidcConstants.AuthorizeRequest.ResponseType, responseType }
            };

            if (codeChallenge != null)
            {
                parameters.Add(OidcConstants.AuthorizeRequest.CodeChallenge, codeChallenge);
            }
            if (codeChallengeMethod != null)
            {
                parameters.Add(OidcConstants.AuthorizeRequest.CodeChallengeMethod, codeChallengeMethod);
            }

            return parameters;
        }

        private static Client Fapi2Client(string clientId, bool allowPlainPkce = false, IEnumerable<string> grantTypes = null) => new()
        {
            ClientId = clientId,
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = grantTypes != null ? new List<string>(grantTypes) : GrantTypes.CodeAndClientCredentials,
            AllowedScopes = { "openid" },
            RedirectUris = { "https://app/callback" },
            RequireConsent = false,
            AllowPlainTextPkce = allowPlainPkce,
            RequireFapi2 = true
        };

        private static string SerializeParams(NameValueCollection nvc)
        {
            var dict = new Dictionary<string, string>();
            foreach (string key in nvc.AllKeys)
                dict[key!] = nvc[key] ?? string.Empty;
            return JsonSerializer.Serialize(dict);
        }

        /// <summary>
        /// Pushes the given authorize parameters through a real PAR store round-trip (store, then
        /// resolve via request_uri), so tests exercise actual PAR resolution rather than just
        /// flipping IsPushedAuthorization by hand.
        /// </summary>
        private static async Task<NameValueCollection> PushAsync(IPushedAuthorizationRequestStore parStore, NameValueCollection parameters, string clientId)
        {
            var handle = Guid.NewGuid().ToString("N");
            await parStore.StoreAsync(new PushedAuthorizationRequest
            {
                ReferenceValue = handle,
                ExpiresAt = DateTime.UtcNow.AddSeconds(60),
                Parameters = SerializeParams(parameters)
            });

            return new NameValueCollection
            {
                { OidcConstants.AuthorizeRequest.ClientId, clientId },
                { OidcConstants.AuthorizeRequest.RequestUri, ParPrefix + handle }
            };
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Fapi2_client_via_PAR_with_code_and_S256_should_be_accepted()
        {
            var client = Fapi2Client("fapi2.happy-path");
            var parStore = new InMemoryPushedAuthorizationRequestStore();
            var validator = Factory.CreateAuthorizeRequestValidator(clients: BuildClientStore(client), parStore: parStore);

            var request = await PushAsync(parStore, AuthorizeParameters("fapi2.happy-path"), "fapi2.happy-path");
            var result = await validator.ValidateAsync(request);

            result.IsError.Should().BeFalse("PAR + code + S256 satisfies every FAPI 2.0 requirement this client has");
            result.ValidatedRequest.IsPushedAuthorization.Should().BeTrue();
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Fapi2_client_not_using_PAR_should_be_rejected()
        {
            var client = Fapi2Client("fapi2.no-par");
            var validator = Factory.CreateAuthorizeRequestValidator(clients: BuildClientStore(client));

            var result = await validator.ValidateAsync(AuthorizeParameters("fapi2.no-par"));

            result.IsError.Should().BeTrue("FAPI 2.0 requires PAR even if RequirePushedAuthorization wasn't separately set");
            result.ErrorDescription.Should().Contain("pushed authorization");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Fapi2_client_requesting_hybrid_response_type_should_be_rejected()
        {
            // Hybrid is otherwise allowed for this client — isolates the FAPI 2.0 "code only"
            // check from a plain "grant type not allowed" rejection.
            var client = Fapi2Client("fapi2.hybrid", grantTypes: GrantTypes.Hybrid);
            var parStore = new InMemoryPushedAuthorizationRequestStore();
            var validator = Factory.CreateAuthorizeRequestValidator(clients: BuildClientStore(client), parStore: parStore);

            var parameters = AuthorizeParameters("fapi2.hybrid", responseType: "code id_token");
            var request = await PushAsync(parStore, parameters, "fapi2.hybrid");
            var result = await validator.ValidateAsync(request);

            result.IsError.Should().BeTrue("FAPI 2.0 only permits the authorization code flow, even via PAR");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Fapi2_client_using_plain_pkce_should_be_rejected()
        {
            var client = Fapi2Client("fapi2.plain-pkce", allowPlainPkce: true);
            var parStore = new InMemoryPushedAuthorizationRequestStore();
            var validator = Factory.CreateAuthorizeRequestValidator(clients: BuildClientStore(client), parStore: parStore);

            var parameters = AuthorizeParameters("fapi2.plain-pkce", codeChallengeMethod: OidcConstants.CodeChallengeMethods.Plain);
            var request = await PushAsync(parStore, parameters, "fapi2.plain-pkce");
            var result = await validator.ValidateAsync(request);

            result.IsError.Should().BeTrue("FAPI 2.0 requires S256 specifically — plain must never satisfy it, even if the client otherwise allows plain PKCE");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Fapi2_client_without_pkce_at_all_should_be_rejected()
        {
            // Confidential client with RequirePkce left false — would be allowed to skip PKCE
            // entirely if not for RequireFapi2.
            var client = Fapi2Client("fapi2.no-pkce");
            client.RequirePkce = false;
            var parStore = new InMemoryPushedAuthorizationRequestStore();
            var validator = Factory.CreateAuthorizeRequestValidator(clients: BuildClientStore(client), parStore: parStore);

            var parameters = AuthorizeParameters("fapi2.no-pkce", codeChallenge: null, codeChallengeMethod: null);
            var request = await PushAsync(parStore, parameters, "fapi2.no-pkce");
            var result = await validator.ValidateAsync(request);

            result.IsError.Should().BeTrue("FAPI 2.0 requires PKCE even for confidential clients that wouldn't otherwise need it");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Non_fapi2_client_is_unaffected_by_these_checks()
        {
            var client = new Client
            {
                ClientId = "plain.client",
                ClientSecrets = { new Secret("secret".Sha256()) },
                AllowedGrantTypes = GrantTypes.Code,
                AllowedScopes = { "openid" },
                RedirectUris = { "https://app/callback" },
                RequireConsent = false,
                RequirePkce = false
                // RequireFapi2 left false (default)
            };

            var validator = Factory.CreateAuthorizeRequestValidator(clients: BuildClientStore(client));

            var result = await validator.ValidateAsync(AuthorizeParameters(
                "plain.client",
                codeChallenge: null,
                codeChallengeMethod: null));

            result.IsError.Should().BeFalse("a client with RequireFapi2=false must not be affected by any of the FAPI 2.0 checks");
        }
    }
}
