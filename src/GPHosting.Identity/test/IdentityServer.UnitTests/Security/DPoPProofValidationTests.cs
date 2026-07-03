// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using GPHosting.Identity.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace IdentityServer.UnitTests.Security
{
    /// <summary>
    /// Verifies DPoP proof validation (RFC 9449) rejects the specific attacks it exists to close:
    /// a stolen/forged proof reusing someone else's key, replay outside the recency window, and a
    /// proof that leaks private key material — not just that a well-formed proof is accepted.
    /// </summary>
    public class DPoPProofValidationTests
    {
        private const string Category = "Security - DPoP Proof Validation";
        private const string Htu = "https://server/connect/token";
        private const string Htm = "POST";

        private readonly DefaultDPoPProofValidator _validator = new(NullLogger<DefaultDPoPProofValidator>.Instance);

        private static string CreateProof(
            ECDsa key,
            string htm = Htm,
            string htu = Htu,
            DateTimeOffset? iat = null,
            string jti = "test-jti",
            string typ = "dpop+jwt",
            bool includePrivateKey = false)
        {
            var parameters = key.ExportParameters(includePrivateKey);
            var jwk = new Dictionary<string, object>
            {
                ["kty"] = "EC",
                ["crv"] = "P-256",
                ["x"] = Base64UrlEncoder.Encode(parameters.Q.X),
                ["y"] = Base64UrlEncoder.Encode(parameters.Q.Y)
            };

            if (includePrivateKey)
            {
                jwk["d"] = Base64UrlEncoder.Encode(parameters.D);
            }

            var headerClaims = new Dictionary<string, object> { ["typ"] = typ, ["jwk"] = jwk };

            var payloadDict = new Dictionary<string, object> { ["iat"] = (iat ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() };
            if (htm != null) payloadDict["htm"] = htm;
            if (htu != null) payloadDict["htu"] = htu;
            if (jti != null) payloadDict["jti"] = jti;

            var credentials = new SigningCredentials(new ECDsaSecurityKey(key), SecurityAlgorithms.EcdsaSha256);
            return new JsonWebTokenHandler().CreateToken(JsonSerializer.Serialize(payloadDict), credentials, headerClaims);
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Valid_proof_should_be_accepted_and_produce_a_confirmation()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var proof = CreateProof(key);

            var result = await _validator.ValidateAsync(new DPoPProofValidationContext { ProofToken = proof, HttpMethod = Htm, HttpUrl = Htu });

            result.IsError.Should().BeFalse();
            result.Confirmation.Should().NotBeNullOrEmpty();
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Proof_containing_private_key_material_should_be_rejected()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var proof = CreateProof(key, includePrivateKey: true);

            var result = await _validator.ValidateAsync(new DPoPProofValidationContext { ProofToken = proof, HttpMethod = Htm, HttpUrl = Htu });

            result.IsError.Should().BeTrue("a proof leaking the private key must never be accepted, even if otherwise well-formed");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Mismatched_http_method_should_be_rejected()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var proof = CreateProof(key, htm: "GET");

            var result = await _validator.ValidateAsync(new DPoPProofValidationContext { ProofToken = proof, HttpMethod = Htm, HttpUrl = Htu });

            result.IsError.Should().BeTrue("a proof minted for a different HTTP method must not authorize this request");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Mismatched_http_url_should_be_rejected()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var proof = CreateProof(key, htu: "https://server/connect/introspect");

            var result = await _validator.ValidateAsync(new DPoPProofValidationContext { ProofToken = proof, HttpMethod = Htm, HttpUrl = Htu });

            result.IsError.Should().BeTrue("a proof minted for a different endpoint must not authorize this request");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Stale_iat_outside_the_recency_window_should_be_rejected()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var proof = CreateProof(key, iat: DateTimeOffset.UtcNow.AddMinutes(-10));

            var result = await _validator.ValidateAsync(new DPoPProofValidationContext { ProofToken = proof, HttpMethod = Htm, HttpUrl = Htu });

            result.IsError.Should().BeTrue("an old proof must be rejected — this is what bounds replay for a proof without an exp claim");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Missing_jti_should_be_rejected()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var proof = CreateProof(key, jti: null);

            var result = await _validator.ValidateAsync(new DPoPProofValidationContext { ProofToken = proof, HttpMethod = Htm, HttpUrl = Htu });

            result.IsError.Should().BeTrue("jti is required by RFC 9449 for replay-detection bookkeeping");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Wrong_typ_header_should_be_rejected()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var proof = CreateProof(key, typ: "JWT");

            var result = await _validator.ValidateAsync(new DPoPProofValidationContext { ProofToken = proof, HttpMethod = Htm, HttpUrl = Htu });

            result.IsError.Should().BeTrue("a proof must be typed dpop+jwt so it can't be confused with an ordinary JWT");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Signature_from_a_different_key_than_the_embedded_jwk_should_be_rejected()
        {
            using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var claimedKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            // Build a proof whose header advertises claimedKey's public key, but sign it with a
            // different key entirely — simulates an attacker who doesn't hold the private key
            // for the jwk they're claiming.
            var parameters = claimedKey.ExportParameters(false);
            var jwk = new Dictionary<string, object>
            {
                ["kty"] = "EC",
                ["crv"] = "P-256",
                ["x"] = Base64UrlEncoder.Encode(parameters.Q.X),
                ["y"] = Base64UrlEncoder.Encode(parameters.Q.Y)
            };
            var headerClaims = new Dictionary<string, object> { ["typ"] = "dpop+jwt", ["jwk"] = jwk };
            var payload = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["htm"] = Htm,
                ["htu"] = Htu,
                ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ["jti"] = "mismatched-key-test"
            });
            var credentials = new SigningCredentials(new ECDsaSecurityKey(signingKey), SecurityAlgorithms.EcdsaSha256);
            var proof = new JsonWebTokenHandler().CreateToken(payload, credentials, headerClaims);

            var result = await _validator.ValidateAsync(new DPoPProofValidationContext { ProofToken = proof, HttpMethod = Htm, HttpUrl = Htu });

            result.IsError.Should().BeTrue("the signature must actually verify against the jwk embedded in the same proof");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Malformed_token_should_be_rejected()
        {
            var result = await _validator.ValidateAsync(new DPoPProofValidationContext { ProofToken = "not-a-jwt", HttpMethod = Htm, HttpUrl = Htu });

            result.IsError.Should().BeTrue();
        }
    }
}
