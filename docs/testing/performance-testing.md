# Performance Testing — Koras.Results

Performance claims in this repository (allocation-free success path, cheap short-circuiting,
orders-of-magnitude advantage over exceptions on failure paths) are **verified, not asserted**.
The benchmark project is `benchmarks/Koras.Results.Benchmarks`; current numbers live in
[`docs/performance/benchmarks.md`](../performance/benchmarks.md) — update both together (the
benchmark source carries a comment saying exactly that).

## Methodology

The suite uses **BenchmarkDotNet 0.14.0** (version centralized in `Directory.Packages.props`)
with three benchmark classes in `ResultBenchmarks.cs`:

- **`ResultBenchmarks`** — core operations: `Create_success`, `Create_failure`, a four-step
  `Map`→`Bind`→`Ensure`→`Match` chain on both the success path and the failure
  (short-circuit) path, `Combine_four_successes`, and an async `MapAsync`/`MatchAsync` pipeline.
  A `RawValue_baseline` method (returning a bare `int`) is marked `[Benchmark(Baseline = true)]`
  so result-wrapping overhead is measured *relative to doing nothing*.
- **`FailurePathBenchmarks`** — the headline comparison: signalling a failure as a `Result` value
  (`Result_failure_path`, the baseline) versus throwing and catching an exception
  (`Exception_failure_path`). Both paths perform the same logical lookup-miss.
- **`SerializationBenchmarks`** — System.Text.Json serialize/deserialize throughput for a
  success `Result<OrderDto>` and a two-field `ValidationError` failure, against the stable wire
  shape.

Conventions:

- **`[MemoryDiagnoser]` on every class.** Allocation numbers are first-class results here — the
  zero-allocation claims for success paths and short-circuits are the library's core performance
  promise, and a regression to nonzero `Allocated` fails review even if timings look fine.
- **Baseline comparisons** (`Baseline = true`) so reports include `Ratio` columns; relative
  numbers survive hardware differences much better than absolute nanoseconds.
- **Static readonly fixtures** (pre-built errors, pre-serialized JSON strings) so the measured
  region contains only the operation under test.
- **Short vs full jobs.** Routine verification and CI-adjacent runs use the ShortRun job
  (3 warmup + 3 measurement iterations, 1 launch) — fast, and precise enough to catch
  order-of-magnitude regressions and any allocation change. Numbers published in
  `docs/performance/benchmarks.md` are labelled with the job used; ShortRun figures are
  *indicative*, and a full default-job run is expected before publishing numbers in release
  announcements or README claims.

`Program.cs` uses `BenchmarkSwitcher.FromAssembly(...)`, so class/method selection and job
configuration are all driven from the command line.

## How to run

From the repository root:

```bash
# Everything, default job:
dotnet run -c Release --project benchmarks/Koras.Results.Benchmarks -- --filter '*'

# Everything, quick indicative pass:
dotnet run -c Release --project benchmarks/Koras.Results.Benchmarks -- --filter '*' --job short

# One class:
dotnet run -c Release --project benchmarks/Koras.Results.Benchmarks -- --filter '*FailurePathBenchmarks*'

# List available benchmarks:
dotnet run -c Release --project benchmarks/Koras.Results.Benchmarks -- --list flat
```

Always `-c Release`; BenchmarkDotNet refuses Debug builds for good reason. Reports (GitHub
markdown among them) land in `benchmarks/Koras.Results.Benchmarks/BenchmarkDotNet.Artifacts/results/`.

Interpreting output: `Mean` per operation, `Allocated` per operation, `Ratio` versus the class
baseline. On very fast operations (sub-nanosecond `Create_*`), BenchmarkDotNet may print `?` for
ratios and warn that the operation is below timer resolution — the meaningful signal there is
"indistinguishable from free, 0 B allocated", not the exact picosecond value.

## Regression policy

1. **Rerun the relevant benchmark class whenever a PR touches core primitives** — anything in
   `src/Koras.Results/` (`Result`, `Result<T>`, `Error` family, `ResultExtensions`,
   `ResultAsyncExtensions`, `Result.Try`, `Result.Combine`, serialization converters). A ShortRun
   pass is the minimum; use the default job if the ShortRun result is ambiguous.
2. **Record the before/after table in the PR description.** Reviewers compare against the
   current baseline in `docs/performance/benchmarks.md`. There is no automated benchmark CI gate
   (shared runners are too noisy for reliable nanosecond assertions); the PR record is the gate.
3. **Hard rules that fail review regardless of timings:**
   - `Create_success`, `Create_failure`, `Map_bind_chain_success`, and
     `Map_bind_chain_failure_short_circuit` must report **0 B allocated**;
   - the `Result_failure_path` vs `Exception_failure_path` relationship must remain
     orders of magnitude apart;
   - no benchmark may regress by more than ~10 % mean time without an explicit justification in
     the PR (and an update to the published numbers).
4. **Update `docs/performance/benchmarks.md`** in the same PR when numbers move materially or
   when a new benchmark is added — the doc and the source are a pair.

## What is deliberately not benchmarked

- The AspNetCore adapters (dominated by ASP.NET Core's own pipeline; a micro-benchmark would
  measure the framework, not this library).
- The MediatR behavior (dominated by MediatR dispatch and FluentValidation execution).

For these, the meaningful performance property is "no avoidable allocations added on top of the
framework", which is reviewed at the code level.
