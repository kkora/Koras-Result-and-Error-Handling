# API Naming Guidelines — Koras.Results

## General

- Follow [.NET Framework Design Guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/) without exception; this file records package-specific refinements.
- Namespaces mirror package IDs exactly: `Koras.Results`, `Koras.Results.AspNetCore`, etc. No extra nesting — each package is one namespace so a single `using` lights up the whole surface.
- Extension-method classes: `<Subject><Aspect>Extensions` (`ResultExtensions`, `ResultAsyncExtensions`, `HttpResultExtensions`, `ActionResultExtensions`, `ValidationResultExtensions`).

## Method-name vocabulary (fixed; do not invent synonyms)

| Name | Meaning | Never called |
|---|---|---|
| `Map` | transform the success value | `Select`* |
| `Bind` | chain a Result-returning function | `Then`, `FlatMap`, `AndThen` |
| `Match` | fold both branches into a value | `Fold`, `Either` |
| `Switch` | act on both branches (void) | `Do` |
| `Ensure` | assert a post-condition on success | `Filter`, `Where`* |
| `Tap` / `TapError` | side effect, pass through | `OnSuccess`/`OnFailure` (reads like events) |
| `MapError` | translate the error | `WrapError` |
| `Try` | exception boundary → Result | `Catch`, `Attempt` |
| `Combine` | aggregate independent results | `Merge`, `Zip` |

\* `Select`/`SelectMany`/`Where` are reserved for the future LINQ query-syntax feature (KR-102) as *aliases*, not replacements.

## Async

- Suffix `Async` on any method returning `Task`/`Task<T>` — including combinators whose receiver is a `Task<Result<T>>` (e.g. `MapAsync`), so call chains read uniformly.
- No `ValueTask` in public signatures (ADR-0003 note).
- CancellationToken parameters are always last, always optional (`= default`) where the API owns the async operation (e.g. `ValidateToResultAsync`).

## Errors

- Error codes: `PascalCase.Dot.Separated`, `{Subject}.{Condition}` (`User.NotFound`, `Order.InsufficientStock`, `Validation.Failed`, `Unexpected.Exception`, `Result.Uninitialized`, `Errors.Multiple`). Subjects singular. No spaces, no hyphens.
- Factory names equal `ErrorType` member names exactly (`Error.NotFound(...)` creates `ErrorType.NotFound`).
- Metadata keys: `camelCase`.

## HTTP integration

- `To<Target>` conversion prefix: `ToProblemDetails`, `ToHttpResult`, `ToActionResult`, `ToResult`, `ToValidationError`.
- Overloads customizing success take a delegate named `onSuccess`; location factories are `locationFactory`.

## Options & DI

- One options class per package needing it: `KorasResults` prefix (`KorasResultsOptions`).
- Registration methods: `Add<PackageArea>` (`AddKorasResults`, `AddKorasResultsValidationBehavior`) on `IServiceCollection`, returning it.

## Type-parameter names

`T` for single value; `TIn`/`TOut` for transformations; `TRequest`/`TResponse` in MediatR context.

## Abbreviation policy

None. (`Configuration` not `Config`, `Metadata` not `Meta`.) Established acronyms keep BCL casing: `Json`, `Http`, `Uri`, `Id`.
