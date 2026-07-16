# Recipes: Testing

Snippet-level recipes for testing code built on Koras.Results. For the broader testing story
(fakes, `WebApplicationFactory` setup, MediatR pipelines), see the [testing guide](../guides/testing.md).
Examples use xUnit; the patterns translate directly to NUnit/MSTest.

## Asserting ValidationError field contents

Pattern-match the error down to `ValidationError`, then assert on `FieldErrors` — property name,
message, and optional code are all preserved in order:

```csharp
[Fact]
public async Task Register_with_bad_email_reports_the_email_field()
{
    var validator = new RegisterUserValidator();

    var result = await validator.ValidateToResultAsync(new RegisterUser("not-an-email", "Ada"));

    Assert.True(result.IsFailure);
    var validation = Assert.IsType<ValidationError>(result.Error);

    var emailError = Assert.Single(validation.FieldErrors, f => f.PropertyName == "Email");
    Assert.Equal("User.EmailInvalid", emailError.Code);          // FluentValidation .WithErrorCode
    Assert.Equal("Validation.Failed", validation.Code);          // the aggregate error's code
    Assert.Equal(ErrorType.Validation, validation.Type);
}
```

For multi-field assertions, project to tuples first — failure messages read much better:

```csharp
var fields = validation.FieldErrors.Select(f => (f.PropertyName, f.Code)).ToArray();
Assert.Contains(("Email", "User.EmailInvalid"), fields);
Assert.Contains(("DisplayName", (string?)null), fields);
```

## Asserting problem+json payloads with JsonElement

Read the raw response and assert against `JsonElement` — this tests the *actual wire contract*,
including extension members that typed `ProblemDetails` deserialization can mangle:

```csharp
[Fact]
public async Task Duplicate_email_returns_409_with_stable_error_code()
{
    var client = factory.CreateClient();
    await client.PostAsJsonAsync("/users", new { email = "ada@example.com", displayName = "Ada" });

    var response = await client.PostAsJsonAsync("/users", new { email = "ada@example.com", displayName = "Twin" });

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var root = doc.RootElement;

    Assert.Equal(409, root.GetProperty("status").GetInt32());
    Assert.Equal("User.DuplicateEmail", root.GetProperty("errorCode").GetString());
    Assert.True(root.TryGetProperty("traceId", out var traceId));
    Assert.False(string.IsNullOrEmpty(traceId.GetString()));
    Assert.False(root.TryGetProperty("metadata", out _));   // MetadataExposure=None: never leaks
}
```

The validation shape (`errors` dictionary keyed by property):

```csharp
using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
var errors = doc.RootElement.GetProperty("errors");
Assert.Equal(JsonValueKind.Array, errors.GetProperty("Title").ValueKind);
Assert.Equal("'Title' must not be empty.", errors.GetProperty("Title")[0].GetString());
```

## Snapshot-testing the wire shape

Snapshot tests catch *any* unintended change to the public error contract. With
[Verify](https://github.com/VerifyTests/Verify), scrub the volatile `traceId` and snapshot the
rest:

```csharp
[Fact]
public async Task NotFound_problem_shape_is_stable()
{
    var client = factory.CreateClient();

    var response = await client.GetAsync("/todos/00000000-0000-0000-0000-000000000001");
    var json = await response.Content.ReadAsStringAsync();

    await VerifyJson(json)
        .ScrubMember("traceId");
}
```

The approved snapshot then locks the contract:

```json
{
  "type": "https://errors.example.com/Todo.NotFound",
  "title": "Not Found",
  "status": 404,
  "detail": "No todo with id '00000000-0000-0000-0000-000000000001'.",
  "errorCode": "Todo.NotFound",
  "traceId": "{Scrubbed}"
}
```

Without a snapshot library, the same idea works with a normalized string comparison: parse,
remove `traceId`, re-serialize with `JsonSerializerOptions { WriteIndented = true }`, and compare
against a checked-in `.json` fixture file.

## Delegate-invocation-count assertions for short-circuiting

The combinators guarantee that on failure, `Map`/`Bind`/`Ensure`/`Tap` never invoke their
delegates. When your pipeline's correctness depends on that (e.g. "we must not charge a card
after validation failed"), assert it with counters:

```csharp
[Fact]
public async Task Failed_reservation_never_reaches_payment()
{
    var chargeCalls = 0;

    var result = await Task.FromResult(Result.Failure<Reservation>(
            Error.Conflict("Inventory.OutOfStock", "No stock.")))
        .BindAsync(reservation =>
        {
            chargeCalls++;
            return ChargeAsync(reservation);
        })
        .TapAsync(receipt => Assert.Fail("Tap must not run on failure"));

    Assert.Equal(0, chargeCalls);                            // short-circuited
    Assert.Equal("Inventory.OutOfStock", result.Error.Code); // original error, identity preserved
}
```

The mirror-image test for the success path — `TapError`/`MapError` must not run:

```csharp
[Fact]
public void Success_passes_through_error_combinators_untouched()
{
    var errorTaps = 0;

    var result = Result.Success(42)
        .TapError(_ => errorTaps++)
        .MapError(_ => Error.Unexpected("Never.Happens", "unreachable"));

    Assert.Equal(0, errorTaps);
    Assert.Equal(42, result.Value);
}
```

These counter-based tests need no mocking framework: closures over local variables are the whole
test double.

## Related documentation

- [Testing guide](../guides/testing.md)
- [All options reference](../configuration/all-options.md)
- [Configuration validation](../configuration/validation.md)
