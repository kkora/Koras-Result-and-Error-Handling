# Console.Sample

Demonstrates the zero-dependency `Koras.Results` core in a plain console application: success/failure results, railway composition (`Bind`/`Ensure`/`Map`/`Match`), field-level validation errors, exception boundaries (`Result.Try`), result combination, and async pipelines.

## Prerequisites

- .NET SDK 10 (see repository `global.json`)
- No configuration or secrets required — the sample is fully self-contained.

## Run

```bash
dotnet run --project samples/Console.Sample
```

## Expected output (abridged)

```
── 1. Basic success and failure ──
Found: The Pragmatic Programmer (...)
Failed: Product.NotFound — No product with SKU 'no-such-sku'.

── 2. Railway composition: parse → find → ensure → price ──
  'book-1:2' => total ...
  'book-1:999' => Conflict: Order.InsufficientStock
  'garbage' => Validation: Order.MalformedInput

── 3. Validation errors with field detail ──
  Email: Email is required.
  Age: You must be at least 18.

── 4. Exception boundary with Result.Try ──
  Input.NotANumber: The input is not a valid integer.

── 5. Combining independent checks ──
  combined outcome: Validation.Failed

── 6. Async pipeline ──
  report line: Domain-Driven Design => ...
```

(Currency formatting depends on your locale.)

## Error scenarios shown

| Scenario | ErrorType | Code |
|---|---|---|
| Unknown SKU | NotFound | `Product.NotFound` |
| Insufficient stock | Conflict | `Order.InsufficientStock` |
| Malformed input | Validation | `Order.MalformedInput` |
| Non-numeric parse | Validation | `Input.NotANumber` |

## Switching to released packages

Replace the `<ProjectReference>` in `Console.Sample.csproj` with:

```xml
<PackageReference Include="Koras.Results" Version="x.y.z" />
```

## Related documentation

- [Quick start](../../docs/getting-started/quick-start.md)
- [Core abstractions](../../docs/concepts/core-abstractions.md)
- [Functional composition](../../docs/features/functional-composition.md)
