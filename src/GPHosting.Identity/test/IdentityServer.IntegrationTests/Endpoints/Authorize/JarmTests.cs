// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityServer.IntegrationTests.Common;
using GPHosting.Identity;
using GPHosting.Identity.Models;
using GPHosting.Identity.Test;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace IdentityServer.IntegrationTests.Endpoints.Authorize
{
    /// <summary>
    /// Verifies JARM (JWT Secured Authorization Response Mode) — that response modes ending in
    /// .jwt (and bare "jwt") wrap the authorize response in a signed JWT rather than plain query
    /// or fragment parameters, and that the JWT is genuinely verifiable against the server's own
    /// published signing key (not just JWT-shaped).
    /// </summary>
    public class JarmTests
    {
        private const string Category = "Authorize endpoint - JARM";

        private IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();

        public JarmTests()
        {
            _mockPipeline.Clients.Add(new Client
            {
                ClientId = "jarm.client",
                AllowedGrantTypes = GrantTypes.Code,
                ClientSecrets = { new Secret("secret".Sha256()) },
                RequirePkce = false,
                RequireConsent = false,
                AllowedScopes = new List<string> { "openid" },
                RedirectUris = new List<string> { "https://jarm.client/callback" }
            });

            _mockPipeline.Users.Add(new TestUser
            {
                SubjectId = "bob",
                Username = "bob"
            });

            _mockPipeline.IdentityScopes.Add(new IdentityResources.OpenId());

            _mockPipeline.Initialize();
        }

        private async Task<JwtSecurityToken> GetVerifiedJarmResponseAsync(string url)
        {
            _mockPipeline.BrowserClient.AllowAutoRedirect = false;
            var response = await _mockPipeline.BrowserClient.GetAsync(url);
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);

            var location = response.Headers.Location.ToString();
            location.Should().Contain("response=");

            var query = QueryHelpers.ParseQuery(new System.Uri(location).Query);
            query.TryGetValue("response", out var jwtValues).Should().BeTrue();
            var jwt = jwtValues.ToString();
            jwt.Should().NotBeNullOrEmpty();

            var jwksJson = await (await _mockPipeline.BackChannelClient.GetAsync(IdentityServerPipeline.DiscoveryKeysEndpoint)).Content.ReadAsStringAsync();
            var jwks = new JsonWebKeySet(jwksJson);

            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(jwt, new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                IssuerSigningKeys = jwks.GetSigningKeys()
            }, out var validatedToken);

            return (JwtSecurityToken)validatedToken;
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task query_jwt_response_mode_should_return_a_verifiable_jwt_in_the_query_string()
        {
            await _mockPipeline.LoginAsync("bob");

            var url = _mockPipeline.CreateAuthorizeUrl(
                clientId: "jarm.client",
                responseType: "code",
                responseMode: "query.jwt",
                scope: "openid",
                redirectUri: "https://jarm.client/callback",
                state: "abc123");

            var jwt = await GetVerifiedJarmResponseAsync(url);

            jwt.Claims.Should().Contain(c => c.Type == "state" && c.Value == "abc123");
            jwt.Claims.Should().Contain(c => c.Type == "code");
            jwt.Claims.Should().Contain(c => c.Type == "aud" && c.Value == "jarm.client");
            jwt.Claims.Should().Contain(c => c.Type == "iss");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task bare_jwt_response_mode_should_default_to_query_delivery_for_code_flow()
        {
            await _mockPipeline.LoginAsync("bob");

            var url = _mockPipeline.CreateAuthorizeUrl(
                clientId: "jarm.client",
                responseType: "code",
                responseMode: "jwt",
                scope: "openid",
                redirectUri: "https://jarm.client/callback",
                state: "xyz789");

            var jwt = await GetVerifiedJarmResponseAsync(url);

            jwt.Claims.Should().Contain(c => c.Type == "state" && c.Value == "xyz789");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task plain_query_response_mode_should_not_be_wrapped_in_a_jwt()
        {
            await _mockPipeline.LoginAsync("bob");

            _mockPipeline.BrowserClient.AllowAutoRedirect = false;
            var url = _mockPipeline.CreateAuthorizeUrl(
                clientId: "jarm.client",
                responseType: "code",
                responseMode: "query",
                scope: "openid",
                redirectUri: "https://jarm.client/callback",
                state: "plain123");

            var response = await _mockPipeline.BrowserClient.GetAsync(url);

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            var location = response.Headers.Location.ToString();
            location.Should().Contain("code=");
            location.Should().NotContain("response=");
        }
    }
}
