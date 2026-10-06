using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RagExperiment.Cli;

internal sealed class StartupCheck(
    ILogger<StartupCheck> logger,
    IConfiguration configuration) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "{ApplicationName} initialized. Hosting, DI, configuration, and logging are ready.",
            configuration["Application:Name"] ?? "RagExperiment.Cli");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
