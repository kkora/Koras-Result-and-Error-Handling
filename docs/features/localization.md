# Localization

## Overview

`Koras.Results.AspNetCore` localizes client-facing error text through `IErrorMessageLocalizer`, a two-method interface: `Localize(Error, CultureInfo)` produces the ProblemDetails `detail`, and `LocalizeField(FieldError, CultureInfo)` produces each entry of the validation `errors` dictionary. The default implementation, `PassThroughErrorMessageLocalizer`, returns messages unchanged — localization is strictly opt-in. Because errors carry stable `Code`s independent of their `Message`s, a localizer can translate by code without touching domain logic.

## When to use it

- APIs serving users in multiple languages that need translated ProblemDetails `detail` and validation messages.
- Enforcing a client-facing message catalog keyed by `Error.Code` / `FieldError.Code`, decoupled from internal developer-facing messages.
- Post-processing messages centrally (tone, terminology, branding) without editing every error factory.

## When not to use it

- Single-language APIs — the pass-through default is already correct; register nothing.
- Localizing *success* payloads — the localizer only runs for failure conversion; localize DTO content with ASP.NET Core's own localization stack.
- Non-HTTP applications — the interface lives in the AspNetCore package and is invoked during ProblemDetails conversion only.

## Installation

```bash
dotnet add package Koras.Results.AspNetCore
```

The core `Koras.Results` package comes transitively.

## Basic configuration

Register your implementation as a **singleton**, then call `AddKorasResults`:

```csharp
builder.Services.AddSingleton<IErrorMessageLocalizer, CatalogErrorMessageLocalizer>();
builder.Services.AddKorasResults();
```

Ordering matters because `AddKorasResults` registers the default via `TryAddSingleton` — it only adds the pass-through when no `IErrorMessageLocalizer` is registered yet, so **a custom registration placed first wins** and is never overwritten. (Equivalently, use `TryAdd` semantics for your own registration and place it before.)

## Basic usage

```csharp
using System.Globalization;
using Koras.Results;
using Koras.Results.AspNetCore;

public sealed class CatalogErrorMessageLocalizer : IErrorMessageLocalizer
{
    private static readonly Dictionary<(string Code, string Language), string> Catalog = new()
    {
        [("Todo.NotFound", "de")] = "Das Todo wurde nicht gefunden.",
        [("Todo.TitleRequired", "de")] = "Der Titel darf nicht leer sein.",
    };

    public string Localize(Error error, CultureInfo culture) =>
        Catalog.TryGetValue((error.Code, culture.TwoLetterISOLanguageName), out var text)
            ? text
            : error.Message; // fall back to the original message

    public string LocalizeField(FieldError fieldError, CultureInfo culture) =>
        fieldError.Code is not null
            && Catalog.TryGetValue((fieldError.Code, culture.TwoLetterISOLanguageName), out var text)
            ? text
            : fieldError.Message;
}
```

The culture passed in is **`CultureInfo.CurrentUICulture`** at conversion time. In ASP.NET Core, set it per request with the request-localization middleware (see below); returning the input message is the standard fallback for unknown codes.

## Dependency-injection usage

The localizer is resolved from `HttpContext.RequestServices` when the deferred endpoint results execute, and injected nowhere else — your implementation may itself take singleton-safe dependencies (e.g. `IStringLocalizerFactory`):

```csharp
public sealed class ResourceErrorMessageLocalizer(IStringLocalizerFactory factory) : IErrorMessageLocalizer
{
    private readonly IStringLocalizer _localizer = factory.Create("Errors", typeof(Program).Assembly.GetName().Name!);

    public string Localize(Error error, CultureInfo culture)
    {
        var entry = _localizer[error.Code];
        return entry.ResourceNotFound ? error.Message : entry.Value;
    }

    public string LocalizeField(FieldError fieldError, CultureInfo culture)
    {
        if (fieldError.Code is null) return fieldError.Message;
        var entry = _localizer[fieldError.Code];
        return entry.ResourceNotFound ? fieldError.Message : entry.Value;
    }
}
```

(`IStringLocalizer` reads `CultureInfo.CurrentUICulture` internally, which matches the culture argument during request processing.)

## ASP.NET Core usage

Wire up request localization so `CurrentUICulture` reflects the client's `Accept-Language` (or query/cookie provider) before results execute:

```csharp
builder.Services.AddSingleton<IErrorMessageLocalizer, CatalogErrorMessageLocalizer>();
builder.Services.AddKorasResults();

var app = builder.Build();

app.UseRequestLocalization(options =>
{
    options.SetDefaultCulture("en");
    options.AddSupportedUICultures("en", "de", "fr");
});

app.MapGet("/todos/{id:guid}", (Guid id, TodoStore store) =>
    store.Find(id).ToHttpResult());
```

A request with `Accept-Language: de` now receives `"detail": "Das Todo wurde nicht gefunden."` while logs keep the original English message.

## Console application usage

Rarely needed, but the eager conversion accepts a localizer explicitly, so non-host code can localize without DI:

```csharp
CultureInfo.CurrentUICulture = new CultureInfo("de");
var problem = Error.NotFound("Todo.NotFound", "No todo with id '42'.")
    .ToProblemDetails(localizer: new CatalogErrorMessageLocalizer());
```

## Advanced configuration

- **Fallback discipline** — always return the original message for unknown codes; never throw for missing translations.
- **Suppressed `Unexpected` details** — when `IncludeUnexpectedErrorDetails` is `false` (the default), the generic English `"An unexpected error occurred."` is used *instead of* calling `Localize` for `Unexpected` errors; the localizer cannot leak suppressed content.
- **Model-level field errors** — `FieldError.PropertyName` may be `""`; key translations on `FieldError.Code`, not the property name.
- **Explicit overloads** — `ToProblemDetails(options, localizer)` lets middleware and tests supply a specific localizer, bypassing DI.

## Public API

```csharp
public interface IErrorMessageLocalizer
{
    string Localize(Error error, CultureInfo culture);
    string LocalizeField(FieldError fieldError, CultureInfo culture);
}

public sealed class PassThroughErrorMessageLocalizer : IErrorMessageLocalizer
{
    public static readonly PassThroughErrorMessageLocalizer Instance; // for non-DI scenarios
    public string Localize(Error error, CultureInfo culture);         // returns error.Message
    public string LocalizeField(FieldError fieldError, CultureInfo culture); // returns fieldError.Message
}

public static class KorasResultsServiceCollectionExtensions
{
    public static IServiceCollection AddKorasResults(this IServiceCollection services, Action<KorasResultsOptions>? configure = null);
}
```

## Execution lifecycle

When a deferred failure result executes (Minimal API or MVC), the builder resolves `IErrorMessageLocalizer` from `HttpContext.RequestServices` — falling back to `PassThroughErrorMessageLocalizer.Instance` when nothing is registered — captures `CultureInfo.CurrentUICulture`, then calls `Localize(error, culture)` for the `detail` field and `LocalizeField(fieldError, culture)` for every validation dictionary entry. The eager `ToProblemDetails` overloads follow the same steps with the localizer you pass (or the pass-through default).

## Error handling

The conversion pipeline does not catch localizer exceptions — a throwing localizer fails the response, so treat missing translations as fallbacks, not errors. The pass-through implementation throws `ArgumentNullException` only for null inputs. Localization never changes status codes, `errorCode`, or any extension — only human-readable text.

## Cancellation

Not applicable: localization is a synchronous, in-memory call during response writing; the interface deliberately has no async form or token.

## Logging

The surrounding mapper logs under `Koras.Results.AspNetCore.ResultHttpMapper` (Debug `Mapped error {ErrorCode} ({ErrorType}) to HTTP {StatusCode}`; Warning when `Unexpected` details are suppressed). Log entries always carry the original, unlocalized data — localization affects only the client payload.

## Telemetry

Not applicable directly: localized text is never exported to traces; [Koras.Results.OpenTelemetry](opentelemetry.md) tags spans with codes only, which keeps telemetry culture-invariant and aggregatable.

## Security considerations

The localizer sees every outgoing error message — keep implementations free of side effects and be careful not to *add* internal detail during translation. The `Unexpected`-detail suppression runs before localization, so the secure default cannot be undone by a localizer. Treat translation catalogs as code-reviewed content (they are injected into client responses).

## Performance considerations

The localizer is called once per failure response, plus once per validation field error. It sits on the error path only — successes never touch it. Cache catalogs in memory (dictionaries, `IStringLocalizer` resource caches); avoid per-call I/O, since a slow localizer delays every error response.

## Thread safety

Implementations **must be thread-safe** — they are resolved as singletons and invoked concurrently across requests. `PassThroughErrorMessageLocalizer` is stateless; `Instance` is a shared static. Prefer immutable catalogs or `FrozenDictionary` for lookup tables.

## Testing applications using this feature

Test localizers directly, and test the wiring through the explicit `ToProblemDetails` overload:

```csharp
[Fact]
public void Known_code_is_translated()
{
    var localizer = new CatalogErrorMessageLocalizer();
    var error = Error.NotFound("Todo.NotFound", "No todo with id '42'.");

    var text = localizer.Localize(error, new CultureInfo("de"));

    Assert.Equal("Das Todo wurde nicht gefunden.", text);
}

[Fact]
public void Detail_uses_the_localizer()
{
    var original = CultureInfo.CurrentUICulture;
    CultureInfo.CurrentUICulture = new CultureInfo("de");
    try
    {
        var problem = Error.NotFound("Todo.NotFound", "No todo with id '42'.")
            .ToProblemDetails(localizer: new CatalogErrorMessageLocalizer());

        Assert.Equal("Das Todo wurde nicht gefunden.", problem.Detail);
    }
    finally
    {
        CultureInfo.CurrentUICulture = original;
    }
}
```

## Complete example

```csharp
using System.Globalization;
using Koras.Results;
using Koras.Results.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IErrorMessageLocalizer, GermanErrorMessageLocalizer>(); // before AddKorasResults
builder.Services.AddKorasResults();

var app = builder.Build();

app.UseRequestLocalization(options =>
{
    options.SetDefaultCulture("en");
    options.AddSupportedUICultures("en", "de");
});

app.MapGet("/greetings/{id:int}", (int id) =>
{
    Result<string> greeting = id == 1
        ? "Hello!"
        : Error.NotFound("Greeting.NotFound", $"No greeting with id '{id}'.");
    return greeting.ToHttpResult();
});

app.Run();

public sealed class GermanErrorMessageLocalizer : IErrorMessageLocalizer
{
    public string Localize(Error error, CultureInfo culture) =>
        culture.TwoLetterISOLanguageName == "de" && error.Code == "Greeting.NotFound"
            ? "Die Begrüßung wurde nicht gefunden."
            : error.Message;

    public string LocalizeField(FieldError fieldError, CultureInfo culture) => fieldError.Message;
}
```

`curl -H 'Accept-Language: de' http://localhost:5000/greetings/2` returns a 404 whose `detail` is the German text; without the header, the original English message.

## Common mistakes

- Registering the custom localizer *after* `AddKorasResults` with `TryAddSingleton` — the pass-through default is already present, so your `TryAdd` is skipped; register first (or use a plain `AddSingleton`, understanding you now own the registration).
- Forgetting `UseRequestLocalization` — `CurrentUICulture` stays the server default and every client gets the same language.
- Throwing for missing translations instead of falling back to `error.Message`/`fieldError.Message`.
- Keying `LocalizeField` on `PropertyName` — model-level failures have an empty property name; key on `FieldError.Code`.
- Expecting `Unexpected` errors to be localized — their detail is replaced by the generic message first when details are suppressed (the secure default).

## Troubleshooting

- **Translations never appear** — confirm the registration order relative to `AddKorasResults`, and that the failing path goes through the adapters (explicit `ToProblemDetails()` calls without a `localizer` argument use the pass-through default).
- **Wrong language chosen** — inspect `CultureInfo.CurrentUICulture` inside the request; check request-localization providers and supported-culture lists.
- **Validation messages untranslated while `detail` is** — implement `LocalizeField` too; the two methods serve different payload parts.
- **Culture-sensitive formatting looks wrong** — format numbers/dates inside your localizer with the provided `culture`, not the invariant culture.

## Related features

- [ProblemDetails conversion](problemdetails.md) — where `detail` and `errors` are produced.
- [FluentValidation integration](fluentvalidation.md) — the source of `FieldError` codes worth translating.
- [Minimal API](minimal-api.md) / [MVC](mvc.md) adapters — resolve the localizer from request services at execution time.
