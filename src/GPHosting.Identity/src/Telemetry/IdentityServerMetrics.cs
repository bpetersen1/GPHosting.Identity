// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System.Diagnostics.Metrics;

namespace GPHosting.Identity.Telemetry;
/// <summary>
/// Provides the <see cref="System.Diagnostics.Metrics.Meter"/> and instruments used to emit
/// IdentityServer metrics. Host applications wire this into an OpenTelemetry MeterProvider
/// via <c>AddMeter(IdentityServerMetrics.MeterName)</c>.
/// </summary>
public static class IdentityServerMetrics
{
    /// <summary>
    /// The name used to register this meter with an OpenTelemetry MeterProvider.
    /// </summary>
    public const string MeterName = "GPHosting.Identity";

    private static readonly Meter Meter = new(MeterName);

    /// <summary>
    /// Number of tokens issued from the token endpoint or authorize endpoint, tagged by
    /// <c>grant_type</c>, <c>client_id</c>, and <c>token_type</c>.
    /// </summary>
    public static readonly Counter<long> TokensIssued = Meter.CreateCounter<long>(
        "identityserver.tokens.issued",
        unit: "{token}",
        description: "Number of tokens issued.");

    /// <summary>
    /// Number of failed token requests, tagged by <c>error</c> and <c>client_id</c>.
    /// </summary>
    public static readonly Counter<long> TokenRequestErrors = Meter.CreateCounter<long>(
        "identityserver.token_requests.errors",
        unit: "{request}",
        description: "Number of failed token requests.");

    /// <summary>
    /// Number of authorize requests processed, tagged by <c>outcome</c> (success, error, login, consent, redirect).
    /// </summary>
    public static readonly Counter<long> AuthorizeRequests = Meter.CreateCounter<long>(
        "identityserver.authorize_requests",
        unit: "{request}",
        description: "Number of authorize requests processed, tagged by outcome.");
}
