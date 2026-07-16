# JSON Serialization

Feature ID: KR-008 · Package: `Koras.Results` (Core)

## Overview

Results and errors that cross internal process boundaries — queues, caches, inter-service
messages — need a stable wire shape. The core package ships System.Text.Json converters for
`Result`, `Result<T>`, `Error`, `ValidationError`, and `AggregateError`, wired automatically via
`[JsonConverter]` attributes on the types. In the normal case there is **no setup**:
`JsonSerializer.Serialize(result)` and `Deserialize` just work.

The wire shape is a public, versioned contract (ADR-0007) with fixed camelCase property names
(independent of your `JsonSerializerOptions` naming policy):

```json
{ "isSuccess": true, "value": 42 }
{ "isSuccess": true }
{ "isSuccess": false,
  "error": {
    "code": "User.NotFound",
    "message": "No user with id 7.",
    "type": "notFound",
    "metadata": { "userId": 7 }
  } }
```

Error subtypes are discriminated **structurally**, never by polymorphic type names: the presence
of a `fieldErrors` array marks a `ValidationError`, an `errors` array marks an `AggregateError`.
`ErrorType` serializes as a fixed string: `failure`, `validation`, `notFound`, `conflict`,
`unauthorized`, `forbidden`, `unavailable`, `unexpected`.

An important caveat up front: this format is for *internal* boundaries. At public API boundaries,
prefer projecting errors to RFC 9457 ProblemDetails via the `Koras.Results.AspNetCore` package
(ADR-0007) — clients should see a standard problem document, not your internal result envelope.

## When to use it

- Persisting or transporting results across internal process boundaries: message queues,
  distributed caches, durable job state, actor messages.
- Round-tripping errors between your own services that both speak the Koras.Results contract.
- Snapshot-style testing where a stable, readable JSON form of an outcome is convenient.

## When not to use it

- **Public API responses.** Use ProblemDetails from `Koras.Results.AspNetCore`; the serialized
  result envelope is an internal contract, not a client-facing one.
- Interop with services that do not use Koras.Results — they gain nothing from the envelope;
  design an explicit DTO.
- Storing large success payloads — the converter serializes `value` with your options; the
  envelope adds nothing to a plain DTO if you never transport failures.

## Installation

```bash
dotnet add package Koras.Results
```

Serialization is a core feature; converters ship in the same assembly
(`Koras.Results.Serialization` namespace) and require no extra package.

## Basic usage

```csharp
using System.Text.Json;
using Koras.Results;

public static class Program
{
    public static void Main()
    {
        // Success round-trip
        Result<int> success = Result.Success(42);
        var successJson = JsonSerializer.Serialize(success);
        Console.WriteLine(successJson);
        // {"isSuccess":true,"value":42}

        // Failure round-trip, including metadata
        Result<int> failure = Result.Failure<int>(
            Error.NotFound("User.NotFound", "No user with id 7.").WithMetadata("userId", 7L));
        var failureJson = JsonSerializer.Serialize(failure);
        Console.WriteLine(failureJson);
        // {"isSuccess":false,"error":{"code":"User.NotFound","message":"No user with id 7.",
        //   "type":"notFound","metadata":{"userId":7}}}

        var roundTripped = JsonSerializer.Deserialize<Result<int>>(failureJson);
        Console.WriteLine(roundTripped.Error.Code);              // "User.NotFound"
        Console.WriteLine(roundTripped.Error.Metadata["userId"]); // 7 (a long, not a double)

        // ValidationError keeps its shape via the fieldErrors array
        Error validation = new ValidationError(
            new FieldError("Email", "Email is required.", "Email.Required"));
        Console.WriteLine(JsonSerializer.Serialize(validation));
        // {"code":"Validation.Failed","message":"One or more validation errors occurred.",
        //  "type":"validation","fieldErrors":[{"propertyName":"Email",
        //  "message":"Email is required.","code":"Email.Required"}]}

        var back = JsonSerializer.Deserialize<Error>(JsonSerializer.Serialize(validation));
        Console.WriteLine(back is ValidationError); // True — structural discrimination
    }
}
```

## Dependency-injection usage

Nothing to register: the converters are attribute-wired onto the types themselves, so any
`JsonSerializer` call — including the ones ASP.NET Core and typed HTTP clients make internally —
picks them up. Services simply return results; serialization happens wherever your app already
serializes:

```csharp
using System.Text.Json;
using Koras.Results;

public interface IJobStore
{
    Task SaveOutcomeAsync(Guid jobId, Result<string> outcome);
}

public sealed class RedisJobStore(IRedisConnection redis) : IJobStore
{
    public Task SaveOutcomeAsync(Guid jobId, Result<string> outcome) =>
        redis.SetAsync($"job:{jobId}:outcome", JsonSerializer.Serialize(outcome));
}
```

If you build custom `JsonSerializerOptions` pipelines that clear or replace converters, the
converter types are public and can be registered explicitly:

```csharp
using System.Text.Json;
using Koras.Results.Serialization;

var options = new JsonSerializerOptions();
options.Converters.Add(new ErrorJsonConverter());
options.Converters.Add(new ResultJsonConverter());
options.Converters.Add(new ResultJsonConverterFactory());
```

## Advanced configuration

The wire shape is deliberately not configurable: property names are fixed camelCase regardless of
`PropertyNamingPolicy`, and `type` strings are a fixed vocabulary. This is what makes the shape a
contract — two services with different serializer settings still interoperate. What *is*
influenced by your options: the serialization of the success `value` (your DTO) and of complex
values inside metadata, which are serialized with the ambient `JsonSerializerOptions`.

## Public API

Namespace `Koras.Results.Serialization`:

- `ErrorJsonConverter` (`JsonConverter<Error>`) — reads/writes `Error` and, via structural
  discrimination, its subclasses.
- `ResultJsonConverter` (`JsonConverter<Result>`) — reads/writes the non-generic `Result`
  (`{"isSuccess":true}` / `{"isSuccess":false,"error":{...}}`).
- `ResultJsonConverterFactory` (`JsonConverterFactory`) — creates converters for any closed
  `Result<T>` (`{"isSuccess":true,"value":...}` / `{"isSuccess":false,"error":{...}}`).

All are public so they can be registered explicitly in custom `JsonSerializerOptions`;
attribute wiring means you normally never need to.

Wire contract summary:

- Result: `isSuccess` (bool, required); `value` (success `Result<T>` only, non-null);
  `error` (failures only).
- Error: `code`, `message`, `type` (required, non-empty); `metadata` (present only when
  non-empty); `fieldErrors` (ValidationError only); `errors` (AggregateError only).
- `type` strings: `failure`, `validation`, `notFound`, `conflict`, `unauthorized`, `forbidden`,
  `unavailable`, `unexpected`.

## Error handling

Malformed payloads throw `JsonException` — deserialization never silently produces a bogus
result. Rejected shapes include:

- a result object without `isSuccess`;
- a failure (`"isSuccess":false`) without an `error` object;
- a success `Result<T>` with a missing or null `value` (the null-success guarantee holds on the
  wire too);
- an error missing non-empty `code`, `message`, or `type`;
- an unknown `type` string;
- an error carrying both `fieldErrors` and `errors`;
- `fieldErrors` present with `"type"` other than `"validation"`, or an empty `fieldErrors` array;
- structurally invalid contents (e.g. an aggregate with fewer than two children) — the underlying
  `ArgumentException` is wrapped in a `JsonException`.

Unknown extra properties are skipped, allowing forward-compatible additions.

## Cancellation

Not applicable as a feature concern: the converters are synchronous, CPU-only code invoked by
`JsonSerializer` and never observe a `CancellationToken` themselves. When you serialize inside
async I/O (e.g. `JsonSerializer.SerializeAsync(stream, result, ct)`), cancellation belongs to the
stream operation, and — as everywhere in this package — surfaces as `OperationCanceledException`,
never as a failed result.

## Security considerations

- **Deserialization constructs only the package's sealed types.** Discrimination is structural;
  no type names are read from the payload, so there is no polymorphic-deserialization gadget
  surface.
- **Nested metadata stays inert.** Metadata primitives deserialize to their natural CLR types;
  nested objects/arrays become `JsonElement` values — never rehydrated into arbitrary types.
- **Do not expose the envelope publicly.** Errors serialize their full `message` and `metadata`;
  the ASP.NET Core ProblemDetails projection is the layer that suppresses `Unexpected` details
  and gates metadata exposure. Serialized results bypass those protections by design (they are an
  internal fidelity format), so keep them internal — and keep secrets out of errors entirely.
- Malformed input fails fast with `JsonException`; treat payloads from less-trusted internal
  sources with the same size limits you apply to any JSON input.

## Performance considerations

- Converters are used only when a `Result`/`Error` is actually (de)serialized; the in-memory
  success path of `Result`/`Result<T>` remains allocation-free readonly structs, and failures
  allocate only the `Error`.
- Writing streams directly through `Utf8JsonWriter`; reading buffers only the `value` payload of
  `Result<T>` (as a `JsonElement`) so property order in the payload does not matter.
- Metadata numbers are read as `long` when integral (`double` otherwise) — no boxing surprises
  beyond the usual `object?` dictionary values.

## Thread safety

The converter instances are stateless and thread-safe, as System.Text.Json requires; a single
instance serves concurrent serializations. The values being serialized are immutable, so there is
no torn-read hazard.

## Testing applications using this feature

Round-trip tests are the canonical form:

```csharp
using System.Text.Json;
using Koras.Results;
using Xunit;

public class SerializationTests
{
    [Fact]
    public void SuccessResult_RoundTrips()
    {
        var original = Result.Success(new[] { 1, 2, 3 });

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<Result<int[]>>(json);

        Assert.True(restored.IsSuccess);
        Assert.Equal(new[] { 1, 2, 3 }, restored.Value);
    }

    [Fact]
    public void FailureResult_PreservesErrorIdentityAndMetadataTypes()
    {
        var original = Result.Failure<string>(
            Error.Conflict("Order.Duplicate", "Duplicate order.").WithMetadata("attempt", 3L));

        var restored = JsonSerializer.Deserialize<Result<string>>(JsonSerializer.Serialize(original));

        Assert.True(restored.IsFailure);
        Assert.Equal(original.Error, restored.Error);          // equality: Code + Type
        Assert.Equal(3L, restored.Error.Metadata["attempt"]);  // longs stay longs
    }

    [Fact]
    public void ValidationError_RoundTripsAsValidationError()
    {
        Error original = new ValidationError(new FieldError("Email", "Required.", "Email.Required"));

        var restored = JsonSerializer.Deserialize<Error>(JsonSerializer.Serialize(original));

        var validation = Assert.IsType<ValidationError>(restored);
        Assert.Equal("Email", validation.FieldErrors[0].PropertyName);
    }

    [Fact]
    public void MalformedPayload_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Result<int>>("""{"isSuccess":false}"""));

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Result<int>>("""{"isSuccess":true,"value":null}"""));
    }
}
```

## Complete example

```csharp
using System.Text.Json;
using Koras.Results;

public sealed record JobPayload(string Name);

public static class Program
{
    // Simulates an outcome traveling through a queue as JSON.
    public static void Main()
    {
        var outcome = RunJob(new JobPayload(""));

        // Producer side: serialize the outcome
        var wire = JsonSerializer.Serialize(outcome);
        Console.WriteLine($"on the wire: {wire}");

        // Consumer side: deserialize and react
        var received = JsonSerializer.Deserialize<Result<string>>(wire);

        received.Switch(
            onSuccess: value => Console.WriteLine($"job produced: {value}"),
            onFailure: error =>
            {
                Console.WriteLine($"job failed [{error.Type}] {error.Code}");
                if (error is ValidationError validation)
                {
                    foreach (var field in validation.FieldErrors)
                    {
                        Console.WriteLine($"  {field.PropertyName}: {field.Message}");
                    }
                }
            });
    }

    private static Result<string> RunJob(JobPayload payload) =>
        string.IsNullOrWhiteSpace(payload.Name)
            ? Result.Failure<string>(new ValidationError(
                new FieldError(nameof(payload.Name), "A job name is required.", "Job.NameRequired")))
            : Result.Success($"processed:{payload.Name}");
}
```

Output:

```text
on the wire: {"isSuccess":false,"error":{"code":"Validation.Failed","message":"One or more validation errors occurred.","type":"validation","fieldErrors":[{"propertyName":"Name","message":"A job name is required.","code":"Job.NameRequired"}]}}
job failed [Validation] Validation.Failed
  Name: A job name is required.
```

## Common mistakes

1. **Returning serialized results from public APIs.** Clients should receive ProblemDetails
   (RFC 9457) produced by `Koras.Results.AspNetCore`, which applies status mapping and detail
   suppression. The result envelope is an internal contract (ADR-0007).
2. **Expecting the naming policy to apply.** `isSuccess`, `error`, `code` etc. are fixed camelCase
   regardless of `JsonSerializerOptions.PropertyNamingPolicy`; only your `value` payload follows
   your options.
3. **Assuming nested metadata objects rehydrate as your types.** They come back as `JsonElement`.
   If you need typed metadata, store primitives, or deserialize the `JsonElement` explicitly at
   the consumption site.
4. **Treating `JsonException` on deserialize as a bug.** It is the designed rejection of malformed
   payloads (missing `isSuccess`, null `value` on success, unknown `type` …). Catch it at the
   queue/cache boundary and decide policy there.
5. **Hand-writing the envelope.** Producing `{"isSuccess":false}` without an `error`, or a success
   with `"value":null`, will be rejected on read. Always produce payloads by serializing real
   `Result` values.

## Troubleshooting

- **`JsonException: A result object requires an 'isSuccess' property.`** — the payload is not a
  serialized result; check what was actually written to the transport.
- **`JsonException: A success result requires a non-null 'value' property.`** — the producer
  serialized something that violates the null-success rule, or the payload was hand-built.
- **`JsonException: Unknown error type '...'`** — the `type` string is outside the fixed
  vocabulary; likely a foreign producer or a manually edited payload.
- **Deserialized error lost its `ValidationError`/`AggregateError` identity** — the payload lacks
  the `fieldErrors`/`errors` array that drives structural discrimination; inspect the JSON.
- **Integral metadata came back as `double`** — it will not: integral JSON numbers are read as
  `long` by contract. If you see a `double`, the producer wrote a non-integral number.

## Related features

- [result-types.md](result-types.md) — the `Result`/`Result<T>` envelope semantics.
- [error-model.md](error-model.md) — `code`/`message`/`type`/`metadata` and their rules.
- [validation-errors.md](validation-errors.md) — the `fieldErrors` wire shape.
- [result-combination.md](result-combination.md) — the `errors` wire shape of `AggregateError`.
- [exception-conversion.md](exception-conversion.md) — where `exceptionType` metadata originates.
