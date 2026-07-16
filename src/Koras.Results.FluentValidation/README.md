# Koras.Results.FluentValidation

FluentValidation integration for [Koras.Results](https://www.nuget.org/packages/Koras.Results): validate directly into `Result<T>`.

```csharp
Result<CreateUser> validated = await _validator.ValidateToResultAsync(command, ct);
// invalid -> failure with ValidationError carrying one FieldError per rule violation
// valid   -> success carrying the validated instance

return validated
    .Bind(cmd => _users.Create(cmd))
    .ToHttpResult(); // with Koras.Results.AspNetCore: 400 problem details on validation failure
```

Property names, messages, and FluentValidation error codes are preserved on each `FieldError`.

Documentation: https://github.com/korastechnologies/koras-results/tree/main/docs
