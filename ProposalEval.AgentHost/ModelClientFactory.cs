using Microsoft.Extensions.AI;

namespace ProposalEval.AgentHost;

public sealed class ModelClientFactory(IConfiguration configuration, IHttpClientFactory http)
{
    public IChatClient Create()
    {
        var azureKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
                       ?? configuration["AzureOpenAI:ApiKey"];
        var azureEndpoint = FirstNonEmpty(
            Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT"),
            configuration["AzureOpenAI:Endpoint"]);
        var azureDeployment = FirstNonEmpty(
            Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT"),
            configuration["AzureOpenAI:Deployment"],
            configuration["AzureOpenAI:Model"]);
        var apiVersion = FirstNonEmpty(
            Environment.GetEnvironmentVariable("AZURE_OPENAI_API_VERSION"),
            configuration["AzureOpenAI:ApiVersion"]) ?? "2024-06-01";

        if (string.IsNullOrWhiteSpace(azureKey))
            throw new InvalidOperationException("Set AZURE_OPENAI_API_KEY.");

        if (string.IsNullOrWhiteSpace(azureEndpoint) || string.IsNullOrWhiteSpace(azureDeployment))
            throw new InvalidOperationException(
                "AZURE_OPENAI_API_KEY is set. Also set AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_DEPLOYMENT.");

        var client = new AzureChatCompletionsClient(
            http.CreateClient("AzureOpenAI"),
            azureEndpoint,
            azureDeployment,
            azureKey,
            apiVersion);

        return client
            .AsBuilder()
            .UseFunctionInvocation(configure: c => c.MaximumIterationsPerRequest = 30)
            .Build();
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
