using WorkerServiceSample;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<FlakyDownstream>();
builder.Services.AddHostedService<SyncWorker>();

var host = builder.Build();
await host.RunAsync();
