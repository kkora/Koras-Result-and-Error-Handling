# Koras.Results.MediatR

MediatR integration for [Koras.Results](https://www.nuget.org/packages/Koras.Results): request validation that short-circuits with a failed `Result` instead of throwing.

```csharp
services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
services.AddValidatorsFromAssemblyContaining<Program>();
services.AddKorasResultsValidationBehavior();

// Handler signature stays clean:
public sealed class CreateUserHandler : IRequestHandler<CreateUserCommand, Result<UserDto>> { ... }
// Invalid requests never reach the handler; the pipeline returns
// Result<UserDto>.Failure(ValidationError) aggregated across all validators.
```

> **Licensing note:** this package depends on `MediatR [12.4, 13.0)` — the Apache-2.0 licensed release line. MediatR 13+ is commercially licensed and is intentionally not supported (see the repository's ADR-0006).

Documentation: https://github.com/korastechnologies/koras-results/tree/main/docs
