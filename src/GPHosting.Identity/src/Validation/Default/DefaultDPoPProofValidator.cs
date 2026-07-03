// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MsTokenValidationResult = Microsoft.IdentityModel.Tokens.TokenValidationResult;

namespace GPHosting.Identity.Validation;

public class DefaultDPoPProofValidator : IDPoPProofValidator
{
    private static readonly TimeSpan _clockSkew = TimeSpan.FromMinutes(5);

    private readonly ILogger _logger;

    public DefaultDPoPProofValidator(ILogger<DefaultDPoPProofValidator> logger)
    {
        _logger = logger;
    }

    public async Task<DPoPProofValidationResult> ValidateAsync(DPoPProofValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(context.ProofToken))
            return Fail("invalid_dpop_proof", "DPoP proof token is missing");

        // Parse the three-part JWT structure
        var parts = context.ProofToken.Split('.');
        if (parts.Length != 3)
            return Fail("invalid_dpop_proof", "Malformed DPoP proof JWT");

        // Decode and parse header
        string headerJson;
        try
        {
            headerJson = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[0]));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to decode DPoP proof JWT header");
            return Fail("invalid_dpop_proof", "Malformed DPoP proof JWT header");
        }

        JsonElement header;
        try
        {
            using var doc = JsonDocument.Parse(headerJson);
            header = doc.RootElement.Clone();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse DPoP proof JWT header");
            return Fail("invalid_dpop_proof", "Malformed DPoP proof JWT header");
        }

        // Validate typ = "dpop+jwt"
        if (!header.TryGetProperty("typ", out var typEl) ||
            !string.Equals(typEl.GetString(), "dpop+jwt", StringComparison.OrdinalIgnoreCase))
        {
            return Fail("invalid_dpop_proof", "DPoP proof JWT must have typ=dpop+jwt");
        }

        // Extract and validate jwk
        if (!header.TryGetProperty("jwk", out var jwkEl))
            return Fail("invalid_dpop_proof", "DPoP proof JWT missing jwk header parameter");

        JsonWebKey jwk;
        try
        {
            jwk = JsonWebKey.Create(jwkEl.GetRawText());
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse JWK from DPoP proof header");
            return Fail("invalid_dpop_proof", "DPoP proof JWT has invalid jwk header parameter");
        }

        // Reject private keys — D is the private exponent for RSA and private scalar for EC
        if (!string.IsNullOrEmpty(jwk.D))
            return Fail("invalid_dpop_proof", "DPoP proof JWT jwk must not contain private key material");

        // Validate signature using the embedded public key.
        // RFC 9449 §4.2: DPoP proofs require iat but NOT exp — recency is enforced via the
        // iat window check below. ValidateLifetime/RequireExpirationTime are therefore false;
        // however if exp IS present we validate it explicitly after this block.
        var handler = new JsonWebTokenHandler();
        // DPoP proofs (RFC 9449) are self-signed by the client's own embedded jwk, not issued by any
        // authority — there is no issuer/audience to validate. They use iat, not exp, for recency
        // (checked explicitly above); the iat window enforces recency without RequireExpirationTime.
        var validationParams = new TokenValidationParameters // nosemgrep: gphosting-jwt-issuer-not-validated,gphosting-jwt-lifetime-not-validated,jwt-tokenvalidationparameters-no-expiry-validation
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,          // nosemgrep: csharp.lang.security.ad.jwt-tokenvalidationparameters-no-expiry-validation.jwt-tokenvalidationparameters-no-expiry-validation
            RequireExpirationTime = false,      // nosemgrep: csharp.lang.security.ad.jwt-tokenvalidationparameters-no-expiry-validation.jwt-tokenvalidationparameters-no-expiry-validation
            RequireSignedTokens = true,
            IssuerSigningKey = jwk
        };

        MsTokenValidationResult tokenResult;
        try
        {
            tokenResult = await handler.ValidateTokenAsync(context.ProofToken, validationParams);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DPoP proof JWT signature validation threw");
            return Fail("invalid_dpop_proof", "DPoP proof JWT signature validation failed");
        }

        if (!tokenResult.IsValid)
        {
            _logger.LogDebug("DPoP proof JWT signature validation failed: {exception}", tokenResult.Exception?.Message);
            return Fail("invalid_dpop_proof", "DPoP proof JWT signature validation failed");
        }

        var jwt = (JsonWebToken)tokenResult.SecurityToken;

        // Defense-in-depth: if exp is present, reject expired tokens even though RFC 9449 doesn't require it
        if (jwt.TryGetPayloadValue<long>("exp", out var expEpoch))
        {
            var exp = DateTimeOffset.FromUnixTimeSeconds(expEpoch).UtcDateTime;
            if (DateTime.UtcNow > exp)
                return Fail("invalid_dpop_proof", "DPoP proof JWT has expired");
        }

        // Validate htm claim
        if (!jwt.TryGetPayloadValue<string>("htm", out var htm) ||
            !string.Equals(htm, context.HttpMethod, StringComparison.OrdinalIgnoreCase))
        {
            return Fail("invalid_dpop_proof", "DPoP proof htm claim does not match HTTP method");
        }

        // Validate htu claim — compare scheme + host + path, ignore query string
        if (!jwt.TryGetPayloadValue<string>("htu", out var htu))
            return Fail("invalid_dpop_proof", "DPoP proof missing htu claim");

        Uri htuUri, requestUri;
        try
        {
            htuUri = new Uri(htu, UriKind.Absolute);
            requestUri = new Uri(context.HttpUrl, UriKind.Absolute);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse DPoP proof htu or request URL");
            return Fail("invalid_dpop_proof", "DPoP proof htu claim is not a valid absolute URI");
        }

        var htuPath = htuUri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        var reqPath = requestUri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        if (!string.Equals(htuPath, reqPath, StringComparison.OrdinalIgnoreCase))
            return Fail("invalid_dpop_proof", "DPoP proof htu claim does not match request URL");

        // Validate iat within 5-minute window
        if (!jwt.TryGetPayloadValue<long>("iat", out var iatEpoch))
            return Fail("invalid_dpop_proof", "DPoP proof missing or invalid iat claim");

        var iat = DateTimeOffset.FromUnixTimeSeconds(iatEpoch).UtcDateTime;
        if (Math.Abs((DateTime.UtcNow - iat).TotalSeconds) > _clockSkew.TotalSeconds)
            return Fail("invalid_dpop_proof", "DPoP proof iat claim is outside the acceptable time window");

        // Validate jti is present (uniqueness enforcement out of scope for this scaffold)
        if (!jwt.TryGetPayloadValue<string>("jti", out var jti) || string.IsNullOrEmpty(jti))
            return Fail("invalid_dpop_proof", "DPoP proof missing jti claim");

        // Compute JWK thumbprint and build cnf confirmation value
        string thumbprint;
        try
        {
            thumbprint = Base64UrlEncoder.Encode(jwk.ComputeJwkThumbprint());
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to compute JWK thumbprint");
            return Fail("invalid_dpop_proof", "Failed to compute JWK thumbprint");
        }

        var confirmation = $"{{\"jkt\":\"{thumbprint}\"}}";

        _logger.LogDebug("DPoP proof validated successfully. jti={jti}", jti);

        return new DPoPProofValidationResult { Confirmation = confirmation };
    }

    private static DPoPProofValidationResult Fail(string error, string description) =>
        new DPoPProofValidationResult { IsError = true, Error = error, ErrorDescription = description };
}
