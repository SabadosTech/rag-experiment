using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using RagExperiment.Ingestion;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddSerilog(configuration => configuration
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:yyyy-MM-dd HH:mm:ss zzz} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));
builder.Services.AddHostedService<StartupCheck>();

using var host = builder.Build();
await host.StartAsync();
await host.StopAsync();
