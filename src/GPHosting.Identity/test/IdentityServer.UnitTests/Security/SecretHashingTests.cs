// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityModel;
using IdentityServer.UnitTests.Common;
using GPHosting.Identity;
using GPHosting.Identity.Models;
using GPHosting.Identity.Validation;
using Xunit;

namespace IdentityServer.UnitTests.Security
{
    /// <summary>
    /// Verifies that client secrets are stored and compared using cryptographic hashes,
    /// not plain text, to limit the impact of a credential store breach (RFC 6819 §5.1.4.1.3).
    /// </summary>
    public class SecretHashingTests
    {
        private const string Category = "Security - Secret Hashing";

        private readonly HashedSharedSecretValidator _validator =
            new HashedSharedSecretValidator(TestLogger.Create<HashedSharedSecretValidator>());

        private static ParsedSecret ParsedSharedSecret(string clientId, string plainTextCredential) =>
            new ParsedSecret
            {
                Id = clientId,
                Credential = plainTextCredential,
                Type = IdentityServerConstants.ParsedSecretTypes.SharedSecret
            };

        [Fact]
        [Trait("Category", Category)]
        public async Task Sha256_hashed_secret_should_validate_successfully()
        {
            const string plainText = "supersecret";

            var secrets = new List<Secret>
            {
                new Secret(plainText.Sha256())
            };

            var parsed = ParsedSharedSecret("client1", plainText);

            var result = await _validator.ValidateAsync(secrets, parsed);

            result.Success.Should().BeTrue("a SHA-256 hashed secret must match the plain-text credential");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Sha512_hashed_secret_should_validate_successfully()
        {
            const string plainText = "supersecret";

            var secrets = new List<Secret>
            {
                new Secret(plainText.Sha512())
            };

            var parsed = ParsedSharedSecret("client1", plainText);

            var result = await _validator.ValidateAsync(secrets, parsed);

            result.Success.Should().BeTrue("a SHA-512 hashed secret must match the plain-text credential");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Wrong_plain_text_should_not_validate()
        {
            const string storedPlainText = "supersecret";
            const string submittedPlainText = "wrongsecret";

            var secrets = new List<Secret>
            {
                new Secret(storedPlainText.Sha256())
            };

            var parsed = ParsedSharedSecret("client1", submittedPlainText);

            var result = await _validator.ValidateAsync(secrets, parsed);

            result.Success.Should().BeFalse("an incorrect credential must not validate");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Plain_text_stored_secret_should_not_validate()
        {
            // Secrets must be stored as SHA-256 or SHA-512 hashes.
            // If a plain-text value (not 32 or 64 base64 bytes) is stored, the validator
            // must reject it because the length does not match a known hash algorithm.
            const string plainText = "notstoredashash";

            var secrets = new List<Secret>
            {
                new Secret(plainText) // stored as-is, not hashed — insecure configuration
            };

            var parsed = ParsedSharedSecret("client1", plainText);

            var result = await _validator.ValidateAsync(secrets, parsed);

            result.Success.Should().BeFalse(
                "plain-text secrets cannot be validated by the hashed secret validator; " +
                "they must be hashed with SHA-256 or SHA-512 before storage");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Multiple_secrets_validates_against_any_matching_entry()
        {
            const string secret1 = "secret-one";
            const string secret2 = "secret-two";

            var secrets = new List<Secret>
            {
                new Secret(secret1.Sha256()),
                new Secret(secret2.Sha512())
            };

            var parsed = ParsedSharedSecret("client1", secret2);

            var result = await _validator.ValidateAsync(secrets, parsed);

            result.Success.Should().BeTrue(
                "validation must succeed when the credential matches any of the registered hashed secrets");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task No_configured_secrets_should_fail()
        {
            var secrets = new List<Secret>(); // no secrets at all

            var parsed = ParsedSharedSecret("client1", "anyvalue");

            var result = await _validator.ValidateAsync(secrets, parsed);

            result.Success.Should().BeFalse("a client with no configured secrets must never authenticate");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Non_shared_secret_type_should_not_be_validated_by_this_validator()
        {
            var secrets = new List<Secret>
            {
                new Secret("value") { Type = IdentityServerConstants.SecretTypes.X509CertificateBase64 }
            };

            var parsed = new ParsedSecret
            {
                Id = "client1",
                Credential = "value",
                Type = "X509Certificate" // Not a shared secret
            };

            var result = await _validator.ValidateAsync(secrets, parsed);

            result.Success.Should().BeFalse(
                "the hashed shared secret validator must only process SharedSecret type credentials");
        }
    }
}
