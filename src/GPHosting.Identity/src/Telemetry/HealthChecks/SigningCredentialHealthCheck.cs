// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System;
using System.Threading;
using System.Threading.Tasks;
using GPHosting.Identity.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GPHosting.Identity.Telemetry.HealthChecks;
/// <summary>
/// Health check that verifies at least one signing credential is configured and retrievable.
/// A server that cannot produce a signing credential cannot issue tokens.
/// </summary>
internal class SigningCredentialHealthCheck : IHealthCheck
{
    private readonly IKeyMaterialService _keyMaterialService;

    public SigningCredentialHealthCheck(IKeyMaterialService keyMaterialService)
    {
        _keyMaterialService = keyMaterialService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var credential = await _keyMaterialService.GetSigningCredentialsAsync();
            if (credential == null)
            {
                return HealthCheckResult.Unhealthy("No signing credential is available.");
            }

            return HealthCheckResult.Healthy($"Signing credential available (alg={credential.Algorithm}).");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to retrieve a signing credential.", ex);
        }
    }
}
