// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using GPHosting.Identity.Models;
using GPHosting.Identity.Validation;
using Xunit;

namespace IdentityServer.UnitTests.Security
{
    /// <summary>
    /// Verifies that redirect URI validation is strict and not susceptible to open redirect
    /// vulnerabilities or URI confusion attacks (RFC 6749 §10.6).
    /// </summary>
    public class RedirectUriValidationTests
    {
        private const string Category = "Security - Redirect URI Validation";

        private readonly StrictRedirectUriValidator _validator = new StrictRedirectUriValidator();

        private Client ClientWithRedirectUri(string uri) => new Client
        {
            RedirectUris = new List<string> { uri }
        };

        [Fact]
        [Trait("Category", Category)]
        public async Task Exact_redirect_uri_should_be_accepted()
        {
            var client = ClientWithRedirectUri("https://app.example.com/callback");

            var result = await _validator.IsRedirectUriValidAsync(
                "https://app.example.com/callback", client);

            result.Should().BeTrue("an exact match must be accepted");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Different_path_should_be_rejected()
        {
            var client = ClientWithRedirectUri("https://app.example.com/callback");

            var result = await _validator.IsRedirectUriValidAsync(
                "https://app.example.com/admin", client);

            result.Should().BeFalse("a URI with a different path must be rejected to prevent open redirect");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Different_host_should_be_rejected()
        {
            var client = ClientWithRedirectUri("https://app.example.com/callback");

            var result = await _validator.IsRedirectUriValidAsync(
                "https://evil.example.com/callback", client);

            result.Should().BeFalse("a URI pointing to a different host must be rejected");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Http_scheme_substitution_for_https_should_be_rejected()
        {
            var client = ClientWithRedirectUri("https://app.example.com/callback");

            var result = await _validator.IsRedirectUriValidAsync(
                "http://app.example.com/callback", client);

            result.Should().BeFalse("downgrading from https to http must be rejected");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Query_string_injection_should_be_rejected()
        {
            var client = ClientWithRedirectUri("https://app.example.com/callback");

            var result = await _validator.IsRedirectUriValidAsync(
                "https://app.example.com/callback?injected=value", client);

            result.Should().BeFalse("appending query parameters to the registered URI must be rejected");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Wildcard_uri_should_not_be_configured_or_matched()
        {
            // The validator must not interpret '*' as a wildcard.
            var client = ClientWithRedirectUri("https://app.example.com/*");

            var result = await _validator.IsRedirectUriValidAsync(
                "https://app.example.com/callback", client);

            result.Should().BeFalse("wildcard patterns must not be treated as glob matches");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Null_redirect_uri_should_be_rejected()
        {
            var client = ClientWithRedirectUri("https://app.example.com/callback");

            var result = await _validator.IsRedirectUriValidAsync(null, client);

            result.Should().BeFalse("a null redirect URI must be rejected");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Empty_redirect_uri_should_be_rejected()
        {
            var client = ClientWithRedirectUri("https://app.example.com/callback");

            var result = await _validator.IsRedirectUriValidAsync(string.Empty, client);

            result.Should().BeFalse("an empty redirect URI must be rejected");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Multiple_registered_uris_matched_by_exact_value_should_succeed()
        {
            var client = new Client
            {
                RedirectUris = new List<string>
                {
                    "https://app.example.com/callback",
                    "https://app.example.com/silent-renew"
                }
            };

            var result = await _validator.IsRedirectUriValidAsync(
                "https://app.example.com/silent-renew", client);

            result.Should().BeTrue("any registered redirect URI must be accepted by exact match");
        }
    }
}
