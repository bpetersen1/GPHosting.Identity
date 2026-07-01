// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Threading.Tasks;

namespace GPHosting.Identity.Validation;

public interface IDPoPProofValidator
{
    Task<DPoPProofValidationResult> ValidateAsync(DPoPProofValidationContext context);
}

public class DPoPProofValidationContext
{
    public string ProofToken { get; set; } = default!;
    public string HttpMethod { get; set; } = default!;
    public string HttpUrl { get; set; } = default!;
    public string? AccessToken { get; set; }
}

public class DPoPProofValidationResult
{
    public bool IsError { get; set; }
    public string? Error { get; set; }
    public string? ErrorDescription { get; set; }
    public string? Confirmation { get; set; }
}
