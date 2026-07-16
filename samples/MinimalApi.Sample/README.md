# MinimalApi.Sample

A todo API showing `Koras.Results.AspNetCore` with Minimal APIs: every endpoint ends in one conversion call (`ToHttpResult` / `ToCreatedHttpResult`), and every failure becomes a correct RFC 9457 `application/problem+json` response. FluentValidation runs through `ValidateToResultAsync` — no exceptions, no filters.

## Prerequisites

- .NET SDK 10 (see repository `global.json`)
- No configuration or secrets required.

## Run

```bash
dotnet run --project samples/MinimalApi.Sample
```

The app listens on the URL printed at startup (e.g. `http://localhost:5000`).

## Try it

```bash
BASE=http://localhost:5000

# Create (201 + Location header)
curl -si $BASE/todos -H 'content-type: application/json' -d '{"title":"Write docs"}'

# Validation failure (400 + errors dictionary)
curl -s $BASE/todos -H 'content-type: application/json' -d '{"title":""}' | jq
# {
#   "type": "https://errors.example.com/Validation.Failed",
#   "title": "Bad Request",
#   "status": 400,
#   "errors": { "Title": ["'Title' must not be empty."] },
#   "errorCode": "Validation.Failed",
#   "traceId": "..."
# }

# Not found (404)
curl -s $BASE/todos/00000000-0000-0000-0000-000000000001 | jq '.status, .errorCode'
# 404, "Todo.NotFound"

# Domain rule failure — complete twice (second call: 400, Todo.AlreadyCompleted)
ID=$(curl -s $BASE/todos -H 'content-type: application/json' -d '{"title":"x"}' | jq -r .id)
curl -s -o /dev/null -w '%{http_code}\n' -X POST $BASE/todos/$ID/complete   # 200
curl -s -X POST $BASE/todos/$ID/complete | jq '.errorCode'                  # "Todo.AlreadyCompleted"

# Delete (204)
curl -s -o /dev/null -w '%{http_code}\n' -X DELETE $BASE/todos/$ID
```

## What to look at

- `Program.cs` — `AddKorasResults` options (status remapping, custom `type` URIs); one-line endpoint conversions.
- `TodoErrors` — the error-catalog pattern: static factories with stable codes.
- `TodoStore` — domain code returning `Result`/`Result<T>` with zero HTTP awareness.

## Switching to released packages

Replace the `<ProjectReference>` items in the csproj with `<PackageReference>`s to `Koras.Results.AspNetCore` and `Koras.Results.FluentValidation`.

## Related documentation

- [Minimal API guide](../../docs/guides/minimal-api.md)
- [ProblemDetails feature guide](../../docs/features/problemdetails.md)
- [Configuration reference](../../docs/configuration/all-options.md)
