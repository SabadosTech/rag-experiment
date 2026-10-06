using Microsoft.Extensions.AI;
using Xunit;

namespace RagExperiment.Tests;

public sealed class InfrastructureTests
{
    [Fact]
    public void StandardAiContractsAreAvailableToTheTestRunner()
    {
        Assert.True(typeof(IChatClient).IsInterface);
        Assert.True(typeof(IEmbeddingGenerator<string, Embedding<float>>).IsInterface);
    }
}
