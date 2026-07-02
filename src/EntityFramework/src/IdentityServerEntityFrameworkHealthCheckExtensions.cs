// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using GPHosting.Identity.EntityFramework.DbContexts;
using GPHosting.Identity.EntityFramework.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Microsoft.Extensions.DependencyInjection;
/// <summary>
/// Extension methods to add EF Core database health checks for IdentityServer's stores.
/// </summary>
public static class IdentityServerEntityFrameworkHealthCheckExtensions
{
    /// <summary>
    /// Adds a health check that verifies connectivity to the configuration store's database,
    /// assuming the default <see cref="ConfigurationDbContext"/>. Use the generic overload if
    /// <c>AddConfigurationStore&lt;TContext&gt;</c> was called with a custom context type.
    /// </summary>
    public static IIdentityServerBuilder AddConfigurationStoreHealthCheck(this IIdentityServerBuilder builder) =>
        builder.AddConfigurationStoreHealthCheck<ConfigurationDbContext>();

    /// <summary>
    /// Adds a health check that verifies connectivity to the configuration store's database.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type registered via <c>AddConfigurationStore&lt;TContext&gt;</c>.</typeparam>
    public static IIdentityServerBuilder AddConfigurationStoreHealthCheck<TContext>(this IIdentityServerBuilder builder)
        where TContext : DbContext
    {
        builder.Services
            .AddHealthChecks()
            .AddCheck<DbContextHealthCheck<TContext>>(
                "identityserver_configuration_store",
                tags: ["identityserver", "ready", "db"]);

        return builder;
    }

    /// <summary>
    /// Adds a health check that verifies connectivity to the operational (persisted grant) store's
    /// database, assuming the default <see cref="PersistedGrantDbContext"/>. Use the generic overload
    /// if <c>AddOperationalStore&lt;TContext&gt;</c> was called with a custom context type.
    /// </summary>
    public static IIdentityServerBuilder AddOperationalStoreHealthCheck(this IIdentityServerBuilder builder) =>
        builder.AddOperationalStoreHealthCheck<PersistedGrantDbContext>();

    /// <summary>
    /// Adds a health check that verifies connectivity to the operational (persisted grant) store's database.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type registered via <c>AddOperationalStore&lt;TContext&gt;</c>.</typeparam>
    public static IIdentityServerBuilder AddOperationalStoreHealthCheck<TContext>(this IIdentityServerBuilder builder)
        where TContext : DbContext
    {
        builder.Services
            .AddHealthChecks()
            .AddCheck<DbContextHealthCheck<TContext>>(
                "identityserver_operational_store",
                tags: ["identityserver", "ready", "db"]);

        return builder;
    }
}
