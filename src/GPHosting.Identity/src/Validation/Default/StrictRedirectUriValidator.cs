// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Modifications Copyright (c) GP Hosting.


using GPHosting.Identity.Extensions;
using GPHosting.Identity.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GPHosting.Identity.Validation;
/// <summary>
/// Default implementation of redirect URI validator. Validates the URIs against
/// the client's configured URIs.
/// </summary>
public class StrictRedirectUriValidator : IRedirectUriValidator
{
    /// <summary>
    /// Checks if a given URI string is in a collection of strings using strict per-component
    /// comparison. Scheme and host are compared case-insensitively (RFC 3986 §6.2.2.1);
    /// port, path, and query are compared with ordinal (case-sensitive) equality.
    /// Parsing both URIs before comparison prevents path-encoding and case-bypass attacks
    /// (CVE-2022-24306).
    /// </summary>
    /// <param name="uris">The uris.</param>
    /// <param name="requestedUri">The requested URI.</param>
    /// <returns></returns>
    protected bool StringCollectionContainsString(IEnumerable<string> uris, string requestedUri)
    {
        if (uris.IsNullOrEmpty()) return false;

        if (!Uri.TryCreate(requestedUri, UriKind.Absolute, out var requested))
            return false;

        foreach (var uri in uris)
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out var configured))
                continue;

            if (string.Equals(requested.Scheme, configured.Scheme, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(requested.Host, configured.Host, StringComparison.OrdinalIgnoreCase) &&
                requested.Port == configured.Port &&
                string.Equals(requested.AbsolutePath, configured.AbsolutePath, StringComparison.Ordinal) &&
                string.Equals(requested.Query, configured.Query, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether a redirect URI is valid for a client.
    /// </summary>
    /// <param name="requestedUri">The requested URI.</param>
    /// <param name="client">The client.</param>
    /// <returns>
    ///   <c>true</c> is the URI is valid; <c>false</c> otherwise.
    /// </returns>
    public virtual Task<bool> IsRedirectUriValidAsync(string requestedUri, Client client)
    {
        return Task.FromResult(StringCollectionContainsString(client.RedirectUris, requestedUri));
    }

    /// <summary>
    /// Determines whether a post logout URI is valid for a client.
    /// </summary>
    /// <param name="requestedUri">The requested URI.</param>
    /// <param name="client">The client.</param>
    /// <returns>
    ///   <c>true</c> is the URI is valid; <c>false</c> otherwise.
    /// </returns>
    public virtual Task<bool> IsPostLogoutRedirectUriValidAsync(string requestedUri, Client client)
    {
        return Task.FromResult(StringCollectionContainsString(client.PostLogoutRedirectUris, requestedUri));
    }
}
