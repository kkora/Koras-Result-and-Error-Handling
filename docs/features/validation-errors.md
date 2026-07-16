# Validation Errors (`ValidationError`, `FieldError`)

Feature ID: KR-003 · Package: `Koras.Results` (Core)

## Overview

`ValidationError` is the specialized [`Error`](error-model.md) for input validation: a single
failure that carries *all* invalid fields, so a client can render per-field messages from one
response instead of fixing one field at a time. It is a sealed subclass of `Error` with
`Type == ErrorType.Validation` and an ordered, non-empty `IReadOnlyList<FieldError>`.

Each `FieldError` is a small record: the `PropertyName` that failed (empty string for model-level
failures), a human-readable `Message`, and an optional machine-readable `Code` for the specific
rule.

The structure survives the whole pipeline: it round-trips through JSON serialization (as a
`fieldErrors` array) and the ASP.NET Core package projects it into the standard ProblemDetails
`errors` dictionary grouped by property name.

## When to use it

- Validating request models, commands, or forms where several fields can be wrong at once.
- Aggregating rule checks so the caller receives every violation in a single failure.
- Bridging validation libraries — the `Koras.Results.FluentValidation` package maps
  `ValidationResult` into `ValidationError` using exactly these types.

## When not to use it

- Single, non-field-shaped domain rejections ("order already shipped") — use
  `Error.Failure(...)` or another taxonomy factory with a specific code.
- Post-condition checks inside a composition pipeline — `Ensure` with a plain `Error` is usually
  the better fit (see [functional-composition.md](functional-composition.md)).
- Signaling *technical* problems with input transport (malformed JSON, wrong content type) —
  those belong to the web framework, before your domain code runs.

## Installation

```bash
dotnet add package Koras.Results
```

Validation errors are a core feature; no other package is required. Add
`Koras.Results.FluentValidation` only if you want automatic mapping from FluentValidation.

## Basic usage

```csharp
using Koras.Results;

public sealed record RegisterUser(string Email, string Password);

public static class RegisterUserValidator
{
    public static Result<RegisterUser> Validate(RegisterUser input)
    {
        var fieldErrors = new List<FieldError>();

        if (!input.Email.Contains('@'))
        {
            fieldErrors.Add(new FieldError(nameof(input.Email), "Email must be a valid address.", "Email.Invalid"));
        }

        if (input.Password.Length < 12)
        {
            fieldErrors.Add(new FieldError(nameof(input.Password), "Password must be at least 12 characters.", "Password.TooShort"));
        }

        return fieldErrors.Count > 0
            ? Result.Failure<RegisterUser>(new ValidationError(fieldErrors))
            : Result.Success(input);
    }
}

public static class Program
{
    public static void Main()
    {
        var result = RegisterUserValidator.Validate(new RegisterUser("not-an-email", "short"));

        if (result.IsFailure && result.Error is ValidationError validation)
        {
            Console.WriteLine(validation.Code);    // "Validation.Failed"
            Console.WriteLine(validation.Message); // "One or more validation errors occurred."
            foreach (var field in validation.FieldErrors)
            {
                Console.WriteLine($"{field.PropertyName}: {field.Message} ({field.Code})");
            }
        }
    }
}
```

The params constructor is convenient for inline construction:

```csharp
var error = new ValidationError(
    new FieldError("Email", "Email is required."),
    new FieldError("Password", "Password is required."));
```

## Dependency-injection usage

`ValidationError` and `FieldError` are plain values needing no DI. A typical shape is an injected
validator service returning `Result<T>`:

```csharp
using Koras.Results;

public interface ISignupValidator
{
    Result<RegisterUser> Validate(RegisterUser input);
}

public sealed class SignupValidator : ISignupValidator
{
    public Result<RegisterUser> Validate(RegisterUser input) =>
        RegisterUserValidator.Validate(input);
}

public sealed class SignupService(ISignupValidator validator, IUserRepository users)
{
    public Result<RegisterUser> Register(RegisterUser input) =>
        validator.Validate(input)
            .Ensure(u => !users.EmailExists(u.Email),
                    u => Error.Conflict("User.DuplicateEmail", "That email is already registered."));
}
```

Register `SignupValidator` with any lifetime — it is stateless, so singleton is fine.

## Advanced configuration

None in the core package. The only variability is the constructor overload that supplies a custom
code and message instead of the defaults. Projection details (how field errors become the HTTP
`errors` dictionary) are configured in `Koras.Results.AspNetCore`.

## Public API

- `FieldError` (sealed record) — `FieldError(string PropertyName, string Message, string? Code = null)`;
  one field-level failure. `PropertyName` is the empty string for model-level failures.
- `ValidationError` (sealed class, `: Error`, `Type == ErrorType.Validation`)
  - `ValidationError(params FieldError[] fieldErrors)` — default code `"Validation.Failed"`,
    default message `"One or more validation errors occurred."`.
  - `ValidationError(IEnumerable<FieldError> fieldErrors)` — same defaults, enumerable input.
  - `ValidationError(string code, string message, IEnumerable<FieldError> fieldErrors)` — custom
    code and message.
  - `FieldErrors` — `IReadOnlyList<FieldError>`; non-empty, order preserved.
  - `DefaultCode` (`"Validation.Failed"`) / `DefaultMessage` — public constants.
  - Inherits `WithMetadata`, equality (`Code` + `Type`), and `ToString` from `Error`; cloning via
    `WithMetadata` preserves the field errors.

## Error handling

- An empty field-error collection → `ArgumentException` (a validation error with nothing invalid
  is a contradiction). A null collection → `ArgumentNullException`; null entries →
  `ArgumentException`.
- Custom-code overload: null/whitespace `code` or `message` → `ArgumentException` (inherited
  guards).
- `Result.Combine` merges multiple `ValidationError`s into one, concatenating their field errors
  in order — see [result-combination.md](result-combination.md).
- Downstream code that needs the field list pattern-matches:
  `if (result.Error is ValidationError v) { ... v.FieldErrors ... }`.

## Cancellation

Not applicable in the usual sense: `ValidationError` is a pure data type with no asynchronous
behavior. The package-wide rule still holds around it — if an async validator is cancelled,
`OperationCanceledException` propagates and is never wrapped in a `ValidationError`.

## Security considerations

- Field messages are intended for end users and are projected to clients by the ASP.NET Core
  package. Never echo secrets into them (e.g. do not include a rejected password value in the
  message — name the rule, not the input).
- `PropertyName` reveals your model's shape; that is normal for validation responses, but keep
  internal-only property names out of externally validated models.
- As with all errors, metadata attached via `WithMetadata` is exposed to clients only under the
  ASP.NET Core `MetadataExposure` policy (default: none).

## Performance considerations

- Validation errors allocate (the error, the copied field-error array, and the records) — on the
  failure path only. Success results remain allocation-free readonly structs.
- The constructor copies the supplied collection defensively; build the list once and pass it,
  rather than constructing multiple intermediate `ValidationError`s.

## Thread safety

`ValidationError` is deeply immutable: the field-error list is copied at construction, and
`FieldError` is an immutable record. Instances are freely shareable across threads.

## Testing applications using this feature

```csharp
using Koras.Results;
using Xunit;

public class RegisterUserValidatorTests
{
    [Fact]
    public void Invalid_Input_ReportsAllFields()
    {
        var result = RegisterUserValidator.Validate(new RegisterUser("bad", "short"));

        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);

        Assert.Equal(ValidationError.DefaultCode, validation.Code);
        Assert.Equal(ErrorType.Validation, validation.Type);
        Assert.Collection(validation.FieldErrors,
            f => Assert.Equal("Email", f.PropertyName),
            f => Assert.Equal("Password", f.PropertyName));
    }

    [Fact]
    public void Valid_Input_PassesThrough()
    {
        var input = new RegisterUser("ada@example.com", "a-long-enough-password");

        var result = RegisterUserValidator.Validate(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(input, result.Value);
    }

    [Fact]
    public void EmptyFieldErrors_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ValidationError(Array.Empty<FieldError>()));
    }
}
```

## Complete example

```csharp
using Koras.Results;

public sealed record CreateProduct(string Sku, string Name, decimal Price);

public static class CreateProductValidator
{
    public static Result<CreateProduct> Validate(CreateProduct input)
    {
        var errors = new List<FieldError>();

        if (string.IsNullOrWhiteSpace(input.Sku))
        {
            errors.Add(new FieldError(nameof(input.Sku), "SKU is required.", "Sku.Required"));
        }
        else if (input.Sku.Length > 32)
        {
            errors.Add(new FieldError(nameof(input.Sku), "SKU must be 32 characters or fewer.", "Sku.TooLong"));
        }

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            errors.Add(new FieldError(nameof(input.Name), "Name is required.", "Name.Required"));
        }

        if (input.Price <= 0)
        {
            errors.Add(new FieldError(nameof(input.Price), "Price must be positive.", "Price.Invalid"));
        }

        return errors.Count > 0
            ? new ValidationError("Product.Invalid", "The product is invalid.", errors)
            : Result.Success(input);
    }
}

public static class Program
{
    public static void Main()
    {
        var result = CreateProductValidator.Validate(new CreateProduct("", "", -1m));

        var summary = result.Match(
            onSuccess: p => $"Valid product {p.Sku}",
            onFailure: error => error is ValidationError v
                ? $"{v.Code}: {v.FieldErrors.Count} invalid field(s): " +
                  string.Join(", ", v.FieldErrors.Select(f => f.PropertyName))
                : error.ToString());

        Console.WriteLine(summary);
        // "Product.Invalid: 3 invalid field(s): Sku, Name, Price"
    }
}
```

## Common mistakes

1. **Returning on the first invalid field.** The whole point of `ValidationError` is completeness
   — collect every `FieldError`, then construct one error, so users fix everything in one pass.
   For aggregating results (not fields), use `Result.Combine`.
2. **Constructing a `ValidationError` with no field errors.** This throws `ArgumentException`.
   If nothing is invalid, return a success — do not "pre-build" empty validation errors.
3. **Using `Error.Validation(code, message)` when fields are known.** The plain factory produces
   an `Error` without field structure; the HTTP projection then cannot build the per-field
   `errors` dictionary. Prefer `ValidationError` whenever you know which fields failed.
4. **Checking `error.Type == ErrorType.Validation` instead of pattern-matching.** A plain
   `Error.Validation(...)` also has that type but carries no `FieldErrors`; use
   `error is ValidationError v` when you need the field list.
5. **Encoding user input values in field messages.** Messages travel to clients and logs; name
   the violated rule, not the submitted secret or PII.

## Troubleshooting

- **`ArgumentException: At least one field error is required.`** — the collection you passed was
  empty; only construct a `ValidationError` when at least one field failed.
- **Field errors lost after `WithMetadata`** — they are not: cloning preserves the list. If they
  seem lost, check that you did not upcast and re-wrap the error manually as a plain `Error`.
- **`is ValidationError` fails after deserialization** — verify the JSON payload contains a
  `fieldErrors` array and `"type":"validation"`; the deserializer discriminates structurally (see
  [serialization.md](serialization.md)).
- **Fields appear in the wrong order** — order is preserved exactly as supplied; sort your list
  before constructing the error if presentation order matters.

## Related features

- [error-model.md](error-model.md) — the base `Error` contract that `ValidationError` extends.
- [result-combination.md](result-combination.md) — merging multiple validation errors into one.
- [result-types.md](result-types.md) — the results that carry validation failures.
- [serialization.md](serialization.md) — the `fieldErrors` wire shape.
- [functional-composition.md](functional-composition.md) — `Ensure` for single post-conditions.
