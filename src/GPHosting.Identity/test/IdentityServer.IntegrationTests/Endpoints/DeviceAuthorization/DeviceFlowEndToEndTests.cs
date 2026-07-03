// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityModel.Client;
using IdentityServer.IntegrationTests.Common;
using GPHosting.Identity;
using GPHosting.Identity.Models;
using GPHosting.Identity.Services;
using GPHosting.Identity.Test;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IdentityServer.IntegrationTests.Endpoints.DeviceAuthorization
{
    /// <summary>
    /// The unit tests for device flow (request validation, code validation, response generation,
    /// throttling, storage) all exercise their piece in isolation. This test drives the actual
    /// HTTP endpoints end-to-end — device authorization, the pending-authorization poll, simulated
    /// user approval, and the final token issuance — to confirm the whole pipeline actually wires
    /// together, not just that each piece works alone.
    /// </summary>
    public class DeviceFlowEndToEndTests
    {
        private const string Category = "Device flow - end to end";

        private IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();

        public DeviceFlowEndToEndTests()
        {
            _mockPipeline.Clients.Add(new Client
            {
                ClientId = "device.client",
                AllowedGrantTypes = GrantTypes.DeviceFlow,
                ClientSecrets = { new Secret("secret".Sha256()) },
                AllowedScopes = { "openid", "api1" },
                AllowOfflineAccess = true
            });

            _mockPipeline.Users.Add(new TestUser { SubjectId = "bob", Username = "bob" });
            _mockPipeline.IdentityScopes.Add(new IdentityResources.OpenId());
            _mockPipeline.ApiScopes.Add(new ApiScope("api1"));

            _mockPipeline.Initialize();

            // short polling interval so the test doesn't need a long real-time wait between polls
            _mockPipeline.Options.DeviceFlow.Interval = 1;
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Full_device_flow_loop_should_issue_a_token_once_approved()
        {
            // 1. Device requests a device_code/user_code pair from the real endpoint.
            var authResponse = await _mockPipeline.BackChannelClient.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
            {
                Address = IdentityServerPipeline.DeviceAuthorization,
                ClientId = "device.client",
                ClientSecret = "secret",
                Scope = "openid api1"
            });

            authResponse.IsError.Should().BeFalse();
            authResponse.DeviceCode.Should().NotBeNullOrEmpty();
            authResponse.UserCode.Should().NotBeNullOrEmpty();

            // 2. Before approval, polling the real token endpoint must report authorization_pending
            // — proves the "not yet approved" path is wired correctly, not just assumed.
            var pendingResponse = await _mockPipeline.BackChannelClient.RequestDeviceTokenAsync(new DeviceTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = "device.client",
                ClientSecret = "secret",
                DeviceCode = authResponse.DeviceCode
            });

            pendingResponse.IsError.Should().BeTrue();
            pendingResponse.Error.Should().Be("authorization_pending");

            // 3. Simulate the user approving the code on a second device via the verification page
            // — done directly through IDeviceFlowCodeService (the hashing wrapper around
            // IDeviceFlowStore that the real endpoints use) rather than driving the real
            // login/consent UI, the same simplification the rest of this test suite uses for login
            // (LoginAsync sets Subject directly rather than posting the actual login form).
            var deviceFlowCodeService = _mockPipeline.Server.Services.GetRequiredService<IDeviceFlowCodeService>();
            var deviceCode = await deviceFlowCodeService.FindByUserCodeAsync(authResponse.UserCode);
            deviceCode.Should().NotBeNull("the device authorization request must actually be persisted for the verification page to find it");

            deviceCode.IsAuthorized = true;
            deviceCode.Subject = new IdentityServerUser("bob")
            {
                AuthenticationTime = DateTime.UtcNow,
                IdentityProvider = IdentityServerConstants.LocalIdentityProvider
            }.CreatePrincipal();
            deviceCode.AuthorizedScopes = new[] { "openid", "api1" };
            await deviceFlowCodeService.UpdateByUserCodeAsync(authResponse.UserCode, deviceCode);

            // 4. Now polling the token endpoint must actually issue a token. Wait out the polling
            // interval first — polling faster than that is expected to (and does) return slow_down.
            await Task.Delay(1200);

            var tokenResponse = await _mockPipeline.BackChannelClient.RequestDeviceTokenAsync(new DeviceTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = "device.client",
                ClientSecret = "secret",
                DeviceCode = authResponse.DeviceCode
            });

            tokenResponse.IsError.Should().BeFalse("once approved, the device's poll must succeed and return a real token");
            tokenResponse.AccessToken.Should().NotBeNullOrEmpty();
            tokenResponse.IdentityToken.Should().NotBeNullOrEmpty("openid was requested and approved");
        }
    }
}
