# Benchmarks — Koras.Results

Verified results from the benchmark suite in `benchmarks/Koras.Results.Benchmarks`
(`ResultBenchmarks.cs`). Raw BenchmarkDotNet reports are checked in under
`benchmarks/Koras.Results.Benchmarks/BenchmarkDotNet.Artifacts/results/`.

## Environment

```
BenchmarkDotNet v0.14.0, ShortRun job (LaunchCount=1, WarmupCount=3, IterationCount=3)
.NET 10.0.10 (SDK 10.0.302), X64 RyuJIT AVX-512
Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Xeon Processor 2.80GHz, 1 CPU, 4 logical / 4 physical cores
```

> **Disclaimer — ShortRun numbers are indicative.** The ShortRun job trades statistical rigor for
> speed (3 iterations, 1 launch); error margins on the fastest benchmarks exceed the means, and
> absolute nanoseconds will differ on your hardware. The stable signals are the **allocation
> column** (exact, not sampled) and the **orders of magnitude**. Rerun with the default job
> before quoting numbers anywhere formal (see
> [../testing/performance-testing.md](../testing/performance-testing.md)).

## Core operations (`ResultBenchmarks`)

| Benchmark | Mean | Allocated |
|---|---:|---:|
| `RawValue_baseline` (return a bare `int`) | ~0.05 ns | 0 B |
| `Create_success` | sub-nanosecond (~0.01 ns) | **0 B** |
| `Create_failure` (reusing a static error) | sub-nanosecond (~0.003 ns) | **0 B** |
| `Map_bind_chain_success` (Map → Bind → Ensure → Match) | ~7.9 ns | **0 B** |
| `Map_bind_chain_failure_short_circuit` (same chain, failed input) | ~8.0 ns | **0 B** |
| `Combine_four_successes` | ~32.5 ns | 88 B (the params array) |
| `Async_pipeline_success` (Task.FromResult → MapAsync → MatchAsync) | ~113 ns | 304 B (Task machinery) |

Reading it:

- Creating results is indistinguishable from returning raw values — both are below timer
  resolution, which is why BenchmarkDotNet flags them; the load-bearing fact is **0 B allocated**.
- A four-combinator chain costs a handful of nanoseconds and allocates nothing, on **both**
  outcomes — the failure path is as cheap as the success path because short-circuiting passes the
  same `Error` reference through without invoking delegates.
- `Combine`'s 88 B is the `params Result[]` array at the call site; use the tuple overloads on
  hot paths (see [performance-guide.md](performance-guide.md)).
- The async pipeline's 304 B is `Task<T>`/state-machine plumbing, not result plumbing — the cost
  of `async` itself, paid identically by any Task-based code.

## Failure signalling: results vs exceptions (`FailurePathBenchmarks`)

Both benchmarks perform the same logical lookup-miss; one signals it as a value, the other throws
and catches.

| Benchmark | Mean | Allocated | Ratio |
|---|---:|---:|---:|
| `Result_failure_path` (baseline) | ~2.4 ns | **0 B** | 1.00 |
| `Exception_failure_path` | ~2,506 ns | 320 B | **~1,000×** |

**Caveat, stated honestly:** this is not a claim that exceptions are badly implemented. The
~1,000× gap exists because a thrown exception captures a stack trace and unwinds the stack —
work that is *valuable when a genuine bug needs diagnosing* and pure waste when the "failure" is
an expected domain outcome (user not found, validation rejected, stock insufficient). That is
precisely the argument for the Result pattern: **expected failures are control flow, and control
flow should not pay for stack traces.** Exceptions remain the right tool for unexpected,
non-local faults; `Result.Try` converts at the boundary. The comparison also flatters neither
side artificially: the exception path here has a shallow stack — deep call stacks make throwing
proportionally more expensive in real applications.

## Serialization (`SerializationBenchmarks`)

System.Text.Json round-trips of a `Result<OrderDto>` (two-property record) and a validation
failure with two field errors:

| Benchmark | Mean | Allocated |
|---|---:|---:|
| `Serialize_success` | ~489 ns | 128 B |
| `Serialize_validation_failure` | ~894 ns | 536 B |
| `Deserialize_success` | ~1,172 ns | 464 B |
| `Deserialize_validation_failure` | ~1,640 ns | 1,024 B |

These are ordinary STJ magnitudes — the converters add structural discrimination but no
reflection-heavy work at runtime beyond STJ's own. Deserialization allocates the resulting object
graph (error, field-error list, strings), which scales with payload content; buffering behavior
is described in [memory-management.md](memory-management.md).

## Reproducing

```bash
# Full suite, default (rigorous) job:
dotnet run -c Release --project benchmarks/Koras.Results.Benchmarks -- --filter '*'

# Match the published ShortRun numbers:
dotnet run -c Release --project benchmarks/Koras.Results.Benchmarks -- --filter '*' --job short

# Single class:
dotnet run -c Release --project benchmarks/Koras.Results.Benchmarks -- --filter '*SerializationBenchmarks*'
```

Reports are written to `BenchmarkDotNet.Artifacts/results/` next to the project. When results
change materially (new hardware baseline, code changes), update this document and the PR record
together — the benchmark source and this page are maintained as a pair, per the comment at the
top of `ResultBenchmarks.cs` and the regression policy in
[../testing/performance-testing.md](../testing/performance-testing.md).
