// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;

namespace GPHosting.Identity.Models;
/// <summary>
/// Represents a pushed authorization request (RFC 9126).
/// </summary>
public class PushedAuthorizationRequest
{
    /// <summary>
    /// The unique reference value used as the request_uri handle.
    /// </summary>
    public string ReferenceValue { get; set; } = default!;

    /// <summary>
    /// UTC time at which the pushed request expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// JSON-encoded form parameters from the original authorization request.
    /// </summary>
    public string Parameters { get; set; } = default!;
}
