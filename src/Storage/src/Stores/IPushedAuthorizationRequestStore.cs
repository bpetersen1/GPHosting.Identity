// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using GPHosting.Identity.Models;
using System.Threading.Tasks;

namespace GPHosting.Identity.Stores;
/// <summary>
/// Store for pushed authorization requests (RFC 9126).
/// </summary>
public interface IPushedAuthorizationRequestStore
{
    /// <summary>
    /// Stores a pushed authorization request.
    /// </summary>
    Task StoreAsync(PushedAuthorizationRequest request);

    /// <summary>
    /// Retrieves a pushed authorization request by its reference value.
    /// Returns null if not found or expired.
    /// </summary>
    Task<PushedAuthorizationRequest?> GetAsync(string referenceValue);

    /// <summary>
    /// Removes a pushed authorization request by its reference value.
    /// </summary>
    Task RemoveAsync(string referenceValue);
}
