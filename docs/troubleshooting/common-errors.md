# Common Errors — Koras.Results

Real failure modes, their exact symptoms, and fixes. See also
[provider-errors.md](provider-errors.md) for integration-package issues and [faq.md](faq.md) for
the "why is it like this" questions.

## InvalidOperationException: "Cannot access Value on a failure result…"

```
System.InvalidOperationException: Cannot access Value on a failure result (error 'User.NotFound').
Check IsSuccess or use TryGetValue before accessing Value.
```

**Cause**: reading `result.Value` on a failure (the mirror image — reading `result.Error` on a
success — throws "Cannot access Error on a success result…"). This is deliberate: silently
returning `default` would turn a handled failure into a latent `NullReferenceException` far from
the cause. The message includes the error **code** so the log line already tells you *which*
failure you mishandled.

**Fix** — branch before access, or use the total members:

```csharp
if (result.IsSuccess) Use(result.Value);            // guard first
if (result.TryGetValue(out var value)) Use(value);  // try-pattern
var v = result.GetValueOrDefault(fallback);          // explicit fallback
var text = result.Match(v => Render(v), e => RenderError(e)); // exhaustive fold
```

## default(Result) surprises: error code `Result.Uninitialized`

A failure with code `"Result.Uninitialized"` and type `Unexpected` means a `Result`/`Result<T>`
was **never assigned**: `default(Result)`, `new Result<T>[n]` slots, an unset struct field, or a
`Result` obtained from `FirstOrDefault()`. By design (ADR-0003) an uninitialized struct is a
*failure*, never a fake success — so the bug surfaces as a classified error instead of a null
value.

**Fix**: find the unassigned path. Typical culprits: a `switch` without a default arm assigning
the variable, dictionaries of results with missing keys, or fields initialized later than first
use. Initialize explicitly (`Result.Success()` / a real failure) on every path.

## ArgumentNullException from Success(null)

```
System.ArgumentNullException: A success result cannot carry null. Model optional values in the
domain type instead. (Parameter 'value')
```

**Cause**: `Result.Success<T>(null)` or the implicit conversion from a null `T`. `Result<T>`
never carries null — `IsSuccess == true` *guarantees* a non-null `Value` (enforced at runtime,
annotated for NRT). "Found nothing" is either a `NotFound` failure or an explicit optional type
in your domain model — not a null success.

**Fix**: return `Result.Failure<T>(Error.NotFound(...))`, or make absence part of the value type
(e.g. `Result<SearchOutcome>` where the outcome models emptiness).

## JsonException shapes from deserialization

The converters reject malformed payloads loudly (all pinned in `SerializationTests`):

| Message contains | Payload problem |
|---|---|
| `requires an 'isSuccess' property` | Missing discriminator (e.g. `{"value":1}`) |
| `A failure result requires an 'error' property` | `{"isSuccess":false}` with no error |
| `A success result requires a non-null 'value' property` | Generic success without `value`, or `"value":null` |
| `Expected a result object, found …` | Wrong JSON token (array/string where an object is required) |
| Unknown `type` value / missing `code`/`message` | Error object not matching the wire contract |
| Ambiguous / empty `fieldErrors` / single-child `errors` | Structurally invalid `ValidationError`/`AggregateError` |

**Fix**: the payload does not match the documented wire shape (ADR-0007). Check the producer —
common causes are hand-built JSON, a naming policy applied to the envelope (the envelope property
names `isSuccess`/`value`/`error` are fixed; only the *value* respects your serializer options),
or a different serializer on the other side. Unknown *extra* properties are fine (ignored for
forward compatibility); missing or malformed *required* ones are not.

## NU1107 / version conflict on MediatR 13

```
error NU1107: Version conflict detected for MediatR. Install/reference MediatR 13.x directly ...
Koras.Results.MediatR -> MediatR (>= 12.4.1 && < 13.0.0)
```

**This conflict is deliberate** (ADR-0006). MediatR 13+ moved to a commercial license;
`Koras.Results.MediatR` is bounded to the Apache-2.0 licensed 12.x line —
`MediatR [12.4.1, 13.0.0)` — precisely so an MIT package can never silently pull a commercially
licensed dependency into your build. The loud restore failure *is* the feature.

**Options**:
1. Stay on MediatR 12.x (recommended if you want this integration).
2. If you must use MediatR 13+, drop `Koras.Results.MediatR` and port the behavior yourself — it
   is one small class (`ValidationBehavior<,>`, ~60 lines); with the core and FluentValidation
   packages you can replicate it in your own code under your own MediatR license.

Do not "solve" this with a direct MediatR 13 reference to win the conflict — you would be running
an untested combination against the package's declared range.

## CS0104 / namespace collision: `Results` is ambiguous

In files with `using Koras.Results;` and Minimal API code, `Results.Ok(...)` can become ambiguous
or wrong — `Koras.Results` (the namespace) and `Microsoft.AspNetCore.Http.Results` (the static
class) collide, e.g.:

```
error CS0234: The type or namespace name 'Ok' does not exist in the namespace 'Koras.Results'
```

**Fix** — alias the ASP.NET Core type:

```csharp
using Koras.Results;
using HttpResults = Microsoft.AspNetCore.Http.Results;

app.MapGet("/x", () => HttpResults.Ok());
```

In practice you rarely need `Results.*` at all in Koras endpoints — `result.ToHttpResult()` /
`ToCreatedHttpResult(...)` cover the standard shapes; the alias is for custom `IResult` factories
(as used in the library's own integration tests).

## ArgumentException from Error construction

`Error` requires non-empty `code` and `message` (`ArgumentException` with the parameter name);
`ValidationError` requires at least one `FieldError`; `AggregateError` requires at least two
errors and no nulls. These guards fire at construction — at the site of the mistake — rather than
during projection or serialization later. Fix the construction site; do not catch these.
