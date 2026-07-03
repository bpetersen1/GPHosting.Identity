// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityServer.IntegrationTests.Common;
using GPHosting.Identity.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IdentityServer.IntegrationTests.Endpoints.Token
{
    /// <summary>
    /// Verifies the FAPI 2.0 profile flag (Client.RequireFapi2) actually enforces sender-constrained
    /// access tokens at the token endpoint — a client configured for FAPI 2.0 must present a valid
    /// DPoP proof to get a token at all, not just optionally benefit from one.
    /// </summary>
    public class Fapi2Tests
    {
        private const string Category = "Token endpoint - FAPI 2.0";

        private IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();

        public Fapi2Tests()
        {
            _mockPipeline.Clients.Add(new Client
            {
                ClientId = "fapi2.client",
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                ClientSecrets = { new Secret("secret".Sha256()) },
                AllowedScopes = { "api1" },
                RequireFapi2 = true
            });

            _mockPipeline.ApiScopes.Add(new ApiScope("api1"));

            _mockPipeline.Initialize();
        }

        private static string CreateDPoPProof(ECDsa key, string htm, string htu)
        {
            var parameters = key.ExportParameters(false);
            var jwk = new Dictionary<string, object>
            {
                ["kty"] = "EC",
                ["crv"] = "P-256",
                ["x"] = Base64UrlEncoder.Encode(parameters.Q.X),
                ["y"] = Base64UrlEncoder.Encode(parameters.Q.Y)
            };

            var headerClaims = new Dictionary<string, object>
            {
                ["typ"] = "dpop+jwt",
                ["jwk"] = jwk
            };

            var payload = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["htm"] = htm,
                ["htu"] = htu,
                ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ["jti"] = Guid.NewGuid().ToString()
            });

            var credentials = new SigningCredentials(new ECDsaSecurityKey(key), SecurityAlgorithms.EcdsaSha256);
            return new JsonWebTokenHandler().CreateToken(payload, credentials, headerClaims);
        }

        private HttpRequestMessage BuildTokenRequest(string dPopProof = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, IdentityServerPipeline.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = "fapi2.client",
                    ["client_secret"] = "secret",
                    ["scope"] = "api1"
                })
            };

            if (dPopProof != null)
            {
                request.Headers.Add("DPoP", dPopProof);
            }

            return request;
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Token_request_without_dpop_proof_should_be_rejected()
        {
            var response = await _mockPipeline.BackChannelClient.SendAsync(BuildTokenRequest());

            response.IsSuccessStatusCode.Should().BeFalse("a FAPI 2.0 client must present a DPoP proof to get any token");

            var body = JObject.Parse(await response.Content.ReadAsStringAsync());
            body["error"]?.ToString().Should().Be("invalid_request");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Token_request_with_valid_dpop_proof_should_succeed_and_bind_the_token()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var proof = CreateDPoPProof(key, "POST", IdentityServerPipeline.TokenEndpoint);

            var response = await _mockPipeline.BackChannelClient.SendAsync(BuildTokenRequest(proof));

            response.IsSuccessStatusCode.Should().BeTrue();

            var body = JObject.Parse(await response.Content.ReadAsStringAsync());
            body["token_type"]?.ToString().Should().Be("DPoP");
            body["access_token"].Should().NotBeNull();
        }
    }
}
