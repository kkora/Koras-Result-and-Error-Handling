// Console sample: core Result usage without any framework — composition, validation errors,
// exception boundaries, and combination. See README.md for the expected output.
using ConsoleSample;
using Koras.Results;

Console.WriteLine("── 1. Basic success and failure ──");
var found = Catalog.Find("book-1");
Console.WriteLine(found.Match(
    product => $"Found: {product.Name} ({product.Price:C})",
    error => $"Failed: {error.Code}"));

var missing = Catalog.Find("no-such-sku");
Console.WriteLine(missing.Match(
    product => $"Found: {product.Name}",
    error => $"Failed: {error.Code} — {error.Message}"));

Console.WriteLine();
Console.WriteLine("── 2. Railway composition: parse → find → ensure → price ──");
foreach (var input in new[] { "book-1:2", "book-1:999", "garbage" })
{
    var outcome = Order.Parse(input)
        .Bind(order => Catalog.Find(order.Sku).Map(product => (order, product)))
        .Ensure(pair => pair.product.Stock >= pair.order.Quantity,
                pair => Error.Conflict("Order.InsufficientStock", $"Only {pair.product.Stock} left."))
        .Map(pair => pair.product.Price * pair.order.Quantity);

    Console.WriteLine($"  '{input}' => " + outcome.Match(
        total => $"total {total:C}",
        error => $"{error.Type}: {error.Code}"));
}

Console.WriteLine();
Console.WriteLine("── 3. Validation errors with field detail ──");
var signup = Validation.ValidateSignup(email: "", age: 15);
signup.Switch(
    onSuccess: () => Console.WriteLine("  signup valid"),
    onFailure: error =>
    {
        var validation = (ValidationError)error;
        foreach (var field in validation.FieldErrors)
        {
            Console.WriteLine($"  {field.PropertyName}: {field.Message}");
        }
    });

Console.WriteLine();
Console.WriteLine("── 4. Exception boundary with Result.Try ──");
var parsed = Result.Try(
    () => int.Parse("not-a-number", System.Globalization.CultureInfo.InvariantCulture),
    ex => Error.Validation("Input.NotANumber", "The input is not a valid integer."));
Console.WriteLine("  " + parsed.Match(v => $"parsed {v}", e => $"{e.Code}: {e.Message}"));

Console.WriteLine();
Console.WriteLine("── 5. Combining independent checks ──");
var combined = Result.Combine(
    Validation.ValidateSignup("ada@example.com", 30),
    Validation.ValidateSignup("", 12));
Console.WriteLine($"  combined outcome: {(combined.IsFailure ? combined.Error.Code : "all valid")}");

Console.WriteLine();
Console.WriteLine("── 6. Async pipeline ──");
var report = await Catalog.FindAsync("book-2")
    .MapAsync(product => $"{product.Name} => {product.Price:C}")
    .MatchAsync(line => $"  report line: {line}", error => $"  failed: {error.Code}");
Console.WriteLine(report);
