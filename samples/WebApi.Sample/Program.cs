// MVC + MediatR sample: controllers dispatch commands/queries whose handlers return Results.
// The Koras.Results MediatR validation behavior short-circuits invalid requests before they
// reach a handler; controllers convert with ToActionResult.
using FluentValidation;
using Koras.Results.AspNetCore;
using Koras.Results.MediatR;
using WebApiSample.Users;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddKorasResults();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddKorasResultsValidationBehavior();
builder.Services.AddSingleton<UserRepository>();

var app = builder.Build();
app.MapControllers();
app.Run();
