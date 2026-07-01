// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using GPHosting.Identity.Models;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace GPHosting.Identity.Stores;
/// <summary>
/// In-memory store for pushed authorization requests (RFC 9126).
/// </summary>
public class InMemoryPushedAuthorizationRequestStore : IPushedAuthorizationRequestStore
{
    private readonly ConcurrentDictionary<string, PushedAuthorizationRequest> _store = new();

    /// <inheritdoc/>
    public Task StoreAsync(PushedAuthorizationRequest request)
    {
        _store[request.ReferenceValue] = request;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<PushedAuthorizationRequest?> GetAsync(string referenceValue)
    {
        if (_store.TryGetValue(referenceValue, out var request))
        {
            if (request.ExpiresAt > DateTime.UtcNow)
                return Task.FromResult<PushedAuthorizationRequest?>(request);

            _store.TryRemove(referenceValue, out _);
        }

        return Task.FromResult<PushedAuthorizationRequest?>(null);
    }

    /// <inheritdoc/>
    public Task RemoveAsync(string referenceValue)
    {
        _store.TryRemove(referenceValue, out _);
        return Task.CompletedTask;
    }
}
