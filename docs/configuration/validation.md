# Configuration Validation

`KorasResultsOptions` validates its inputs **at configure time**, the moment you call a mapping
method — not later when a request happens to hit the bad mapping. A misconfigured application
fails at startup, in CI, on the first run; it never ships a subtly wrong error contract.

## What is validated

### Status codes must be 100–599

Both `MapErrorType` and `MapErrorCode` reject anything outside the valid HTTP range with an
`ArgumentOutOfRangeException`:

```csharp
var options = new KorasResultsOptions();

options.MapErrorType(ErrorType.Failure, 400);    // ok
options.MapErrorCode("Api.Teapot", 418);         // ok — unusual, but valid HTTP

options.MapErrorType(ErrorType.Failure, 99);     // throws ArgumentOutOfRangeException
options.MapErrorCode("Order.NotFound", 6000);    // throws ArgumentOutOfRangeException
```

The exception message states the rule: `HTTP status codes must be between 100 and 599.`

### Error codes must be non-empty

`MapErrorCode` rejects null or whitespace codes with an `ArgumentException`:

```csharp
options.MapErrorCode("", 400);      // throws ArgumentException
options.MapErrorCode("   ", 400);   // throws ArgumentException
options.MapErrorCode(null!, 400);   // throws ArgumentException
```

### Resolution guards

`GetStatusCode(null!)` throws `ArgumentNullException`. (The same guard discipline runs through
the whole family: `Error` construction rejects empty codes/messages, `ValidationError` rejects
empty field-error collections, `Result.Success<T>(null)` throws — invalid states are
unrepresentable rather than checked downstream.)

## When validation runs

Because the checks live inside the mapping methods themselves, they execute whenever your
`configure` delegate executes. With the options pattern that is on **first resolution** of
`IOptions<KorasResultsOptions>` — for web apps, effectively the first failing request unless you
force it earlier. Two ways to guarantee startup-time failure:

```csharp
// Option A: resolve once during startup.
var app = builder.Build();
_ = app.Services.GetRequiredService<IOptions<KorasResultsOptions>>().Value;

// Option B: eager validation via the options pipeline.
builder.Services.AddOptions<KorasResultsOptions>().ValidateOnStart();
builder.Services.AddKorasResults(options =>
    options.MapErrorType(ErrorType.Failure, statusFromSomewhere));   // throws at startup if invalid
```

For literal status codes this rarely matters — the values are visibly correct in code review.
It matters when codes are computed or come from configuration you bound yourself.

## The fail-fast philosophy

This is why mappings are a code API rather than a JSON schema:

- **Typos throw, not misroute.** `MapErrorType((ErrorType)42, 400)` cannot happen from typed
  code; `MapErrorCode("", 400)` throws immediately. A hypothetical JSON mapping section would
  silently ignore a misspelled key and ship the wrong status codes to production.
- **The valid range is enforced once, centrally.** No request-time branch ever has to wonder
  whether a stored status code is legal; `GetStatusCode` can be a pure lookup.
- **Exceptions at configure time are cheap; wrong contracts at run time are expensive.** A
  startup crash is caught by the first smoke test. A 200-instead-of-402 is caught by an unhappy
  integration partner.

One thing validation deliberately does *not* check: whether a code passed to `MapErrorCode`
exists in your error catalog. The options class cannot know your catalog — comparison is ordinal
at lookup time, so a code that matches nothing simply never fires. Cover that gap with tests.

## Testing your configuration

`GetStatusCode` is pure, so house rules are one-line assertions — no host, no HTTP:

```csharp
public sealed class KorasResultsOptionsPolicyTests
{
    private static KorasResultsOptions Configured()
    {
        var options = new KorasResultsOptions();
        MyApp.ConfigureKorasResults(options);   // the same method Program.cs passes to AddKorasResults
        return options;
    }

    [Fact]
    public void Configuration_is_valid() =>
        _ = Configured();   // any 100-599 / empty-code violation throws right here

    [Theory]
    [InlineData("Billing.PaymentRequired", 402)]
    [InlineData("Api.RateLimited", 429)]
    public void Code_overrides_match_policy(string code, int expected) =>
        Assert.Equal(expected, Configured().GetStatusCode(Error.Failure(code, "m")));

    [Fact]
    public void Mapped_codes_exist_in_the_error_catalog()
    {
        // Guard against typos MapErrorCode cannot detect: resolve through a *real* catalog
        // error, so a renamed or misspelled code fails this test.
        Assert.Equal(402, Configured().GetStatusCode(BillingErrors.PaymentRequired()));
    }
}
```

Extracting the configuration delegate into a named method (`MyApp.ConfigureKorasResults`) is the
key move: production and tests share the exact same code path.

## Related documentation

- [All options reference](all-options.md)
- [Configuration guide](../guides/configuration.md)
- [Testing recipes](../recipes/testing-recipes.md)
