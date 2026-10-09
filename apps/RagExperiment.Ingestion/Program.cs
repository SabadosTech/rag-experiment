using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RagExperiment.Documents.Abstractions;
using RagExperiment.Documents.Readers;
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
builder.Services.AddSingleton<IDocumentReader, EpubDocumentReader>();
builder.Services.AddSingleton<DocumentReaderResolver>();
builder.Services.AddTransient<DocumentIngestion>();

using var host = builder.Build();
await host.StartAsync();
try
{
    var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
    await host.Services.GetRequiredService<DocumentIngestion>().RunAsync(lifetime.ApplicationStopping);
}
catch (Exception exception)
{
    host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Ingestion")
        .LogError(exception, "Document ingestion failed.");
    Environment.ExitCode = 1;
}
finally
{
    await host.StopAsync();
}
