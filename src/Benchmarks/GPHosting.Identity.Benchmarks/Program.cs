using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(GPHosting.Identity.Benchmarks.ScopeParsingBenchmarks).Assembly).Run(args);
