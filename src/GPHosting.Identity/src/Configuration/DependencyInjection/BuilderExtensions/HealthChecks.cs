// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using GPHosting.Identity.Telemetry.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;
/// <summary>
/// Builder extension methods for registering health checks
/// </summary>
public static class IdentityServerBuilderExtensionsHealthChecks
{
    /// <summary>
    /// Registers ASP.NET Core health checks for IdentityServer, currently covering signing
    /// credential availability. Store-specific checks (e.g. EF Core connectivity) are added
    /// separately by the corresponding store package.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddIdentityServerHealthChecks(this IIdentityServerBuilder builder)
    {
        builder.Services
            .AddHealthChecks()
            .AddCheck<SigningCredentialHealthCheck>(
                "identityserver_signing_credential",
                tags: ["identityserver", "ready"]);

        return builder;
    }
}
