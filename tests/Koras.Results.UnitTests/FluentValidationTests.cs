using FluentValidation;
using FluentValidation.Results;
using Koras.Results.FluentValidation;

namespace Koras.Results.UnitTests;

public class FluentValidationTests
{
    private sealed record Signup(string Email, int Age);

    private sealed class SignupValidator : AbstractValidator<Signup>
    {
        public SignupValidator()
        {
            RuleFor(x => x.Email).NotEmpty().WithErrorCode("Email.Required");
            RuleFor(x => x.Age).GreaterThanOrEqualTo(18);
        }
    }

    [Fact]
    public void ToResult_returns_success_for_valid_results()
    {
        var result = new ValidationResult().ToResult();
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ToResult_with_value_carries_the_instance_on_success()
    {
        var signup = new Signup("a@example.com", 30);
        var result = new ValidationResult().ToResult(signup);

        Assert.Same(signup, result.Value);
    }

    [Fact]
    public void ToResult_maps_failures_with_property_message_and_code()
    {
        var validationResult = new ValidationResult(
        [
            new ValidationFailure("Email", "Email is required.") { ErrorCode = "Email.Required" },
            new ValidationFailure("Age", "Too young."),
        ]);

        var result = validationResult.ToResult();

        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal(2, error.FieldErrors.Count);
        Assert.Equal("Email", error.FieldErrors[0].PropertyName);
        Assert.Equal("Email is required.", error.FieldErrors[0].Message);
        Assert.Equal("Email.Required", error.FieldErrors[0].Code);
    }

    [Fact]
    public void Model_level_failures_keep_an_empty_property_name()
    {
        var validationResult = new ValidationResult([new ValidationFailure(string.Empty, "Model invalid.")]);

        var error = validationResult.ToValidationError();

        Assert.Equal(string.Empty, error.FieldErrors[0].PropertyName);
    }

    [Fact]
    public void ToValidationError_rejects_valid_results()
    {
        Assert.Throws<InvalidOperationException>(() => new ValidationResult().ToValidationError());
    }

    [Fact]
    public void ValidateToResult_runs_the_validator_both_ways()
    {
        var validator = new SignupValidator();

        var valid = validator.ValidateToResult(new Signup("a@example.com", 30));
        Assert.True(valid.IsSuccess);

        var invalid = validator.ValidateToResult(new Signup(string.Empty, 10));
        var error = Assert.IsType<ValidationError>(invalid.Error);
        Assert.Equal(2, error.FieldErrors.Count);
        Assert.Contains(error.FieldErrors, f => f.Code == "Email.Required");
    }

    [Fact]
    public async Task ValidateToResultAsync_runs_the_validator_both_ways()
    {
        var validator = new SignupValidator();

        var valid = await validator.ValidateToResultAsync(new Signup("a@example.com", 30));
        Assert.True(valid.IsSuccess);
        Assert.Equal("a@example.com", valid.Value.Email);

        var invalid = await validator.ValidateToResultAsync(new Signup(string.Empty, 30));
        Assert.True(invalid.IsFailure);
    }

    [Fact]
    public async Task ValidateToResultAsync_propagates_cancellation()
    {
        var validator = new CancellationObservingValidator();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => validator.ValidateToResultAsync(new Signup("x", 1), cts.Token));
    }

    [Fact]
    public void Guards_reject_null_arguments()
    {
        var validator = new SignupValidator();
        Assert.Throws<ArgumentNullException>(() => ((ValidationResult)null!).ToResult());
        Assert.Throws<ArgumentNullException>(() => ((ValidationResult)null!).ToValidationError());
        Assert.Throws<ArgumentNullException>(() => validator.ValidateToResult(null!));
        Assert.Throws<ArgumentNullException>(() => ((IValidator<Signup>)null!).ValidateToResult(new Signup("x", 1)));
    }

    private sealed class CancellationObservingValidator : AbstractValidator<Signup>
    {
        public CancellationObservingValidator()
        {
            RuleFor(x => x.Email).MustAsync(async (_, ct) =>
            {
                await Task.Delay(1000, ct);
                return true;
            });
        }
    }
}
