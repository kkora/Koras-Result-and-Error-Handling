using Koras.Results;

namespace ConsoleSample;

public sealed record Product(string Sku, string Name, decimal Price, int Stock);

public sealed record Order(string Sku, int Quantity)
{
    public static Result<Order> Parse(string input)
    {
        var parts = input.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var quantity) || quantity < 1)
        {
            return Error.Validation("Order.MalformedInput", "Expected the form 'sku:quantity'.");
        }

        return new Order(parts[0], quantity);
    }
}

public static class Catalog
{
    private static readonly Dictionary<string, Product> Products = new()
    {
        ["book-1"] = new Product("book-1", "The Pragmatic Programmer", 42.00m, 3),
        ["book-2"] = new Product("book-2", "Domain-Driven Design", 55.50m, 1),
    };

    public static Result<Product> Find(string sku) =>
        Products.TryGetValue(sku, out var product)
            ? product
            : Error.NotFound("Product.NotFound", $"No product with SKU '{sku}'.");

    public static async Task<Result<Product>> FindAsync(string sku)
    {
        await Task.Delay(10);
        return Find(sku);
    }
}

public static class Validation
{
    public static Result ValidateSignup(string email, int age)
    {
        var fieldErrors = new List<FieldError>();
        if (string.IsNullOrWhiteSpace(email))
        {
            fieldErrors.Add(new FieldError("Email", "Email is required."));
        }

        if (age < 18)
        {
            fieldErrors.Add(new FieldError("Age", "You must be at least 18."));
        }

        return fieldErrors.Count == 0 ? Result.Success() : new ValidationError(fieldErrors);
    }
}
