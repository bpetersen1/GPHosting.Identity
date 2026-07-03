---
sidebar_position: 1
---

# Observability

GPHosting.Identity instruments itself using only `System.Diagnostics.Activity` and
`System.Diagnostics.Metrics` from the base class library — the core package takes **no dependency
on the OpenTelemetry SDK or any specific vendor**. Whether (and how) you export that data is
entirely up to your host application.

## Tracing

Spans are emitted under a single `ActivitySource` named `GPHosting.Identity`
(`IdentityServerActivitySource.Name`), covering the token endpoint and authorize endpoint request
pipelines. To collect them, register the source with an OpenTelemetry `TracerProvider` in your
host:

```csharp
using GPHosting.Identity.Telemetry;

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("my-identity-server"))
    .WithTracing(tracing => tracing
        .AddSource(IdentityServerActivitySource.Name)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter()); // or AddConsoleExporter() while developing
```

This requires adding the OpenTelemetry SDK packages to your **host** project — they're not
transitive dependencies of `GPHosting.Identity`:

```shell
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
```

## Metrics

Counters are emitted under a `Meter` also named `GPHosting.Identity`
(`IdentityServerMetrics.MeterName`):

| Metric | Type | Tags | Meaning |
|---|---|---|---|
| `identityserver.tokens.issued` | Counter | `grant_type`, `client_id`, `token_type` | Tokens issued from the token or authorize endpoint |
| `identityserver.token_requests.errors` | Counter | `error`, `client_id` | Failed token requests |
| `identityserver.authorize_requests` | Counter | `outcome` (`success`, `error`, `login`, `consent`, `redirect`) | Authorize requests processed |

Wire them into a `MeterProvider` the same way:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter(IdentityServerMetrics.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());
```

`identityserver.token_requests.errors` and the `outcome` tag on `authorize_requests` are usually
the first thing worth alerting on — a spike in either is a strong early signal of either an
attack (credential stuffing, invalid grant attempts) or a misconfigured client after a deploy.

## Health checks

Two independent pieces, since a healthy core library and a healthy database are different
concerns:

**Core (always available):**

```csharp
builder.Services.AddIdentityServer()
    .AddIdentityServerHealthChecks();
```

Registers `identityserver_signing_credential` — fails if no signing credential is configured or
resolvable, which would mean the server can start but can't actually issue valid tokens.

**Store connectivity (only if using EF Core stores — see [Stores](/docs/fundamentals/stores)):**

```csharp
builder.Services.AddIdentityServer()
    .AddConfigurationStore(...)
    .AddOperationalStore(...)
    .AddConfigurationStoreHealthCheck()
    .AddOperationalStoreHealthCheck();
```

Both use ASP.NET Core's standard health check middleware, so they show up wherever you're already
mapping health check endpoints:

```csharp
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

Use `/health/live` (no predicate — process is running) for liveness probes and `/health/ready`
(tagged `ready` — signing credential and databases reachable) for readiness probes in Kubernetes
or any orchestrator that distinguishes the two; a pod that's alive but can't reach its database
should be taken out of the load balancer rotation without being restarted.

## Next steps

- [Stores](/docs/fundamentals/stores) — the database connectivity these health checks verify
- [Configuration](/docs/configuration/options) — the separate `Events` system for
  success/failure/error event sinks, distinct from the metrics/tracing covered here
