using GPHosting.Identity.Configuration;
using GPHosting.Identity.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GPHosting.Identity.Validation
{
    /// <summary>
    /// Default client configuration validator
    /// </summary>
    /// <seealso cref="GPHosting.Identity.Validation.IClientConfigurationValidator" />
    public class DefaultClientConfigurationValidator : IClientConfigurationValidator
    {
        private readonly IdentityServerOptions _options;
        private readonly ILogger<DefaultClientConfigurationValidator> _logger;

        /// <summary>
        /// Constructor for DefaultClientConfigurationValidator
        /// </summary>
        public DefaultClientConfigurationValidator(IdentityServerOptions options, ILogger<DefaultClientConfigurationValidator> logger)
        {
            _options = options;
            _logger = logger;
        }

        /// <summary>
        /// Determines whether the configuration of a client is valid.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public async Task ValidateAsync(ClientConfigurationValidationContext context)
        {
            if (context.Client.ProtocolType == IdentityServerConstants.ProtocolTypes.OpenIdConnect)
            {
                await ValidateGrantTypesAsync(context);
                if (context.IsValid == false) return;

                await ValidateLifetimesAsync(context);
                if (context.IsValid == false) return;

                await ValidateRedirectUriAsync(context);
                if (context.IsValid == false) return;

                await ValidateAllowedCorsOriginsAsync(context);
                if (context.IsValid == false) return;

                await ValidateUriSchemesAsync(context);
                if (context.IsValid == false) return;

                await ValidateSecretsAsync(context);
                if (context.IsValid == false) return;

                await ValidatePropertiesAsync(context);
                if (context.IsValid == false) return;
            }
        }

        /// <summary>
        /// Validates grant type related configuration settings.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        protected virtual Task ValidateGrantTypesAsync(ClientConfigurationValidationContext context)
        {
            if (context.Client.AllowedGrantTypes?.Any() != true)
            {
                context.SetError("no allowed grant type specified");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Validates lifetime related configuration settings.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        protected virtual Task ValidateLifetimesAsync(ClientConfigurationValidationContext context)
        {
            if (context.Client.AccessTokenLifetime <= 0)
            {
                context.SetError("access token lifetime is 0 or negative");
                return Task.CompletedTask;
            }

            if (context.Client.IdentityTokenLifetime <= 0)
            {
                context.SetError("identity token lifetime is 0 or negative");
                return Task.CompletedTask;
            }

            if (context.Client.AllowedGrantTypes?.Contains(GrantType.DeviceFlow) == true
                && context.Client.DeviceCodeLifetime <= 0)
            {
                context.SetError("device code lifetime is 0 or negative");
            }

            // 0 means unlimited lifetime
            if (context.Client.AbsoluteRefreshTokenLifetime < 0)
            {
                context.SetError("absolute refresh token lifetime is negative");
                return Task.CompletedTask;
            }

            // 0 might mean that sliding is disabled
            if (context.Client.SlidingRefreshTokenLifetime < 0)
            {
                context.SetError("sliding refresh token lifetime is negative");
                return Task.CompletedTask;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Validates redirect URI related configuration.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        protected virtual Task ValidateRedirectUriAsync(ClientConfigurationValidationContext context)
        {
            if (context.Client.AllowedGrantTypes?.Any() == true)
            {
                if (context.Client.AllowedGrantTypes.Contains(GrantType.AuthorizationCode) ||
                    context.Client.AllowedGrantTypes.Contains(GrantType.Hybrid) ||
                    context.Client.AllowedGrantTypes.Contains(GrantType.Implicit))
                {
                    if (context.Client.RedirectUris?.Any() == false)
                    {
                        context.SetError("No redirect URI configured.");
                    }
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Validates allowed CORS origins for valid format.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        protected virtual Task ValidateAllowedCorsOriginsAsync(ClientConfigurationValidationContext context)
        {
            if (context.Client.AllowedCorsOrigins?.Any() == true)
            {
                foreach (var origin in context.Client.AllowedCorsOrigins)
                {
                    var fail = true;

                    if (!string.IsNullOrWhiteSpace(origin) && Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    {
                        if (uri.AbsolutePath == "/" && !origin.EndsWith("/"))
                        {
                            fail = false;
                        }
                    }

                    if (fail)
                    {
                        if (!string.IsNullOrWhiteSpace(origin))
                        {
                            context.SetError($"AllowedCorsOrigins contains invalid origin: {origin}");
                        }
                        else
                        {
                            context.SetError($"AllowedCorsOrigins contains invalid origin. There is an empty value.");
                        }
                        return Task.CompletedTask;
                    }
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Validates that URI schemes is not in the list of invalid URI scheme prefixes, as controlled by the ValidationOptions.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected virtual Task ValidateUriSchemesAsync(ClientConfigurationValidationContext context)
        {
            if (context.Client.RedirectUris?.Any() == true)
            {
                foreach (var uri in context.Client.RedirectUris)
                {
                    if (_options.Validation.InvalidRedirectUriPrefixes
                            .Any(scheme => uri?.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) == true))
                    {
                        context.SetError($"RedirectUri '{uri}' uses invalid scheme. If this scheme should be allowed, then configure it via ValidationOptions.");
                    }
                }
            }

            if (context.Client.PostLogoutRedirectUris?.Any() == true)
            {
                foreach (var uri in context.Client.PostLogoutRedirectUris)
                {
                    if (_options.Validation.InvalidRedirectUriPrefixes
                            .Any(scheme => uri?.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) == true))
                    {
                        context.SetError($"PostLogoutRedirectUri '{uri}' uses invalid scheme. If this scheme should be allowed, then configure it via ValidationOptions.");
                    }
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Validates secret related configuration.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        protected virtual Task ValidateSecretsAsync(ClientConfigurationValidationContext context)
        {
            if (context.Client.AllowedGrantTypes?.Any() == true)
            {
                foreach (var grantType in context.Client.AllowedGrantTypes)
                {
                    if (!string.Equals(grantType, GrantType.Implicit))
                    {
                        if (context.Client.RequireClientSecret && context.Client.ClientSecrets.Count == 0)
                        {
                            context.SetError($"Client secret is required for {grantType}, but no client secret is configured.");
                            return Task.CompletedTask;
                        }
                    }
                }
            }

            // Warn when SharedSecret values are not base64-encoded SHA256/SHA512 hashes.
            // Plain-text secrets are insecure; use HashedSharedSecretValidator and store hashed values.
            foreach (var secret in context.Client.ClientSecrets)
            {
                if (secret.Type == IdentityServerConstants.SecretTypes.SharedSecret && secret.Value != null)
                {
                    try
                    {
                        var bytes = Convert.FromBase64String(secret.Value);
                        if (bytes.Length != 32 && bytes.Length != 64)
                        {
                            _logger.LogWarning("Client {clientId} has a SharedSecret that is not a SHA256 or SHA512 hash. Plain-text secrets are insecure.", context.Client.ClientId);
                        }
                    }
                    catch (FormatException)
                    {
                        _logger.LogWarning("Client {clientId} has a SharedSecret that is not base64-encoded. Plain-text secrets are insecure.", context.Client.ClientId);
                    }
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Validates properties related configuration settings.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        protected virtual Task ValidatePropertiesAsync(ClientConfigurationValidationContext context)
        {
            return Task.CompletedTask;
        }
    }
}