using BenchmarkDotNet.Attributes;
using GPHosting.Identity.Models;
using IdentityModel;
using System.Security.Claims;

namespace GPHosting.Identity.Benchmarks;

[MemoryDiagnoser]
public class TokenClaimLookupBenchmarks
{
    private Token _token = null!;

    [GlobalSetup]
    public void Setup()
    {
        _token = new Token(OidcConstants.TokenTypes.AccessToken)
        {
            Claims = new List<Claim>
            {
                new Claim(JwtClaimTypes.Subject, "user1"),
                new Claim(JwtClaimTypes.SessionId, "session1"),
                new Claim(JwtClaimTypes.Scope, "openid"),
                new Claim(JwtClaimTypes.Scope, "profile"),
                new Claim(JwtClaimTypes.Scope, "email"),
            }
        };
    }

    [Benchmark]
    public string? GetSubjectId() => _token.SubjectId;

    [Benchmark]
    public string? GetSessionId() => _token.SessionId;

    [Benchmark]
    public IEnumerable<string> GetScopes() => _token.Scopes;
}
