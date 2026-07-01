using BenchmarkDotNet.Attributes;
using GPHosting.Identity.Validation;
using Microsoft.Extensions.Logging.Abstractions;

namespace GPHosting.Identity.Benchmarks;

[MemoryDiagnoser]
public class ScopeParsingBenchmarks
{
    private DefaultScopeParser _parser = null!;
    private string[] _scopeValues = null!;

    [GlobalSetup]
    public void Setup()
    {
        _parser = new DefaultScopeParser(NullLogger<DefaultScopeParser>.Instance);
        _scopeValues = "openid profile email address phone offline_access api1 api2"
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    [Benchmark(Baseline = true)]
    public ParsedScopesResult ParseScopes() => _parser.ParseScopeValues(_scopeValues);
}
