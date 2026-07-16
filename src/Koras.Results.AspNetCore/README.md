# Koras.Results.AspNetCore

ASP.NET Core integration for [Koras.Results](https://www.nuget.org/packages/Koras.Results): one extension method turns any `Result` failure into a correct RFC 9457 `application/problem+json` response.

```csharp
builder.Services.AddKorasResults();

// Minimal API
app.MapGet("/users/{id}", async (Guid id, IUserService users, CancellationToken ct)
    => (await users.GetAsync(id, ct)).ToHttpResult());
// NotFound error -> 404 problem details, ValidationError -> 400 with errors dictionary

// MVC
[HttpGet("{id}")]
public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    => (await _users.GetAsync(id, ct)).ToActionResult();
```

## Features

- `ErrorType` → status code mapping (overridable per type and per error code)
- `ValidationError` → `errors` dictionary matching ASP.NET Core's validation shape
- `errorCode` and `traceId` ProblemDetails extensions
- Secure by default: `Unexpected` error details are never sent to clients unless explicitly enabled
- `IErrorMessageLocalizer` hook for localized client messages
- Minimal API (`ToHttpResult`) and MVC (`ToActionResult`) adapters, sync and `Task` sugar

Documentation: https://github.com/korastechnologies/koras-results/tree/main/docs
