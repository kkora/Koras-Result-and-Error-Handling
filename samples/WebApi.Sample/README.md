# WebApi.Sample (MVC + MediatR)

A users API showing the full Koras.Results stack: MVC controllers dispatch MediatR commands, handlers return `Result<T>`, the **validation behavior** short-circuits invalid commands before they reach a handler (no `ValidationException`), and `ToActionResult` converts everything into correct HTTP responses.

## Prerequisites

- .NET SDK 10 (see repository `global.json`)
- No configuration or secrets required.

> **MediatR licensing note:** this sample (like `Koras.Results.MediatR`) uses MediatR 12.x, the Apache-2.0 licensed release line (ADR-0006).

## Run

```bash
dotnet run --project samples/WebApi.Sample
```

## Try it

```bash
BASE=http://localhost:5000

# Register (201 Created + Location)
curl -si $BASE/users -H 'content-type: application/json' \
  -d '{"email":"ada@example.com","displayName":"Ada"}'

# Validation short-circuit (400; the handler never ran)
curl -s $BASE/users -H 'content-type: application/json' \
  -d '{"email":"not-an-email","displayName":""}' | jq '.errors'

# Duplicate email -> domain Conflict (409)
curl -s $BASE/users -H 'content-type: application/json' \
  -d '{"email":"ada@example.com","displayName":"Ada again"}' | jq '.status, .errorCode'
# 409, "User.DuplicateEmail"

# Unknown user (404)
curl -s $BASE/users/00000000-0000-0000-0000-000000000001 | jq '.errorCode'
# "User.NotFound"
```

## What to look at

- `Program.cs` — `AddKorasResultsValidationBehavior()` wiring next to MediatR + FluentValidation registration.
- `Users/UsersDomain.cs` — commands, validators, handlers returning `Result`/`Result<T>`; the `UserErrors` catalog.
- `Users/UsersController.cs` — controllers that never inspect errors: `ToActionResult` (with a `CreatedAtAction` success factory for POST).

## Switching to released packages

Replace the `<ProjectReference>` items with `<PackageReference>`s to `Koras.Results.AspNetCore`, `Koras.Results.FluentValidation`, and `Koras.Results.MediatR`.

## Related documentation

- [ASP.NET Core guide](../../docs/guides/aspnet-core.md)
- [MediatR feature guide](../../docs/features/mediatr.md)
- [FluentValidation feature guide](../../docs/features/fluentvalidation.md)
