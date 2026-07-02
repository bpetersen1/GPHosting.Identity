// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System.Diagnostics;
using System.Reflection;

namespace GPHosting.Identity.Telemetry;
/// <summary>
/// Provides the <see cref="System.Diagnostics.ActivitySource"/> used to emit distributed tracing spans
/// for IdentityServer request processing. Host applications wire this into an OpenTelemetry
/// TracerProvider via <c>AddSource(IdentityServerActivitySource.Name)</c>.
/// </summary>
public static class IdentityServerActivitySource
{
    /// <summary>
    /// The name used to register this activity source with an OpenTelemetry TracerProvider.
    /// </summary>
    public const string Name = "GPHosting.Identity";

    private static readonly string Version =
        typeof(IdentityServerActivitySource).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(IdentityServerActivitySource).Assembly.GetName().Version?.ToString()
        ?? "1.0.0";

    /// <summary>
    /// The shared <see cref="ActivitySource"/> instance used throughout IdentityServer.
    /// </summary>
    public static readonly ActivitySource Source = new(Name, Version);
}
