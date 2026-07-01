// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using GPHosting.Identity.Extensions;
using GPHosting.Identity.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GPHosting.Identity.Endpoints.Results;
/// <summary>
/// Result for a discovery document
/// </summary>
/// <seealso cref="GPHosting.Identity.Hosting.IEndpointResult" />
public class DiscoveryDocumentResult : IEndpointResult
{
    /// <summary>
    /// Gets the entries.
    /// </summary>
    /// <value>
    /// The entries.
    /// </value>
    public Dictionary<string, object> Entries { get; }

    /// <summary>
    /// Gets the maximum age.
    /// </summary>
    /// <value>
    /// The maximum age.
    /// </value>
    public int? MaxAge { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveryDocumentResult" /> class.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="maxAge">The maximum age.</param>
    /// <exception cref="System.ArgumentNullException">entries</exception>
    public DiscoveryDocumentResult(Dictionary<string, object> entries, int? maxAge)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = entries;
        MaxAge = maxAge;
    }

    /// <summary>
    /// Executes the result.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns></returns>
    public Task ExecuteAsync(HttpContext context)
    {
        if (MaxAge.HasValue && MaxAge.Value >= 0)
        {
            context.Response.SetCache(MaxAge.Value, "Origin");
        }

        return context.Response.WriteJsonAsync(Entries);
    }
}
