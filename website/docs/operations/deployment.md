---
sidebar_position: 1
---

# Deployment

GPHosting.Identity is a normal ASP.NET Core application — it deploys like any other ASP.NET Core
app, but a few settings matter specifically because it's an OIDC token server sitting behind
infrastructure that rewrites requests.

## Set `IssuerUri` explicitly

```csharp
builder.Services.AddIdentityServer(options =>
{
    options.IssuerUri = "https://identity.example.com";
});
```

Left unset, the issuer is inferred from the incoming request's scheme/host. That works for a
single instance with no proxy in front of it, but breaks the moment you're behind a load balancer,
a reverse proxy that terminates TLS, or any setup where the request `Host` header doesn't match
the public-facing URL. Every token's `iss` claim, and the discovery document's `issuer` field,
must match what clients actually configured as the authority — a mismatch here fails token
validation on the client/API side with an error that doesn't obviously point back to this setting.

## Forwarded headers behind a reverse proxy

If TLS terminates at a reverse proxy (nginx, a cloud load balancer) and GPHosting.Identity itself
runs on plain HTTP behind it, ASP.NET Core needs to know the *original* scheme/host to build
correct URLs (redirect URIs, discovery document endpoints):

```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
```

This must run early in the pipeline — before anything that reads `HttpContext.Request.Scheme` or
builds absolute URLs, which for an identity server is essentially everything. Configure your
proxy to actually send `X-Forwarded-For`/`X-Forwarded-Proto`, and if the proxy isn't a known,
trusted host, also configure `KnownProxies`/`KnownNetworks` on `ForwardedHeadersOptions` — trusting
forwarded headers from an untrusted source lets a client spoof its own scheme/host.

## Client certificate forwarding (mTLS)

If you're using mutual TLS client authentication (`IdentityServerOptions.MutualTls`) behind nginx,
the client certificate needs to be forwarded from the proxy (which terminates the TLS handshake)
to the app as a header:

```csharp
services.AddCertificateForwarding(options =>
{
    options.CertificateHeader = "X-SSL-CERT";
    options.HeaderConverter = headerValue =>
    {
        if (string.IsNullOrWhiteSpace(headerValue)) return null;
        var bytes = Encoding.UTF8.GetBytes(Uri.UnescapeDataString(headerValue));
        return new X509Certificate2(bytes);
    };
});
```

Paired with an nginx config that sets `proxy_set_header X-SSL-CERT $ssl_client_cert;` (or your
proxy's equivalent). Only relevant if you're actually using mTLS — most deployments aren't.

## Multiple instances

Two things that don't "just work" the moment you scale beyond one instance:

- **Signing keys** — `AddDeveloperSigningCredential()` generates and caches a key to a local file
  (`tempkey.jwk` by default); each instance would generate its *own* key unless they share a
  filesystem, meaning tokens issued by one instance won't validate against another. Use a real
  certificate or shared key store instead — see [Signing Keys](/docs/fundamentals/signing-keys).
- **Stores** — in-memory clients/scopes/grants are per-instance by definition. Any deployment with
  more than one instance needs the EF Core-backed stores (or a custom store implementation) so all
  instances see the same clients and persisted grants — see [Stores](/docs/fundamentals/stores).

Session affinity (sticky sessions) at the load balancer is **not** a substitute for shared stores —
it might mask the problem during a single user's session, but breaks the moment an instance
restarts or a request lands on a different node (which will happen).

## Containerized deployment

No GPHosting.Identity-specific container image is published — build your own from the standard
ASP.NET Core base images:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish "YourHost.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "YourHost.dll"]
```

In Kubernetes, map the health checks from [Observability](/docs/observability/opentelemetry) to
probes directly:

```yaml
livenessProbe:
  httpGet: { path: /health/live, port: 8080 }
readinessProbe:
  httpGet: { path: /health/ready, port: 8080 }
```

Feed the signing key and database connection string in via a mounted secret / environment
variable — never bake either into the container image.

## Next steps

- [Signing Keys](/docs/fundamentals/signing-keys) — production key management, referenced above
- [Stores](/docs/fundamentals/stores) — shared configuration/grant storage across instances
- [Observability](/docs/observability/opentelemetry) — the health check endpoints used above
