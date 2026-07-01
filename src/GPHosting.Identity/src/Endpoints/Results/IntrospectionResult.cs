// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System.Collections.Generic;
using System.Threading.Tasks;
using GPHosting.Identity.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using GPHosting.Identity.Extensions;

namespace GPHosting.Identity.Endpoints.Results;
/// <summary>
/// Result for introspection
/// </summary>
/// <seealso cref="GPHosting.Identity.Hosting.IEndpointResult" />
public class IntrospectionResult : IEndpointResult
{
    /// <summary>
    /// Gets the result.
    /// </summary>
    /// <value>
    /// The result.
    /// </value>
    public Dictionary<string, object> Entries { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntrospectionResult"/> class.
    /// </summary>
    /// <param name="entries">The result.</param>
    /// <exception cref="System.ArgumentNullException">result</exception>
    public IntrospectionResult(Dictionary<string, object> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = entries;
    }

    /// <summary>
    /// Executes the result.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns></returns>
    public Task ExecuteAsync(HttpContext context)
    {
        context.Response.SetNoCache();
        
        return context.Response.WriteJsonAsync(Entries);
    }
}
