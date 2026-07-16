using BenchmarkDotNet.Attributes;
using Koras.Results;

namespace Koras.Results.Benchmarks;

/// <summary>
/// Core-operation benchmarks. Methodology and current baseline numbers are documented in
/// docs/performance/benchmarks.md — update both together.
/// </summary>
[MemoryDiagnoser]
public class ResultBenchmarks
{
    private static readonly Error NotFound = Error.NotFound("User.NotFound", "The user does not exist.");

    [Benchmark(Baseline = true)]
    public int RawValue_baseline() => 42;

    [Benchmark]
    public Result<int> Create_success() => Result.Success(42);

    [Benchmark]
    public Result<int> Create_failure() => Result.Failure<int>(NotFound);

    [Benchmark]
    public int Map_bind_chain_success() =>
        Result.Success(21)
            .Map(v => v * 2)
            .Bind(v => Result.Success(v + 1))
            .Ensure(v => v > 0, NotFound)
            .Match(v => v, _ => -1);

    [Benchmark]
    public int Map_bind_chain_failure_short_circuit() =>
        Result.Failure<int>(NotFound)
            .Map(v => v * 2)
            .Bind(v => Result.Success(v + 1))
            .Ensure(v => v > 0, NotFound)
            .Match(v => v, _ => -1);

    [Benchmark]
    public Result Combine_four_successes() =>
        Result.Combine(Result.Success(), Result.Success(), Result.Success(), Result.Success());

    [Benchmark]
    public async Task<int> Async_pipeline_success() =>
        await Task.FromResult(Result.Success(21))
            .MapAsync(v => v * 2)
            .MatchAsync(v => v, _ => -1);
}

/// <summary>Compares result-based failure signalling against exception throwing.</summary>
[MemoryDiagnoser]
public class FailurePathBenchmarks
{
    private static readonly Error NotFound = Error.NotFound("User.NotFound", "The user does not exist.");

    [Benchmark(Baseline = true)]
    public int Result_failure_path()
    {
        var result = Find(found: false);
        return result.Match(v => v, _ => -1);
    }

    [Benchmark]
    public int Exception_failure_path()
    {
        try
        {
            return FindThrowing(found: false);
        }
        catch (InvalidOperationException)
        {
            return -1;
        }
    }

    private static Result<int> Find(bool found) => found ? Result.Success(1) : Result.Failure<int>(NotFound);

    private static int FindThrowing(bool found) =>
        found ? 1 : throw new InvalidOperationException("The user does not exist.");
}

/// <summary>Serialization throughput for the stable wire shape.</summary>
[MemoryDiagnoser]
public class SerializationBenchmarks
{
    private static readonly Result<OrderDto> Success = Result.Success(new OrderDto("A-1", 3));
    private static readonly Result<OrderDto> ValidationFailure = Result.Failure<OrderDto>(
        new ValidationError(new FieldError("Sku", "Required."), new FieldError("Quantity", "Too low.")));

    private static readonly string SuccessJson = System.Text.Json.JsonSerializer.Serialize(Success);
    private static readonly string FailureJson = System.Text.Json.JsonSerializer.Serialize(ValidationFailure);

    [Benchmark]
    public string Serialize_success() => System.Text.Json.JsonSerializer.Serialize(Success);

    [Benchmark]
    public string Serialize_validation_failure() => System.Text.Json.JsonSerializer.Serialize(ValidationFailure);

    [Benchmark]
    public Result<OrderDto> Deserialize_success() =>
        System.Text.Json.JsonSerializer.Deserialize<Result<OrderDto>>(SuccessJson);

    [Benchmark]
    public Result<OrderDto> Deserialize_validation_failure() =>
        System.Text.Json.JsonSerializer.Deserialize<Result<OrderDto>>(FailureJson);

    public sealed record OrderDto(string Sku, int Quantity);
}
