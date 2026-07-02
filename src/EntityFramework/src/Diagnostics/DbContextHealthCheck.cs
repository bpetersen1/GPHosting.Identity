// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GPHosting.Identity.EntityFramework.Diagnostics;
/// <summary>
/// Health check that verifies the given <typeparamref name="TContext"/> can connect to its
/// underlying database.
/// </summary>
/// <typeparam name="TContext">The DbContext type to check.</typeparam>
internal class DbContextHealthCheck<TContext> : IHealthCheck
    where TContext : DbContext
{
    private readonly TContext _dbContext;

    public DbContextHealthCheck(TContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy($"{typeof(TContext).Name} can connect to its database.")
                : HealthCheckResult.Unhealthy($"{typeof(TContext).Name} cannot connect to its database.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"{typeof(TContext).Name} health check threw an exception.", ex);
        }
    }
}
